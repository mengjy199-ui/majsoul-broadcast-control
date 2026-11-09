using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace MajsoulBroadcastScorer.V27.Services;

public sealed class BackendProcessManager : IDisposable
{
    private readonly string _backendDirectory;
    private Process? _process;
    private int _disposed;

    public BackendProcessManager(string baseDirectory)
    {
        _backendDirectory = Path.Combine(Path.GetFullPath(baseDirectory), "Backend");
        ControlToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }

    public string ControlToken { get; }
    public bool OwnsProcess => _process is { HasExited: false };
    public string Status { get; private set; } = "后台尚未启动";

    public void Start()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (IsPortInUse(7998))
        {
            Status = "7998 已由其他程序占用；可读取数据，统一控制不可用";
            return;
        }

        var executable = Path.Combine(_backendDirectory, "maj-live-control-center.exe");
        if (!File.Exists(executable))
        {
            Status = "未找到随程序附带的观战后台";
            return;
        }

        var startInfo = new ProcessStartInfo(executable)
        {
            WorkingDirectory = _backendDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        startInfo.Environment["MAJSOUL_V29_CONTROL_TOKEN"] = ControlToken;
        _process = Process.Start(startInfo);
        Status = _process is null ? "观战后台启动失败" : "观战后台已启动";
    }

    private static bool IsPortInUse(int port)
    {
        try
        {
            using var client = new TcpClient();
            var connect = client.ConnectAsync(IPAddress.Loopback, port);
            return connect.Wait(TimeSpan.FromMilliseconds(150)) && client.Connected;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        var process = _process;
        if (process is null)
            return;
        try
        {
            if (!process.WaitForExit(1500))
                process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
        finally
        {
            process.Dispose();
        }
    }
}
