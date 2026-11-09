namespace MajsoulBroadcastScorer.Core.V27;

public enum ReversalScenarioKind
{
    DirectRon,
    OtherRon,
    Tsumo
}

public enum ReversalTargetMode
{
    PreviousRank,
    TopTwo,
    FirstPlace
}

public sealed record ReversalPlayerInput(
    int Seat,
    int Wind,
    int Score,
    decimal PreviousPoints);

public sealed record ReversalCondition(
    ReversalScenarioKind Kind,
    string Display,
    int BasePoints,
    int YakumanMultiplier,
    int? DiscarderSeat = null);

public sealed record ReversalPlayerResult(
    int Seat,
    int Rank,
    decimal CurrentPoints,
    decimal ProjectedTotalPoints,
    decimal GapToTarget,
    int? TargetSeat,
    ReversalCondition? DirectRon,
    ReversalCondition? OtherRon,
    ReversalCondition? Tsumo,
    bool SixYakumanInsufficient);

public sealed record DefenseThreshold(
    int OpponentSeat,
    ReversalCondition? RonFromSelected,
    ReversalCondition? Tsumo,
    bool SixYakumanSafe);

public sealed record OpponentReversalThreshold(
    int OpponentSeat,
    ReversalCondition? DropByRon,
    ReversalCondition? RonToTarget,
    ReversalCondition? TsumoToTarget,
    bool AlreadyFourth,
    bool SixYakumanDropSafe);

public sealed record SelectedPlayerReversalResult(
    int Seat,
    int Rank,
    decimal CurrentPoints,
    decimal ProjectedTotalPoints,
    decimal GapToTarget,
    int? TargetSeat,
    ReversalCondition? DirectRon,
    ReversalCondition? OtherRon,
    ReversalCondition? Tsumo,
    bool SixYakumanInsufficient,
    IReadOnlyList<DefenseThreshold> DefenseThresholds,
    IReadOnlyList<OpponentReversalThreshold> OpponentThresholds);

public static class ReversalConditionCalculator
{
    private const int StartingScore = 25_000;
    private const int YakumanBasePoints = 8_000;
    private static readonly decimal[] Uma = [15m, 5m, -5m, -15m];
    private static readonly WinTier[] WinTiers = CreateWinTiers();

    public static IReadOnlyList<ReversalPlayerResult> Calculate(
        IReadOnlyList<ReversalPlayerInput> players,
        int honba,
        int riichiSticks)
    {
        Validate(players, honba, riichiSticks);
        var bySeat = players.OrderBy(player => player.Seat).ToArray();
        var rawScores = bySeat.Select(player => player.Score).ToArray();

        // 雀魂终局仍有供托时归当前第一名；仅用于“如果现在结束”的实时精算。
        var baselineScores = (int[])rawScores.Clone();
        baselineScores[RankSeats(rawScores)[0]] += riichiSticks * 1_000;
        var baselineSettlements = CalculateSettlements(baselineScores);
        var projectedTotals = bySeat.ToDictionary(
            player => player.Seat,
            player => player.PreviousPoints + baselineSettlements[player.Seat].Points);
        var totalOrder = bySeat
            .OrderByDescending(player => projectedTotals[player.Seat])
            .ThenBy(player => player.Seat)
            .Select(player => player.Seat)
            .ToArray();

        var results = new List<ReversalPlayerResult>(4);
        for (var rankIndex = 0; rankIndex < totalOrder.Length; rankIndex++)
        {
            var seat = totalOrder[rankIndex];
            if (rankIndex == 0)
            {
                results.Add(new ReversalPlayerResult(
                    seat,
                    1,
                    baselineSettlements[seat].Points,
                    projectedTotals[seat],
                    0,
                    null,
                    null,
                    null,
                    null,
                    false));
                continue;
            }

            var targetSeat = totalOrder[rankIndex - 1];
            var direct = FindCondition(bySeat, rawScores, seat, targetSeat, targetSeat,
                ReversalScenarioKind.DirectRon, honba, riichiSticks);
            var other = Enumerable.Range(0, 4)
                .Where(discarder => discarder != seat && discarder != targetSeat)
                .Select(discarder => FindCondition(bySeat, rawScores, seat, targetSeat, discarder,
                    ReversalScenarioKind.OtherRon, honba, riichiSticks))
                .Where(condition => condition is not null)
                .OrderBy(condition => TierOrder(condition!))
                .FirstOrDefault();
            var tsumo = FindCondition(bySeat, rawScores, seat, targetSeat, null,
                ReversalScenarioKind.Tsumo, honba, riichiSticks);
            var insufficient = direct is null && other is null && tsumo is null;

            results.Add(new ReversalPlayerResult(
                seat,
                rankIndex + 1,
                baselineSettlements[seat].Points,
                projectedTotals[seat],
                projectedTotals[targetSeat] - projectedTotals[seat],
                targetSeat,
                direct,
                other,
                tsumo,
                insufficient));
        }

        return results.OrderBy(result => result.Rank).ToArray();
    }

