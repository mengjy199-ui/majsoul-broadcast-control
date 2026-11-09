using MajsoulBroadcastScorer.Core.V27;

namespace MajsoulBroadcastScorer.Tests.V27;

public sealed class ReversalConditionCalculatorTests
{
    [Fact]
    public void Calculate_UsesHonbaAndRiichiSticksInDirectRon()
    {
        var result = ReversalConditionCalculator.Calculate(Players(
            (30_000, 0m), (28_000, 0m), (22_000, 0m), (19_000, 0m)), 2, 1);

        var second = result.Single(item => item.Seat == 1);
        Assert.Equal(0, second.TargetSeat);
        Assert.NotNull(second.DirectRon);
        Assert.Equal("1600", second.DirectRon!.Display);
    }

    [Fact]
    public void Calculate_DoesNotSubtractRiichiDepositTwice()
    {
        var result = ReversalConditionCalculator.Calculate(Players(
            (30_000, 0m), (27_000, 0m), (22_000, 0m), (20_000, 0m)), 0, 1);

        Assert.Equal(0m, result.Sum(item => item.CurrentPoints));
    }

    [Fact]
    public void Calculate_DistinguishesDealerAndNonDealerTsumoPayments()
    {
        var result = ReversalConditionCalculator.Calculate(Players(
            (10_000, 0m), (40_000, 0m), (30_000, 0m), (20_000, 0m)), 0, 0);

        Assert.EndsWith(" all", result.Single(item => item.Seat == 0).Tsumo?.Display ?? string.Empty);
        Assert.DoesNotContain("+", result.Single(item => item.Seat == 0).Tsumo?.Display ?? string.Empty);
        Assert.Contains('/', result.Single(item => item.Seat == 2).Tsumo?.Display ?? string.Empty);
    }

    [Fact]
    public void AnalyzeSelected_CanChoosePreviousRankOrFirstPlace()
    {
        var players = Players((25_000, 100m), (25_000, 50m), (25_000, 0m), (25_000, -50m));

        var previous = ReversalConditionCalculator.AnalyzeSelected(
            players, 0, 0, 3, ReversalTargetMode.PreviousRank);
        var first = ReversalConditionCalculator.AnalyzeSelected(
            players, 0, 0, 3, ReversalTargetMode.FirstPlace);

        Assert.Equal(2, previous.TargetSeat);
        Assert.Equal(0, first.TargetSeat);
        Assert.True(first.GapToTarget > previous.GapToTarget);
    }

    [Fact]
    public void AnalyzeSelected_TopTwoTargetsSecondPlace()
    {
        var players = Players((25_000, 100m), (25_000, 50m), (25_000, 0m), (25_000, -50m));

        var result = ReversalConditionCalculator.AnalyzeSelected(
            players, 0, 0, 3, ReversalTargetMode.TopTwo);

        Assert.Equal(1, result.TargetSeat);
        Assert.Equal(4, result.Rank);
    }

    [Fact]
    public void AnalyzeSelected_TopTwoAcceptsRonThatDropsAnotherPlayerBelowSelected()
    {
        var players = new[]
        {
            new ReversalPlayerInput(0, 1, 23_000, 20m),
            new ReversalPlayerInput(1, 2, 23_000, -5.2m),
            new ReversalPlayerInput(2, 3, 20_900, -6.9m),
            new ReversalPlayerInput(3, 0, 33_100, -7.9m)
        };

        var result = ReversalConditionCalculator.AnalyzeSelected(
            players, 0, 0, 2, ReversalTargetMode.TopTwo);

        var ronFromCurrentLeader = result.OpponentThresholds.Single(item => item.OpponentSeat == 0);
        Assert.Equal("5200", ronFromCurrentLeader.RonToTarget?.Display);
    }

    [Fact]
    public void AnalyzeSelected_TopTwoAlreadyMetKeepsDropWarnings()
    {
        var result = ReversalConditionCalculator.AnalyzeSelected(
            Players((30_000, 0m), (28_000, 0m), (22_000, 0m), (20_000, 0m)),
            0, 0, 1, ReversalTargetMode.TopTwo);

        Assert.Equal(2, result.Rank);
        Assert.Null(result.TargetSeat);
        Assert.Equal(3, result.OpponentThresholds.Count);
        Assert.Contains(result.OpponentThresholds, row => row.DropByRon is not null);
    }

    [Fact]
    public void AnalyzeSelected_LeaderShowsDropThresholdForEveryOpponent()
    {
        var result = ReversalConditionCalculator.AnalyzeSelected(
            Players((30_000, 0m), (28_000, 0m), (22_000, 0m), (20_000, 0m)),
            2,
            1,
            0,
            ReversalTargetMode.FirstPlace);

        Assert.Equal(1, result.Rank);
        Assert.Null(result.TargetSeat);
        Assert.Equal(3, result.DefenseThresholds.Count);
        Assert.All(result.DefenseThresholds, item => Assert.True(
            item.SixYakumanSafe || item.RonFromSelected is not null || item.Tsumo is not null));
        Assert.Equal(0, result.DefenseThresholds[0].RonFromSelected?.DiscarderSeat);
        Assert.Equal(3, result.OpponentThresholds.Count);
    }

