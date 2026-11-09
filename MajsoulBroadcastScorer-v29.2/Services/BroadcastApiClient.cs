using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Diagnostics;
using System.Collections.Concurrent;
using MajsoulBroadcastScorer.Core.V27;

namespace MajsoulBroadcastScorer.V27.Services;

public sealed class BroadcastApiClient : IDisposable
{
    public static readonly Uri DefaultEndpoint = new("ws://127.0.0.1:7998");
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Uri _endpoint;
    private readonly string _controlToken;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ControlReply>> _pendingControls = new();
    private Task? _runTask;
    private ClientWebSocket? _socket;
    private BroadcastApiState _state = new();
    private int _disposed;
    private int _connected;
    private long _lastMessageTimestamp;
    private long _lastMessageUnixMs;

    public BroadcastApiClient(Uri? endpoint = null, string? controlToken = null)
    {
        _endpoint = endpoint ?? DefaultEndpoint;
        _controlToken = controlToken ?? string.Empty;
    }

    public event EventHandler<BroadcastApiState>? StateChanged;
    public event EventHandler<string>? StatusChanged;
    public event EventHandler<BroadcastAuthState>? AuthChanged;
    public bool IsConnected => Volatile.Read(ref _connected) != 0;
    public long LastMessageUnixMs => Interlocked.Read(ref _lastMessageUnixMs);

    public void Start()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        _runTask ??= Task.Run(() => RunAsync(_lifetime.Token));
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                OnStatus("正在连接本机观战服务…", false);
                using var socket = new ClientWebSocket();
                socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
                await socket.ConnectAsync(_endpoint, cancellationToken);
                Volatile.Write(ref _socket, socket);
                MarkMessageReceived();
                OnStatus("本机观战服务已连接，正在同步状态", true);
                await SendAsync(socket, new { type = "hello", client = "majsoul-live-v0.292", version = "0.292" }, cancellationToken);
                await SendAsync(socket, new { type = "subscribe", events = new[] { "auth", "observe" } }, cancellationToken);
                await SendAsync(socket, new { type = "snapshot" }, cancellationToken);

                using var connection = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var receiveTask = ReceiveLoopAsync(socket, connection.Token);
                var heartbeatTask = HeartbeatLoopAsync(socket, connection.Token);
                try
                {
                    var completed = await Task.WhenAny(receiveTask, heartbeatTask);
                    await completed;
                    throw new WebSocketException("观战服务连接已结束");
                }
                finally
                {
                    connection.Cancel();
                    try { await Task.WhenAll(receiveTask, heartbeatTask); }
                    catch (OperationCanceledException) when (connection.IsCancellationRequested) { }
                    catch when (!cancellationToken.IsCancellationRequested) { }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                OnStatus($"本机观战服务已断开，保留最后数据；3 秒后重连（{exception.Message}）", false);
            }

            Volatile.Write(ref _socket, null);
            FailPendingControls(new IOException("观战后台连接已断开"));

            try { await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
                throw new WebSocketException("服务端已关闭连接");
            if (result.MessageType != WebSocketMessageType.Text)
                continue;

            message.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage)
                continue;

