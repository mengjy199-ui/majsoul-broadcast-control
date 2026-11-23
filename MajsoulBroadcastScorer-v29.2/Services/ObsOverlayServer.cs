using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.IO;
namespace MajsoulBroadcastScorer.V27.Services;

public sealed record ObsAssetLayout(bool HasImage, long Version, double X, double Y, double Width, double Height);

public sealed record ObsPlayerState(
    int Seat,
    string Wind,
    string Nickname,
    string TeamName,
    int Score,
    decimal StartPoints,
    decimal GamePoints,
    decimal TotalPoints,
    bool Riichi,
    bool TransparentAvatar,
    bool HasAvatar,
    long AvatarVersion,
    string AvatarFrameMode,
    bool HasAvatarFrame,
    long AvatarFrameVersion)
{
    public decimal? SanmaCumulative { get; init; }
    public decimal? SanmaThisGame { get; init; }
    public decimal? FrierenFourCumulative { get; init; }
}

public sealed record ObsReversalPlayerState(
    int Seat,
    int Rank,
    decimal CurrentPoints,
    decimal ProjectedTotalPoints,
    decimal GapToTarget,
    int? TargetSeat,
    string? DirectRon,
    string? OtherRon,
    string? Tsumo,
    bool SixYakumanInsufficient);

public sealed record ObsOpponentThresholdState(
    int OpponentSeat,
    string DropWarning,
    string RonToTarget);

public sealed record ObsSelectedReversalState(
    int Seat,
    int Rank,
    decimal StartPoints,
    decimal CurrentPoints,
    decimal ProjectedTotalPoints,
    decimal GapToTarget,
    int? TargetSeat,
    string TargetLabel,
    string? DirectRon,
    string? OtherRon,
    string? Tsumo,
    bool SixYakumanInsufficient,
    IReadOnlyList<ObsOpponentThresholdState> OpponentThresholds);

public sealed record ObsOverlayState(
    string LayoutStyle,
    string Department,
    string MatchText,
    string RoundLabel,
    int Honba,
    int RiichiSticks,
    bool ShowTotalPoints,
    bool HasLeftBoard,
    long LeftBoardVersion,
    double LeftBoardFocusX,
    double LeftBoardFocusY,
    ObsAssetLayout Logo,
    ObsAssetLayout BottomLeft,
    int GapSelectedSeat,
    IReadOnlyList<ObsPlayerState> Players,
    IReadOnlyList<ObsReversalPlayerState> Reversals,
    ObsSelectedReversalState? SelectedReversal)
{
    public long Revision { get; init; }
    public string VerticalNameMode { get; init; } = "nickname";
    public string VerticalNumberMode { get; init; } = "totalPoints";
    public string SanmaNumberMode { get; init; } = "score";
    public string FrierenFourNumberMode { get; init; } = "score";
    public int SanmaPlayerGap { get; init; } = 16;
}

public sealed class ObsOverlayServer : IDisposable
{
    public const int DefaultPort = 17657;
    public static string OverlayUrl => $"http://127.0.0.1:{DefaultPort}/overlay";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Dictionary<string, CachedAsset> _assetCache = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, AssetBytes> _assets = new Dictionary<string, AssetBytes>();
    private string _stateJson = "{}";
    private string _previewStateJson = "{}";
    private long _revision;
    private Task? _loopTask;
    private int _disposed;

    public ObsOverlayServer(int port = DefaultPort) =>
        _listener = new TcpListener(IPAddress.Loopback, port);

