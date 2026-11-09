using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using MajsoulBroadcastScorer.V27.Services;

namespace MajsoulBroadcastScorer.Tests.V27;

public sealed class ObsOverlayServerTests
{
    [Fact]
    public async Task PublishedAndPreviewEndpoints_ReturnIndependentStyles()
    {
        var port = ReservePort();
        using var server = new ObsOverlayServer(port);
        server.Start();
        server.Update(
            State("verticalAvatar"),
            State("gapConditions") with { VerticalNameMode = "teamName", VerticalNumberMode = "score" },
            new Dictionary<string, string?>());

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var published = JsonDocument.Parse(await client.GetStringAsync($"http://127.0.0.1:{port}/api/state"));
        var preview = JsonDocument.Parse(await client.GetStringAsync($"http://127.0.0.1:{port}/api/preview-state"));

        Assert.Equal("verticalAvatar", published.RootElement.GetProperty("layoutStyle").GetString());
        Assert.Equal("gapConditions", preview.RootElement.GetProperty("layoutStyle").GetString());
        Assert.Equal("nickname", published.RootElement.GetProperty("verticalNameMode").GetString());
        Assert.Equal("teamName", preview.RootElement.GetProperty("verticalNameMode").GetString());
        Assert.Equal("score", preview.RootElement.GetProperty("verticalNumberMode").GetString());
    }

    private static int ReservePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static ObsOverlayState State(string style) => new(
        style,
        string.Empty,
        string.Empty,
        string.Empty,
        0,
        0,
        true,
        false,
        0,
        50,
        50,
        new ObsAssetLayout(false, 0, 0, 0, 0, 0),
        new ObsAssetLayout(false, 0, 0, 0, 0, 0),
        0,
        [],
        [],
        null);
}
