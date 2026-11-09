using System.Globalization;
using System.Text;
using System.Text.Json;

namespace MajsoulBroadcastScorer.Core.V27;

public sealed record RoundReportPlayer(int Seat, long AccountId, string Nickname);

public sealed record DetailedMatchReport(
    string GameUuid,
    DateTimeOffset StartedAt,
    DateTimeOffset UpdatedAt,
    bool Ended,
    string Coverage,
    IReadOnlyList<RoundReportPlayer> Players,
    IReadOnlyList<JsonElement> RoundResults);

public sealed class MatchRoundReportRecorder : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _directory;
    private readonly Dictionary<string, JsonElement> _records = new(StringComparer.Ordinal);
    private readonly List<string> _recordOrder = [];
    private readonly Dictionary<int, RoundReportPlayer> _players = [];
    private DateTimeOffset _startedAt;
    private DateTimeOffset _updatedAt;
    private string _fileStem = string.Empty;
    private bool _ended;
    private bool _disposed;

    public MatchRoundReportRecorder(string directory) => _directory = Path.GetFullPath(directory);

    public string GameUuid { get; private set; } = string.Empty;
    public int Count => _records.Count;
    public IReadOnlyList<JsonElement> Results => _recordOrder.Select(key => _records[key]).ToArray();
    public IReadOnlyList<RoundReportPlayer> Players => _players.Values.OrderBy(player => player.Seat).ToArray();

    public bool Observe(BroadcastObserveState observe, DateTimeOffset timestamp)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(observe.GameUuid)) return false;
        var changed = false;
        if (!string.Equals(GameUuid, observe.GameUuid, StringComparison.Ordinal))
        {
            SaveCurrent();
            GameUuid = observe.GameUuid;
            _startedAt = timestamp;
            _updatedAt = timestamp;
            _ended = false;
            _records.Clear();
            _recordOrder.Clear();
            _players.Clear();
            var safeUuid = new string(GameUuid.Where(character => char.IsLetterOrDigit(character) || character is '-' or '_').ToArray());
            if (safeUuid.Length == 0) throw new InvalidDataException("逐局报告的对局 ID 无效。");
            if (safeUuid.Length > 48) safeUuid = safeUuid[..48];
            _fileStem = $"{timestamp.LocalDateTime:yyyyMMdd-HHmmss}_{safeUuid}.rounds";
            changed = true;
        }
        foreach (var player in observe.Players.Where(player => player.Seat is >= 0 and <= 3))
        {
            if (!string.IsNullOrWhiteSpace(player.Nickname) || !_players.ContainsKey(player.Seat))
                _players[player.Seat] = new(player.Seat, player.AccountId, player.Nickname);
        }
        foreach (var result in observe.RoundResults ?? [])
        {
            if (result.ValueKind != JsonValueKind.Object ||
                !result.TryGetProperty("key", out var keyValue) || keyValue.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(keyValue.GetString()))
                throw new InvalidDataException("收到的逐局结果缺少唯一编号，已保留既有报告。");
            var key = keyValue.GetString()!;
            if (_records.TryGetValue(key, out var previous) && previous.GetRawText() == result.GetRawText()) continue;
            if (!_records.ContainsKey(key)) _recordOrder.Add(key);
            _records[key] = result.Clone();
            changed = true;
        }
        if (_ended != observe.Ended)
        {
            _ended = observe.Ended;
            changed = true;
        }
        if (changed)
        {
            _updatedAt = timestamp;
            SaveCurrent();
        }
        return changed;
    }

    public bool SaveCurrent()
    {
        if (_records.Count == 0) return false;
        Directory.CreateDirectory(_directory);
        var report = new DetailedMatchReport(GameUuid, _startedAt, _updatedAt, _ended,
            "从开始观战时记录实际收到的逐局结算，接入前未收到的详情不自动补全。", Players, Results);
        var file = Path.Combine(_directory, _fileStem);
        WriteAtomic(file + ".json", JsonSerializer.Serialize(report, JsonOptions), new UTF8Encoding(false));
        WriteAtomic(file + ".csv", RoundResultFormatter.BuildCsv(report), new UTF8Encoding(true));
        WriteAtomic(file + ".txt", RoundResultFormatter.BuildText(report), new UTF8Encoding(true));
        return true;
    }

    private static void WriteAtomic(string path, string content, Encoding encoding)
    {
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, content, encoding);
        File.Move(temporary, path, true);
    }

    public void Dispose()
    {
        if (_disposed) return;
        SaveCurrent();
        _disposed = true;
    }
}
