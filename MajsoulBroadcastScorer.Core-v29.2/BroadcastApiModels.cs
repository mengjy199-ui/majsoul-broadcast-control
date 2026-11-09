using System.Text.Json;

namespace MajsoulBroadcastScorer.Core.V27;

public sealed class BroadcastApiState
{
    public string Mode { get; init; } = "idle";
    public BroadcastAuthState Auth { get; init; } = new();
    public BroadcastObserveState? Observe { get; init; }
    public long ServerTime { get; init; }
}

public sealed class BroadcastAuthState
{
    public string Connection { get; init; } = "disconnected";
    public BroadcastAccount? Account { get; init; }
}

public sealed class BroadcastAccount
{
    public long AccountId { get; init; }
    public string Nickname { get; init; } = string.Empty;
}

public sealed class BroadcastRoundInfo
{
    public int Chang { get; init; }
    public int Ju { get; init; }
    public int Ben { get; init; }

    public string Label => $"{WindText.FromCode(Chang)}{Ju + 1}局";
}

public sealed class BroadcastObserveState
{
    public string GameUuid { get; init; } = string.Empty;
    public BroadcastRoundInfo Round { get; init; } = new();
    public int CurrentSeat { get; init; } = -1;
    public int WaitingSeconds { get; init; }
    public int RiichiSticks { get; init; }
    public IReadOnlyList<BroadcastPlayerState> Players { get; init; } = [];
    public bool Ended { get; init; }
    public IReadOnlyList<BroadcastFinalRankingState> FinalRanking { get; init; } = [];
}

public sealed class BroadcastFinalRankingState
{
    public int Rank { get; init; }
    public int Seat { get; init; }
    public long AccountId { get; init; }
    public string Nickname { get; init; } = string.Empty;
    public int TotalPoint { get; init; }
}

public sealed class BroadcastPlayerState
{
    public int Seat { get; init; }
    public int Wind { get; init; } = -1;
    public long AccountId { get; init; }
    public string Nickname { get; init; } = string.Empty;
    public string? TeamName { get; init; }
    public int Score { get; init; }
    public bool Riichi { get; init; }
    public bool Tenpai { get; init; }
}

public enum BroadcastMessageKind
{
    Ignored,
    Hello,
    Snapshot,
    Observe,
    Auth,
    Pong,
    Error
}

public sealed record BroadcastApiMessage(
    BroadcastMessageKind Kind,
    long Time = 0,
    string? Mode = null,
    BroadcastAuthState? Auth = null,
    BroadcastObserveState? Observe = null,
    string? ErrorMessage = null);

public static class BroadcastApiProtocol
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static BroadcastApiMessage Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var type = RequiredString(root, "type");
        var time = OptionalInt64(root, "time");

        return type switch
        {
            "hello" => new(BroadcastMessageKind.Hello, time),
            "pong" => new(BroadcastMessageKind.Pong, time),
            "snapshot" => ParseSnapshot(root, time),
            "event" => ParseEvent(root, time),
            "error" => new(BroadcastMessageKind.Error, time,
                ErrorMessage: OptionalString(root, "message") ?? "观战 API 返回未知错误"),
            _ => new(BroadcastMessageKind.Ignored, time)
        };
    }

    public static void Validate(BroadcastObserveState observe)
    {
        if (string.IsNullOrWhiteSpace(observe.GameUuid))
            throw new InvalidDataException("观战状态缺少 gameUuid。");
        if (observe.Round.Chang is < 0 or > 3 || observe.Round.Ju is < 0 or > 3 || observe.Round.Ben < 0)
            throw new InvalidDataException("场、局或本场数超出范围。");
        if (observe.RiichiSticks is < 0 or > 100)
            throw new InvalidDataException("供托数量超出范围。");
        if (observe.Players.Count != 4 ||
            observe.Players.Select(player => player.Seat).Order().SequenceEqual([0, 1, 2, 3]) is false ||
            observe.Players.Any(player => player.Wind is < -1 or > 3))
            throw new InvalidDataException("观战状态必须包含 seat 0..3 四名唯一玩家。");
        if (observe.Players.Any(player => player.Score is < -100000 or > 200000 || player.Score % 100 != 0))
            throw new InvalidDataException("观战状态包含无效分数。");

        // 结构化观战 API 可能先推送立直扣分，再推送供托变化。
        // 不因短暂的总点数不同步拒绝整帧，避免直播分数停留在旧值。
    }

    public static bool TryGetFinalScores(BroadcastObserveState observe, out int[] scores)
    {
        scores = new int[4];
        if (!observe.Ended || observe.FinalRanking.Count != 4 ||
            !observe.FinalRanking.Select(item => item.Seat).Order().SequenceEqual([0, 1, 2, 3]) ||
            observe.FinalRanking.Any(item => item.Rank is < 1 or > 4 ||
                item.TotalPoint is < -100000 or > 200000 || item.TotalPoint % 100 != 0) ||
            observe.FinalRanking.Sum(item => item.TotalPoint) != 100000)
            return false;

        foreach (var item in observe.FinalRanking)
            scores[item.Seat] = item.TotalPoint;
        return true;
    }

    private static BroadcastApiMessage ParseSnapshot(JsonElement root, long time)
    {
        var data = RequiredProperty(root, "data");
        var mode = OptionalString(data, "mode") ?? "idle";
        var auth = data.TryGetProperty("auth", out var authValue)
            ? Deserialize<BroadcastAuthState>(authValue)
            : new BroadcastAuthState();
        var observe = data.TryGetProperty("observe", out var observeValue) && observeValue.ValueKind != JsonValueKind.Null
            ? DeserializeObserve(observeValue)
            : null;
        return new(BroadcastMessageKind.Snapshot, time, mode, auth, observe);
    }

    private static BroadcastApiMessage ParseEvent(JsonElement root, long time)
    {
        var eventName = RequiredString(root, "event");
        var data = RequiredProperty(root, "data");
        return eventName switch
        {
            "observe" => new(BroadcastMessageKind.Observe, time,
                Observe: data.ValueKind == JsonValueKind.Null ? null : DeserializeObserve(data)),
            "auth" => new(BroadcastMessageKind.Auth, time, Auth: Deserialize<BroadcastAuthState>(data)),
            _ => new(BroadcastMessageKind.Ignored, time)
        };
    }

    private static BroadcastObserveState DeserializeObserve(JsonElement element)
    {
        var observe = Deserialize<BroadcastObserveState>(element);
        Validate(observe);
        return observe;
    }

    private static T Deserialize<T>(JsonElement element) =>
        JsonSerializer.Deserialize<T>(element.GetRawText(), JsonOptions) ??
        throw new InvalidDataException($"观战 API 数据无法解析为 {typeof(T).Name}。");

    private static JsonElement RequiredProperty(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? value :
        throw new InvalidDataException($"观战 API 消息缺少 {name} 字段。");

    private static string RequiredString(JsonElement element, string name) =>
        OptionalString(element, name) ??
        throw new InvalidDataException($"观战 API 消息缺少 {name} 字符串。");

    private static string? OptionalString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    private static long OptionalInt64(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt64(out var result) ? result : 0;
}
