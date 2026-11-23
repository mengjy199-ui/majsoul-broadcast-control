using MajsoulBroadcastScorer.Core.V27;

namespace MajsoulBroadcastScorer.Tests.V27;

public sealed class SanmaScoreTests
{
    [Fact]
    public void FinalDeltasFollowAccountsAcrossSeatChangesWithoutRecounting()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(directory.FullName, "totals.json");
            var ledger = new ScoreDeltaLedger(path, 3);
            var first = Game("first", [101, 102, 103], [30000, 41200, 33800]);
            Assert.True(ledger.Record("contest-a", first));
            Assert.False(ledger.Record("contest-a", first));
            Assert.Equal(-5m, ledger.Total("contest-a", 101, null));
            Assert.Equal(0m, ledger.Total("contest-a", 101, "first"));

            var reopened = new ScoreDeltaLedger(path, 3);
            Assert.False(reopened.Record("contest-a", first));
            Assert.Equal(2.3m, reopened.CurrentGamePoints(37300));
            Assert.Equal(-5m, reopened.CurrentGamePoints(30000));
            Assert.Equal(-5m, reopened.Total("contest-a", 101, null));

            var second = Game("second", [102, 103, 101], [34700, 33000, 37300]);
            Assert.True(reopened.Record("contest-a", second));
            Assert.Equal(-5m, reopened.Total("contest-a", 101, "second"));
            Assert.False(reopened.Record("contest-a", second));
            Assert.Equal(-2.7m, reopened.Total("contest-a", 101, null));
            Assert.Equal(0m, reopened.Total("contest-b", 101, null));
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void InvalidOrUnfinishedGameDoesNotEnterLedger()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(directory.FullName, "totals.json");
            var ledger = new ScoreDeltaLedger(path, 3);
            Assert.False(ledger.Record("contest", Game("playing", [101, 102, 103], [30000, 40000, 35000], ended: false)));
            Assert.False(ledger.Record("contest", Game("incomplete", [101, 102, 103], [30000, 40000, 34000])));
            Assert.False(ledger.Record("contest", Game("missing-id", [101, 102, 0], [30000, 40000, 35000])));
            Assert.False(File.Exists(path));
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void ManualPriorTotalSurvivesReloadAndAddsEachGameOnlyOnce()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(directory.FullName, "totals.json");
            var ledger = new ScoreDeltaLedger(path, 3);
            var settings = new V27Settings();
            Assert.True(ledger.Record("contest", Game("first", [101, 102, 103], [33000, 37000, 35000])));
            var recorded = ledger.Total("contest", 101, "second");
            settings.SetScoreDeltaPriorAdjustment(3, "contest", 101, 12m - recorded);
            settings = V27SettingsJson.Deserialize(V27SettingsJson.Serialize(settings));

            decimal Displayed(string game, int score) => ledger.ProjectedTotal(
                ledger.Total("contest", 101, game) + settings.ScoreDeltaPriorAdjustment(3, "contest", 101), score);

            Assert.Equal(7m, Displayed("second", 30000));
            Assert.Equal(9m, Displayed("second", 32000));
            var second = Game("second", [103, 101, 102], [38000, 32000, 35000]);
            Assert.True(ledger.Record("contest", second));
            Assert.False(ledger.Record("contest", second));
            Assert.Equal(9m, Displayed("second", 32000));
            Assert.Equal(9m, Displayed("third", 35000));
            Assert.Equal(0m, settings.ScoreDeltaPriorAdjustment(3, "other-contest", 101));
            Assert.Equal(0m, settings.ScoreDeltaPriorAdjustment(3, "contest", 102));
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void FourPlayerDeltaUses25000AndKeepsPriorTotalsAcrossSeatChangesAndReload()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(directory.FullName, "frieren-four-totals.json");
            var ledger = new ScoreDeltaLedger(path, 4);
            var settings = new V27Settings { FrierenFourShowCumulative = true };
            var first = Game("first", [101, 102, 103, 104], [28000, 22000, 25000, 25000]);
            Assert.False(ledger.Record("contest", Game("sanma", [101, 102, 103], [35000, 35000, 35000])));
            Assert.False(ledger.Record("contest", Game("incomplete", [101, 102, 103, 104], [25000, 25000, 25000, 24900])));
            Assert.True(ledger.Record("contest", first));
            settings.SetScoreDeltaPriorAdjustment(4, "contest", 101, 12m - ledger.Total("contest", 101, "second"));
            settings.SetScoreDeltaPriorAdjustment(3, "contest", 101, -6m);
            settings = V27SettingsJson.Deserialize(V27SettingsJson.Serialize(settings));

