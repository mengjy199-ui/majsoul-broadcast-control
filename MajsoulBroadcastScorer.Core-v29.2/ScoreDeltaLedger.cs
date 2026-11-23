using System.Text.Json;

namespace MajsoulBroadcastScorer.Core.V27;

public sealed class ScoreDeltaLedger(string path, int playerCount)
{
    private readonly int _startingScore = playerCount switch
    {
        3 => 35000,
        4 => 25000,
        _ => throw new ArgumentOutOfRangeException(nameof(playerCount))
    };
    private Dictionary<string, Result>? _results;

    private Dictionary<string, Result> Results => _results ??= File.Exists(path)
        ? JsonSerializer.Deserialize<Dictionary<string, Result>>(File.ReadAllText(path), V27SettingsJson.Options)
            ?? throw new InvalidDataException("累计得失分记录为空。")
        : new(StringComparer.Ordinal);

    public string GetContest(string gameUuid, string fallback) =>
        Results.TryGetValue(gameUuid, out var result) ? result.ContestId : fallback;

    public bool Record(string contestId, BroadcastObserveState observe)
    {
        if (observe.Players.Count != playerCount || string.IsNullOrWhiteSpace(observe.GameUuid) ||
            observe.Players.Any(player => player.AccountId <= 0) ||
            observe.Players.Select(player => player.AccountId).Distinct().Count() != playerCount ||
            !observe.Players.Select(player => player.Seat).Order().SequenceEqual(Enumerable.Range(0, playerCount)) ||
            !BroadcastApiProtocol.TryGetFinalScores(observe, out var scores))
            return false;

        var deltas = observe.Players.ToDictionary(player => player.AccountId, player => scores[player.Seat] - _startingScore);
        if (Results.TryGetValue(observe.GameUuid, out var existing))
        {
            if (deltas.All(item => existing.Deltas.TryGetValue(item.Key, out var old) && old == item.Value))
                return false;
            if (!existing.Deltas.Keys.ToHashSet().SetEquals(deltas.Keys))
                return false;
            contestId = existing.ContestId;
        }

        var updated = new Dictionary<string, Result>(Results)
        {
            [observe.GameUuid] = new(contestId, deltas)
        };
        var temporary = path + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(temporary, JsonSerializer.Serialize(updated, V27SettingsJson.Options));
        File.Move(temporary, path, true);
        _results = updated;
        return true;
    }

    public decimal CurrentGamePoints(int score) => (score - _startingScore) / 1000m;

    public decimal ProjectedTotal(decimal previous, int score) => previous + CurrentGamePoints(score);

    public decimal Total(string contestId, long accountId, string? excludedGameUuid) => Results
        .Where(item => item.Value.ContestId == contestId && item.Key != excludedGameUuid)
        .Sum(item => item.Value.Deltas.GetValueOrDefault(accountId) / 1000m);

    private sealed record Result(string ContestId, Dictionary<long, int> Deltas);
}