    public static SelectedPlayerReversalResult AnalyzeSelected(
        IReadOnlyList<ReversalPlayerInput> players,
        int honba,
        int riichiSticks,
        int selectedSeat,
        ReversalTargetMode targetMode)
    {
        Validate(players, honba, riichiSticks);
        if (selectedSeat is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(selectedSeat));

        var bySeat = players.OrderBy(player => player.Seat).ToArray();
        var rawScores = bySeat.Select(player => player.Score).ToArray();
        var baselineScores = (int[])rawScores.Clone();
        baselineScores[RankSeats(rawScores)[0]] += riichiSticks * 1_000;
        var baselineSettlements = CalculateSettlements(baselineScores);
        var projectedTotals = bySeat.ToDictionary(
            player => player.Seat,
            player => player.PreviousPoints + baselineSettlements[player.Seat].Points);
        var totalOrder = bySeat
            .OrderByDescending(player => projectedTotals[player.Seat])
            .ThenBy(player => player.Seat)
            .Select(player => player.Seat)
            .ToArray();
        var rankIndex = Array.IndexOf(totalOrder, selectedSeat);

        var targetAlreadyMet = rankIndex == 0 ||
                               targetMode == ReversalTargetMode.TopTwo && rankIndex <= 1;
        if (targetAlreadyMet)
        {
            var protectedRank = targetMode == ReversalTargetMode.TopTwo ? 2 : 1;
            var targetMetThresholds = totalOrder.Where(seat => seat != selectedSeat).Select(opponentSeat =>
            {
                var drop = FindDropCondition(bySeat, rawScores, opponentSeat, selectedSeat, protectedRank,
                    honba, riichiSticks);
                return new OpponentReversalThreshold(
                    opponentSeat, drop, null, null, false, drop is null);
            }).ToArray();
            var defense = targetMetThresholds.Select(item => new DefenseThreshold(
                item.OpponentSeat, item.DropByRon, null, item.SixYakumanDropSafe)).ToArray();
            var comparisonSeat = rankIndex == 0 ? totalOrder[1] : totalOrder[0];
            return new SelectedPlayerReversalResult(
                selectedSeat, rankIndex + 1, baselineSettlements[selectedSeat].Points, projectedTotals[selectedSeat],
                Math.Abs(projectedTotals[selectedSeat] - projectedTotals[comparisonSeat]), null,
                null, null, null, false, defense, targetMetThresholds);
        }

        var targetSeat = targetMode switch
        {
            ReversalTargetMode.FirstPlace => totalOrder[0],
            ReversalTargetMode.TopTwo => totalOrder[1],
            _ => totalOrder[rankIndex - 1]
        };
        var requiredRank = targetMode == ReversalTargetMode.TopTwo ? 2 : (int?)null;
        var direct = FindCondition(bySeat, rawScores, selectedSeat, targetSeat, targetSeat,
            ReversalScenarioKind.DirectRon, honba, riichiSticks, requiredRank);
        var other = Enumerable.Range(0, 4)
            .Where(discarder => discarder != selectedSeat && discarder != targetSeat)
            .Select(discarder => FindCondition(bySeat, rawScores, selectedSeat, targetSeat, discarder,
                ReversalScenarioKind.OtherRon, honba, riichiSticks, requiredRank))
            .Where(condition => condition is not null)
            .OrderBy(condition => TierOrder(condition!))
            .FirstOrDefault();
        var tsumo = FindCondition(bySeat, rawScores, selectedSeat, targetSeat, null,
            ReversalScenarioKind.Tsumo, honba, riichiSticks, requiredRank);
        var opponentThresholds = totalOrder.Where(seat => seat != selectedSeat).Select(opponentSeat =>
        {
            var drop = rankIndex == 3
                ? null
                : FindDropCondition(bySeat, rawScores, opponentSeat, selectedSeat, rankIndex + 1,
                    honba, riichiSticks);
            var ron = FindCondition(bySeat, rawScores, selectedSeat, targetSeat, opponentSeat,
                opponentSeat == targetSeat ? ReversalScenarioKind.DirectRon : ReversalScenarioKind.OtherRon,
                honba, riichiSticks, requiredRank);
            return new OpponentReversalThreshold(
                opponentSeat,
                drop,
                ron,
                tsumo,
                rankIndex == 3,
                rankIndex != 3 && drop is null);
        }).ToArray();

        return new SelectedPlayerReversalResult(
            selectedSeat,
            rankIndex + 1,
            baselineSettlements[selectedSeat].Points,
            projectedTotals[selectedSeat],
            projectedTotals[targetSeat] - projectedTotals[selectedSeat],
            targetSeat,
            direct,
            other,
            tsumo,
            direct is null && other is null && tsumo is null,
            [],
            opponentThresholds);
    }

