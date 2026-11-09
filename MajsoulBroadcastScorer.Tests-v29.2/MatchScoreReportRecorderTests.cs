using System.Text;
using System.Text.Json;
using MajsoulBroadcastScorer.Core.V27;

namespace MajsoulBroadcastScorer.Tests.V27;

public sealed class MatchScoreReportRecorderTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "majsoul-v29-report-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Observe_WritesInitialAndScoreChangesWithoutDuplicatingSnapshots()
    {
        using var recorder = new MatchScoreReportRecorder(_directory);
        var started = Display("game-a", [25000, 25000, 25000, 25000]);
        var changed = Display("game-a", [24000, 26000, 25000, 25000], "东1局");

        Assert.True(recorder.Observe(started, "API", Time(0)));
        Assert.False(recorder.Observe(started, "API", Time(1)));
        Assert.True(recorder.Observe(changed, "API", Time(2)));

        Assert.Equal(2, recorder.CurrentEntryCount);
        Assert.NotNull(recorder.LastJsonPath);
        var report = JsonSerializer.Deserialize<MatchScoreReport>(File.ReadAllText(recorder.LastJsonPath!),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(report);
        Assert.Equal(["首次记录", "分数变化"], report!.Entries.Select(entry => entry.Event));
        var csv = File.ReadAllText(recorder.LastCsvPath!, Encoding.UTF8);
        Assert.Contains("+1000", csv);
        Assert.Contains("-1000", csv);
    }

    [Fact]
    public void Observe_NewGameArchivesPreviousAndStartsNewReport()
    {
        using var recorder = new MatchScoreReportRecorder(_directory);

        recorder.Observe(Display("game-a", [25000, 25000, 25000, 25000]), "API", Time(0));
        recorder.Observe(Display("game-b", [30000, 25000, 25000, 20000]), "API", Time(5));

        var jsonFiles = Directory.GetFiles(_directory, "*.json");
        Assert.Equal(2, jsonFiles.Length);
        var first = jsonFiles.Select(ReadReport).Single(report => report.GameUuid == "game-a");
        Assert.True(first.Completed);
        Assert.Equal("检测到新对局", first.CompletionReason);
        Assert.Equal("game-b", recorder.CurrentGameUuid);
    }

    [Fact]
    public void Observe_EndedGameFinalizesOnceAndIgnoresRepeatedFinalSnapshot()
    {
        using var recorder = new MatchScoreReportRecorder(_directory);
        var final = Display("game-final", [40000, 30000, 20000, 10000], "南4局");

        Assert.True(recorder.Observe(final, "API", Time(0), ended: true));
        Assert.False(recorder.Observe(final, "API", Time(1), ended: true));

        Assert.Equal(string.Empty, recorder.CurrentGameUuid);
        Assert.Single(Directory.GetFiles(_directory, "*.json"));
        var report = ReadReport(Directory.GetFiles(_directory, "*.json").Single());
        Assert.True(report.Completed);
        Assert.Equal("对局结束", report.CompletionReason);
    }

    [Fact]
    public void Observe_ManualScoreCorrectionIsExplicitlyRecorded()
    {
        using var recorder = new MatchScoreReportRecorder(_directory);
        recorder.Observe(Display("game-manual", [25000, 25000, 25000, 25000]), "API", Time(0));

        recorder.Observe(Display("game-manual", [25100, 24900, 25000, 25000]), "人工确认", Time(1));

        var report = ReadReport(recorder.LastJsonPath!);
        Assert.Equal("人工修正", report.Entries[^1].Event);
    }

    private static LiveDisplayState Display(string gameUuid, int[] scores, string round = "东1局") => new(
        true,
        true,
        "API 已连接，正在观战",
        gameUuid,
        round,
        0,
        0,
        0,
        true,
        scores.Select((score, seat) => new DisplayPlayerState(
            seat,
            seat,
            $"P{seat}",
            score,
            0,
            false,
            AvatarDisplayMode.Image,
            string.Empty,
            AvatarFrameDisplayMode.Default,
            string.Empty,
            $"Team{seat}")).ToArray());

    private static DateTimeOffset Time(int minutes) =>
        new(2026, 9, 22, 12, minutes, 0, TimeSpan.FromHours(8));

    private static MatchScoreReport ReadReport(string path) =>
        JsonSerializer.Deserialize<MatchScoreReport>(
            File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }
}