            var json = Encoding.UTF8.GetString(message.GetBuffer(), 0, checked((int)message.Length));
            message.SetLength(0);
            MarkMessageReceived();
            if (TryCompleteControl(json))
                continue;
            Apply(BroadcastApiProtocol.Parse(json));
        }
    }

    private void Apply(BroadcastApiMessage message)
    {
        switch (message.Kind)
        {
            case BroadcastMessageKind.Snapshot:
                _state = new BroadcastApiState
                {
                    Mode = message.Mode ?? "idle",
                    Auth = message.Auth ?? _state.Auth,
                    Observe = message.Observe,
                    ServerTime = message.Time
                };
                OnStateChanged();
                AuthChanged?.Invoke(this, _state.Auth);
                break;
            case BroadcastMessageKind.Observe:
                _state = new BroadcastApiState
                {
                    Mode = _state.Mode,
                    Auth = _state.Auth,
                    Observe = message.Observe,
                    ServerTime = message.Time
                };
                OnStateChanged();
                break;
            case BroadcastMessageKind.Auth:
                _state = new BroadcastApiState
                {
                    Mode = _state.Mode,
                    Auth = message.Auth ?? new BroadcastAuthState(),
                    Observe = _state.Observe,
                    ServerTime = message.Time
                };
                OnStatus(_state.Auth.Connection == "connected"
                    ? "本机服务已连接；雀魂数据源已连接"
                    : "本机服务已连接；雀魂数据源已断开，保留最后数据", true);
                AuthChanged?.Invoke(this, _state.Auth);
                break;
            case BroadcastMessageKind.Error:
                OnStatus($"观战 API 错误：{message.ErrorMessage}", true);
                break;
        }
    }

    private async Task HeartbeatLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(25), cancellationToken);
            if (Stopwatch.GetElapsedTime(Interlocked.Read(ref _lastMessageTimestamp)) > TimeSpan.FromSeconds(40))
                throw new TimeoutException("超过 40 秒未收到观战服务响应");
            await SendAsync(socket, new { type = "ping" }, cancellationToken);
        }
    }

    public async Task<ControlReply> SendControlAsync(
        string command,
        object? data = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("控制命令不能为空。", nameof(command));
        var socket = Volatile.Read(ref _socket);
        if (socket is null || socket.State != WebSocketState.Open)
            throw new InvalidOperationException("观战后台尚未连接。");

        var requestId = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<ControlReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pendingControls.TryAdd(requestId, completion))
            throw new InvalidOperationException("无法创建控制请求。");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            await SendAsync(socket, new
            {
                type = "control",
                requestId,
                token = _controlToken,
                command,
                data = data ?? new { }
            }, timeout.Token);
            return await completion.Task.WaitAsync(timeout.Token);
        }
        finally
        {
            _pendingControls.TryRemove(requestId, out _);
        }
    }

    private bool TryCompleteControl(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var type) || type.GetString() != "control-result")
                return false;
            var requestId = root.TryGetProperty("requestId", out var id) ? id.GetString() : null;
            if (requestId is null || !_pendingControls.TryRemove(requestId, out var completion))
                return true;
            var ok = root.TryGetProperty("ok", out var okValue) && okValue.ValueKind == JsonValueKind.True;
            JsonElement? data = root.TryGetProperty("data", out var dataValue) && dataValue.ValueKind != JsonValueKind.Null
                ? dataValue.Clone()
                : null;
            var error = root.TryGetProperty("error", out var errorValue) && errorValue.ValueKind == JsonValueKind.String
                ? errorValue.GetString()
                : null;
            completion.TrySetResult(new ControlReply(ok, data, error));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private void FailPendingControls(Exception exception)
    {
        foreach (var request in _pendingControls.ToArray())
            if (_pendingControls.TryRemove(request.Key, out var completion))
                completion.TrySetException(exception);
    }

    private async Task SendAsync(ClientWebSocket socket, object value, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value));
        await _sendLock.WaitAsync(cancellationToken);
        try { await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken); }
        finally { _sendLock.Release(); }
    }

    private void OnStateChanged() => StateChanged?.Invoke(this, _state);
    private void MarkMessageReceived()
    {
        Interlocked.Exchange(ref _lastMessageTimestamp, Stopwatch.GetTimestamp());
        Interlocked.Exchange(ref _lastMessageUnixMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    private void OnStatus(string status, bool connected)
    {
        Volatile.Write(ref _connected, connected ? 1 : 0);
        StatusChanged?.Invoke(this, status);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _lifetime.Cancel();
        FailPendingControls(new ObjectDisposedException(nameof(BroadcastApiClient)));
        try { _runTask?.Wait(TimeSpan.FromSeconds(2)); } catch (AggregateException) { }
        _sendLock.Dispose();
        _lifetime.Dispose();
    }
}

public sealed record ControlReply(bool Ok, JsonElement? Data, string? Error);