    private static ReversalCondition? FindDropCondition(
        IReadOnlyList<ReversalPlayerInput> players,
        int[] rawScores,
        int winnerSeat,
        int selectedSeat,
        int selectedBaselineRank,
        int honba,
        int riichiSticks)
    {
        var winnerIsDealer = players[winnerSeat].Wind == 0;
        foreach (var tier in WinTiers)
        {
            var scores = (int[])rawScores.Clone();
            var payment = RonPayment(tier.BasePoints, winnerIsDealer) + honba * 300;
            scores[selectedSeat] -= payment;
            scores[winnerSeat] += payment + riichiSticks * 1_000;

            var settlements = CalculateSettlements(scores);
            var order = players
                .OrderByDescending(player => player.PreviousPoints + settlements[player.Seat].Points)
                .ThenBy(player => player.Seat)
                .Select(player => player.Seat)
                .ToArray();
            if (Array.IndexOf(order, selectedSeat) + 1 <= selectedBaselineRank)
                continue;

            return new ReversalCondition(
                ReversalScenarioKind.DirectRon,
                FormatCondition(tier, ReversalScenarioKind.DirectRon, winnerIsDealer, honba),
                tier.BasePoints,
                tier.YakumanMultiplier,
                selectedSeat);
        }
        return null;
    }

    private static ReversalCondition? FindCondition(
        IReadOnlyList<ReversalPlayerInput> players,
        int[] rawScores,
        int winnerSeat,
        int targetSeat,
        int? discarderSeat,
        ReversalScenarioKind kind,
        int honba,
        int riichiSticks,
        int? requiredRank = null)
    {
        var winnerIsDealer = players[winnerSeat].Wind == 0;
        foreach (var tier in WinTiers)
        {
            var scores = (int[])rawScores.Clone();
            if (kind == ReversalScenarioKind.Tsumo)
            {
                for (var payerSeat = 0; payerSeat < 4; payerSeat++)
                {
                    if (payerSeat == winnerSeat)
                        continue;
                    var payerIsDealer = players[payerSeat].Wind == 0;
                    var payment = TsumoPayment(tier.BasePoints, winnerIsDealer, payerIsDealer) + honba * 100;
                    scores[payerSeat] -= payment;
                    scores[winnerSeat] += payment;
                }
            }
            else
            {
                var payment = RonPayment(tier.BasePoints, winnerIsDealer) + honba * 300;
                scores[discarderSeat!.Value] -= payment;
                scores[winnerSeat] += payment;
            }
            scores[winnerSeat] += riichiSticks * 1_000;

            var settlements = CalculateSettlements(scores);
            if (requiredRank is not null)
            {
                var order = players
                    .OrderByDescending(player => player.PreviousPoints + settlements[player.Seat].Points)
                    .ThenBy(player => player.Seat)
                    .Select(player => player.Seat)
                    .ToArray();
                if (Array.IndexOf(order, winnerSeat) + 1 > requiredRank.Value)
                    continue;
            }
            else
            {
                var winnerTotal = players[winnerSeat].PreviousPoints + settlements[winnerSeat].Points;
                var targetTotal = players[targetSeat].PreviousPoints + settlements[targetSeat].Points;
                if (winnerTotal <= targetTotal)
                    continue;
            }

            return new ReversalCondition(
                kind,
                FormatCondition(tier, kind, winnerIsDealer, honba),
                tier.BasePoints,
                tier.YakumanMultiplier,
                discarderSeat);
        }
        return null;
    }