            decimal Displayed(string game, int score) => ledger.ProjectedTotal(
                ledger.Total("contest", 101, game) + settings.ScoreDeltaPriorAdjustment(4, "contest", 101), score);

            Assert.True(settings.FrierenFourShowCumulative);
            Assert.Equal(-6m, settings.ScoreDeltaPriorAdjustment(3, "contest", 101));
            Assert.Equal(7m, Displayed("second", 20000));
            Assert.Equal(9.4m, Displayed("second", 22400));
            var second = Game("second", [104, 103, 101, 102], [25000, 30000, 20000, 25000]);
            Assert.True(ledger.Record("contest", second));
            ledger = new ScoreDeltaLedger(path, 4);
            Assert.False(ledger.Record("contest", second));
            Assert.Equal(7m, Displayed("second", 20000));
            Assert.Equal(7m, Displayed("third", 25000));
            Assert.Equal(0m, ledger.Total("other-contest", 101, null));
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void ThreeSeatsUseFinalScoresAndMoveCustomTeamsWithThePlayer()
    {
        var settings = new V27Settings();
        settings.Players[3].AccountId = 101;
        settings.Players[3].TeamName = "手工队名";
        settings.Players[3].TeamNameLocked = true;
        settings.Players[3].AvatarPath = "assets/player.png";
        var reducer = new LiveStateReducer(settings);
        Assert.True(reducer.Apply(new BroadcastApiState { Observe = Game("first", [101, 102, 103], [30000, 40000, 35000]) },
            bindPlayerAccounts: true));

        var display = reducer.Current;
        Assert.True(display.Ended);
        Assert.Equal([30000, 40000, 35000], display.Players.Select(player => player.Score));
        Assert.Equal("手工队名", display.Players[0].TeamName);
        Assert.Equal("assets/player.png", display.Players[0].AvatarPath);
        Assert.Equal("API 队名", display.Players[1].TeamName);
        Assert.Equal(0, settings.Players[3].AccountId);

        Assert.True(reducer.Apply(new BroadcastApiState
        {
            Observe = new()
            {
                GameUuid = "four",
                Players = Enumerable.Range(0, 4).Select(seat => new BroadcastPlayerState
                {
                    Seat = seat, AccountId = 101 + seat, Wind = seat, Score = 25000
                }).ToArray()
            }
        }, bindPlayerAccounts: true));
        Assert.Equal(4, reducer.Current.Players.Count);
        Assert.Equal("手工队名", reducer.Current.Players[0].TeamName);
    }

    private static BroadcastObserveState Game(string uuid, long[] accounts, int[] scores, bool ended = true) => new()
    {
        GameUuid = uuid,
        Ended = ended,
        Round = new() { Chang = 1, Ju = 2 },
        Players = accounts.Select((id, seat) => new BroadcastPlayerState
        {
            Seat = seat, AccountId = id, Wind = seat, TeamName = "API 队名", Score = accounts.Length == 3 ? 35000 : 25000
        }).ToArray(),
        FinalRanking = scores.Select((score, seat) => (score, seat)).OrderByDescending(player => player.score)
            .Select((player, index) => new BroadcastFinalRankingState
            {
                Rank = index + 1, Seat = player.seat, AccountId = accounts[player.seat], TotalPoint = player.score
            }).ToArray()
    };
}