    public void Start()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (_loopTask is not null)
            return;
        _listener.Start();
        _loopTask = Task.Run(() => RunAsync(_lifetime.Token));
    }

    public void Update(ObsOverlayState state, IReadOnlyDictionary<string, string?> assetPaths)
        => Update(state, state, assetPaths);

    public void Update(
        ObsOverlayState publishedState,
        ObsOverlayState previewState,
        IReadOnlyDictionary<string, string?> assetPaths)
    {
        var assets = new Dictionary<string, AssetBytes>(StringComparer.OrdinalIgnoreCase);
        var activeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in assetPaths)
        {
            if (string.IsNullOrWhiteSpace(pair.Value) || !File.Exists(pair.Value))
                continue;
            var file = new FileInfo(pair.Value);
            activeKeys.Add(pair.Key);
            var stamp = new AssetStamp(file.FullName, file.Length, file.LastWriteTimeUtc.Ticks);
            if (!_assetCache.TryGetValue(pair.Key, out var cached) || cached.Stamp != stamp)
            {
                cached = new CachedAsset(
                    stamp,
                    new AssetBytes(File.ReadAllBytes(file.FullName), ContentType(file.Extension), stamp.LastWriteTicks));
                _assetCache[pair.Key] = cached;
            }
            assets[pair.Key] = cached.Bytes;
        }

        foreach (var staleKey in _assetCache.Keys.Where(key => !activeKeys.Contains(key)).ToArray())
            _assetCache.Remove(staleKey);

        Volatile.Write(ref _assets, assets);
        var published = publishedState with { Revision = Interlocked.Increment(ref _revision) };
        var preview = previewState with { Revision = Interlocked.Increment(ref _revision) };
        Volatile.Write(ref _stateJson, JsonSerializer.Serialize(published, JsonOptions));
        Volatile.Write(ref _previewStateJson, JsonSerializer.Serialize(preview, JsonOptions));
    }

    private static string ContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        ".woff2" => "font/woff2",
        _ => "application/octet-stream"
    };

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _listener.AcceptTcpClientAsync(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            catch (SocketException) when (cancellationToken.IsCancellationRequested) { return; }
            _ = HandleAsync(client, cancellationToken);
        }
    }

    private async Task HandleAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            try
            {
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                var requestLine = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(requestLine))
                    return;
                while (await reader.ReadLineAsync(cancellationToken) is { Length: > 0 }) { }

                var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var path = parts.Length >= 2 ? parts[1].Split('?', 2)[0] : string.Empty;
                if (path is "/" or "/overlay")
                {
                    await WriteTextAsync(stream, "200 OK", "text/html; charset=utf-8", ObsOverlayPage.Html, cancellationToken);
                    return;
                }
                if (path == "/api/state")
                {
                    await WriteTextAsync(stream, "200 OK", "application/json; charset=utf-8", Volatile.Read(ref _stateJson), cancellationToken);
                    return;
                }
                if (path == "/api/preview-state")
                {
                    await WriteTextAsync(stream, "200 OK", "application/json; charset=utf-8", Volatile.Read(ref _previewStateJson), cancellationToken);
                    return;
                }
                const string prefix = "/asset/";
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var key = WebUtility.UrlDecode(path[prefix.Length..]);
                    if (Volatile.Read(ref _assets).TryGetValue(key, out var asset))
                        await WriteBinaryAsync(stream, "200 OK", asset.ContentType, asset.Bytes, cancellationToken);
                    else
                        await WriteTextAsync(stream, "404 Not Found", "text/plain; charset=utf-8", "Not Found", cancellationToken);
                    return;
                }
                await WriteTextAsync(stream, "404 Not Found", "text/plain; charset=utf-8", "Not Found", cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or SocketException or OperationCanceledException)
            {
            }
        }
    }

    private static async Task WriteTextAsync(Stream stream, string status, string contentType, string content, CancellationToken token) =>
        await WriteBinaryAsync(stream, status, contentType, Encoding.UTF8.GetBytes(content), token);

    private static async Task WriteBinaryAsync(Stream stream, string status, string contentType, byte[] body, CancellationToken token)
    {
        var header = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {status}\r\nContent-Type: {contentType}\r\nContent-Length: {body.Length}\r\n" +
            "Cache-Control: no-store, no-cache, must-revalidate\r\nAccess-Control-Allow-Origin: *\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header, token);
        await stream.WriteAsync(body, token);
        await stream.FlushAsync(token);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _lifetime.Cancel();
        _listener.Stop();
        try { _loopTask?.Wait(TimeSpan.FromSeconds(2)); } catch (AggregateException) { }
        _lifetime.Dispose();
    }

    private sealed record AssetBytes(byte[] Bytes, string ContentType, long Version);
    private sealed record AssetStamp(string Path, long Length, long LastWriteTicks);
    private sealed record CachedAsset(AssetStamp Stamp, AssetBytes Bytes);
}