    private static string FormatCondition(WinTier tier, ReversalScenarioKind kind, bool winnerIsDealer, int honba)
    {
        if (tier.YakumanMultiplier > 0)
        {
            if (kind != ReversalScenarioKind.Tsumo)
                return tier.Name;
            if (winnerIsDealer)
                return $"{tier.Name} all";
            return $"{tier.Name} {TsumoPayment(tier.BasePoints, false, false) + honba * 100} / {TsumoPayment(tier.BasePoints, false, true) + honba * 100}";
        }
        if (kind != ReversalScenarioKind.Tsumo)
            return (RonPayment(tier.BasePoints, winnerIsDealer) + honba * 300).ToString();
        var additional = honba * 100;
        if (winnerIsDealer)
            return $"{TsumoPayment(tier.BasePoints, true, false) + additional} all";
        var other = TsumoPayment(tier.BasePoints, false, false) + additional;
        var dealer = TsumoPayment(tier.BasePoints, false, true) + additional;
        return $"{other} / {dealer}";
    }

    private static int TierOrder(ReversalCondition condition) => condition.BasePoints;

    private static Settlement[] CalculateSettlements(int[] scores)
    {
        var orderedSeats = RankSeats(scores);
        var results = new Settlement[4];
        for (var index = 0; index < orderedSeats.Length; index++)
        {
            var seat = orderedSeats[index];
            results[seat] = new Settlement(
                index + 1,
                (scores[seat] - StartingScore) / 1_000m + Uma[index]);
        }
        return results;
    }

    private static int[] RankSeats(int[] scores) => Enumerable.Range(0, 4)
        .OrderByDescending(seat => scores[seat])
        .ThenBy(seat => seat)
        .ToArray();

    private static int RonPayment(int basePoints, bool dealer) =>
        RoundUpHundred(basePoints * (dealer ? 6 : 4));

    private static int TsumoPayment(int basePoints, bool winnerDealer, bool payerDealer) =>
        RoundUpHundred(basePoints * (winnerDealer || payerDealer ? 2 : 1));

    private static int RoundUpHundred(int value) => ((value + 99) / 100) * 100;

    private static WinTier[] CreateWinTiers()
    {
        var bases = new SortedSet<int>();
        int[] fuValues = [20, 25, 30, 40, 50, 60, 70, 80, 90, 100, 110];
        for (var han = 1; han <= 4; han++)
        {
            foreach (var fu in fuValues)
            {
                if ((fu == 20 && han < 2) || (fu == 25 && han < 2))
                    continue;
                bases.Add(Math.Min(2_000, fu * (1 << (han + 2))));
            }
        }

        var tiers = bases.Where(value => value < 2_000)
            .Select(value => new WinTier(value, string.Empty, 0))
            .ToList();
        tiers.AddRange([
            new WinTier(2_000, "满贯", 0),
            new WinTier(3_000, "跳满", 0),
            new WinTier(4_000, "倍满", 0),
            new WinTier(6_000, "三倍满", 0)
        ]);
        string[] names = ["役满", "双倍役满", "三倍役满", "四倍役满", "五倍役满", "六倍役满"];
        for (var multiplier = 1; multiplier <= 6; multiplier++)
            tiers.Add(new WinTier(YakumanBasePoints * multiplier, names[multiplier - 1], multiplier));
        return tiers.ToArray();
    }

    private static void Validate(IReadOnlyList<ReversalPlayerInput> players, int honba, int riichiSticks)
    {
        if (players.Count != 4 || !players.Select(player => player.Seat).Order().SequenceEqual([0, 1, 2, 3]))
            throw new ArgumentException("逆转计算需要 seat 0..3 四名玩家。", nameof(players));
        if (players.Any(player => player.Wind is < 0 or > 3) || players.Select(player => player.Wind).Distinct().Count() != 4)
            throw new ArgumentException("逆转计算需要东南西北四个唯一座位风。", nameof(players));
        if (players.Any(player => player.Score % 100 != 0))
            throw new ArgumentException("玩家点数必须为百点整数。", nameof(players));
        if (honba < 0 || riichiSticks < 0)
            throw new ArgumentOutOfRangeException(nameof(honba), "本场和供托不能为负数。");
    }

    private sealed record WinTier(int BasePoints, string Name, int YakumanMultiplier);
    private sealed record Settlement(int Rank, decimal Points);
}