    [Fact]
    public void AnalyzeSelected_DealerTsumoUsesAllLabel()
    {
        var result = ReversalConditionCalculator.AnalyzeSelected(
            Players((10_000, 0m), (40_000, 0m), (30_000, 0m), (20_000, 0m)),
            0,
            0,
            0,
            ReversalTargetMode.FirstPlace);

        Assert.Contains(" all", result.Tsumo?.Display ?? string.Empty);
        Assert.DoesNotContain("+", result.Tsumo?.Display ?? string.Empty);
        Assert.DoesNotContain("全家", result.Tsumo?.Display ?? string.Empty);
    }

    [Fact]
    public void AnalyzeSelected_NonLeaderBuildsThreeOpponentSpecificRows()
    {
        var result = ReversalConditionCalculator.AnalyzeSelected(
            Players((30_000, 0m), (29_000, 0m), (21_000, 0m), (20_000, 0m)),
            1,
            1,
            1,
            ReversalTargetMode.FirstPlace);

        Assert.Equal(2, result.Rank);
        Assert.Equal(3, result.OpponentThresholds.Count);
        Assert.All(result.OpponentThresholds, row =>
        {
            Assert.False(row.AlreadyFourth);
            Assert.Equal(row.OpponentSeat, row.RonToTarget?.DiscarderSeat);
            Assert.Equal(result.Tsumo?.Display, row.TsumoToTarget?.Display);
        });
        Assert.Contains(result.OpponentThresholds, row => row.DropByRon is not null);
    }

    [Fact]
    public void AnalyzeSelected_FourthPlaceDoesNotInventAnotherDropRank()
    {
        var result = ReversalConditionCalculator.AnalyzeSelected(
            Players((40_000, 0m), (30_000, 0m), (20_000, 0m), (10_000, 0m)),
            0,
            0,
            3,
            ReversalTargetMode.PreviousRank);

        Assert.Equal(4, result.Rank);
        Assert.All(result.OpponentThresholds, row =>
        {
            Assert.True(row.AlreadyFourth);
            Assert.Null(row.DropByRon);
            Assert.False(row.SixYakumanDropSafe);
        });
    }

    [Fact]
    public void Calculate_CanFindDoubleYakumanInsteadOfReturningNoCondition()
    {
        var result = ReversalConditionCalculator.Calculate(Players(
            (25_000, 0m), (25_000, 0m), (25_000, 0m), (25_000, -100m)), 0, 0);

        var last = result.Single(item => item.Seat == 3);
        Assert.True(last.DirectRon?.YakumanMultiplier >= 2 ||
                    last.OtherRon?.YakumanMultiplier >= 2 ||
                    last.Tsumo?.YakumanMultiplier >= 2);
        Assert.False(last.SixYakumanInsufficient);
    }

    [Fact]
    public void Calculate_ReportsWhenSixYakumanStillCannotOvertakePreviousPointsGap()
    {
        var players = Players((25_000, 1_500m), (25_000, 1_000m), (25_000, 500m), (25_000, 0m));

        var result = ReversalConditionCalculator.Calculate(players, 0, 0);

        Assert.True(result.Single(item => item.Seat == 3).SixYakumanInsufficient);
    }

    [Fact]
    public void Calculate_CanSelectExactlySixYakumanAtTheBoundary()
    {
        var result = ReversalConditionCalculator.Calculate(Players(
            (25_000, 0m), (25_000, 0m), (25_000, 0m), (25_000, -360m)), 0, 0);

        var last = result.Single(item => item.Seat == 3);
        Assert.Equal(6, last.DirectRon?.YakumanMultiplier);
        Assert.Equal("六倍役满", last.DirectRon?.Display);
        Assert.False(last.SixYakumanInsufficient);
    }

    [Fact]
    public void Calculate_RecomputesUmaAfterRankCrossingAndBreaksScoreTiesBySeat()
    {
        var result = ReversalConditionCalculator.Calculate(Players(
            (25_000, 0m), (25_000, 0m), (25_000, 0m), (25_000, 0m)), 0, 0);

        Assert.Equal([0, 1, 2, 3], result.OrderBy(item => item.Rank).Select(item => item.Seat));
        Assert.Equal([15m, 5m, -5m, -15m], result.OrderBy(item => item.Seat).Select(item => item.CurrentPoints));
    }

    private static ReversalPlayerInput[] Players(params (int Score, decimal Previous)[] values) =>
        values.Select((value, seat) => new ReversalPlayerInput(seat, seat, value.Score, value.Previous)).ToArray();
}
