using System.Text.Json;
using MajsoulBroadcastScorer.Core.V27;

namespace MajsoulBroadcastScorer.Tests.V27;

public sealed class MatchRoundReportRecorderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "majsoul-round-audit", Guid.NewGuid().ToString("N"));
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RepeatedSettlementDoesNotDuplicateRecords()
    {
        using var recorder = new MatchRoundReportRecorder(_directory);
        Assert.True(recorder.Observe(State(), Now));
        Assert.False(recorder.Observe(State(), Now.AddSeconds(1)));
        Assert.Equal(1, recorder.Count);
        Assert.Single(Directory.GetFiles(_directory, "*.json"));
    }

    [Fact]
    public void FailedWriteRetriesWhenTheSameSettlementArrivesAgain()
    {
        Directory.CreateDirectory(_directory);
        var reports = Path.Combine(_directory, "reports");
        File.WriteAllText(reports, "temporary fixture blocks the report directory");
        using var recorder = new MatchRoundReportRecorder(reports);
        Assert.ThrowsAny<IOException>(() => recorder.Observe(State(), Now));
        File.Delete(reports);
        Directory.CreateDirectory(reports);
        Assert.True(recorder.Observe(State(), Now.AddSeconds(1)));
        Assert.Single(Directory.GetFiles(reports, "*.json"));
        Assert.Single(Directory.GetFiles(reports, "*.csv"));
        Assert.Single(Directory.GetFiles(reports, "*.txt"));
    }

    [Fact]
    public void LaterPlayerNamesRefreshExistingReport()
    {
        using var recorder = new MatchRoundReportRecorder(_directory);
        recorder.Observe(State(string.Empty), Now);
        Assert.True(recorder.Observe(State("补齐后的选手"), Now.AddSeconds(1)));
        Assert.Contains("补齐后的选手", File.ReadAllText(Directory.GetFiles(_directory, "*.txt").Single()));
        Assert.Equal(1, recorder.Count);
    }

    [Fact]
    public void InvalidBatchDoesNotPartlyAcceptEarlierValidRecord()
    {
        using var recorder = new MatchRoundReportRecorder(_directory);
        var valid = State();
        var state = new BroadcastObserveState
        {
            GameUuid = valid.GameUuid,
            Players = valid.Players,
            RoundResults = [valid.RoundResults[0], JsonSerializer.SerializeToElement(new { type = "hule" })]
        };
        Assert.Throws<InvalidDataException>(() => recorder.Observe(state, Now));
        Assert.Equal(0, recorder.Count);
    }

    [Fact]
    public void CsvKeepsUntrustedPlayerNamesAsText()
    {
        var state = State("=1+1");
        var players = state.Players.Select(player => new RoundReportPlayer(player.Seat, player.AccountId, player.Nickname)).ToArray();
        var report = new DetailedMatchReport(state.GameUuid, Now, Now, false, "fixture", players, state.RoundResults);
        var csv = RoundResultFormatter.BuildCsv(report);
        Assert.Contains("'=1+1", csv);
    }

    private static BroadcastObserveState State(string name = "测试选手") => new()
    {
        GameUuid = "audit-game",
        RoundResultSchema = 1,
        Players = Enumerable.Range(0, 4).Select(seat => new BroadcastPlayerState
        {
            Seat = seat, Wind = seat, AccountId = seat + 1,
            Nickname = seat == 0 ? name : "选手" + (seat + 1), Score = 25000
        }).ToArray(),
        RoundResults = [JsonSerializer.SerializeToElement(new
        {
            key = "audit-game:0:0:0", chang = 0, ju = 0, ben = 0, type = "hule",
            timestamp = Now.ToUnixTimeMilliseconds(),
            winners = new[] { new { seat = 0, fromSeat = 1, isTsumo = false,
                tiles = new[] { "1m", "2m", "3m" }, melds = Array.Empty<string>(), winTile = "3m",
                han = 1, fu = 30, points = 1000, pointRon = 1000,
                fans = new[] { new { id = 1, name = "测试役种", value = 1 } } } },
            deltaScores = new[] { 1000, -1000, 0, 0 }, scores = new[] { 26000, 24000, 25000, 25000 }
        })]
    };

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
