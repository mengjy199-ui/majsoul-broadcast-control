using System.Globalization;
using System.Text;
using System.Text.Json;

namespace MajsoulBroadcastScorer.Core.V27;

public sealed record ScoreReportPlayer(int Seat, int Wind, string Nickname, int Score);

public sealed record ScoreReportEntry(
    DateTimeOffset Timestamp,
    string Event,
    string RoundLabel,
    int Honba,
    int RiichiSticks,
    IReadOnlyList<ScoreReportPlayer> Players);

public sealed record MatchScoreReport(
    string GameUuid,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    bool Completed,
    string CompletionReason,
    IReadOnlyList<ScoreReportEntry> Entries);

public sealed class MatchScoreReportRecorder : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly object _sync = new();
    private readonly string _reportDirectory;
    private readonly HashSet<string> _completedGames = new(StringComparer.Ordinal);
    private Session? _current;
    private bool _disposed;

    public MatchScoreReportRecorder(string reportDirectory)
    {
        _reportDirectory = Path.GetFullPath(reportDirectory);
    }

    public string ReportDirectory => _reportDirectory;
    public string CurrentGameUuid
    {
        get { lock (_sync) return _current?.GameUuid ?? string.Empty; }
    }
    public int CurrentEntryCount
    {
        get { lock (_sync) return _current?.Entries.Count ?? 0; }
    }
    public string? LastJsonPath { get; private set; }
    public string? LastCsvPath { get; private set; }

    public bool Observe(
        LiveDisplayState display,
        string source,
        DateTimeOffset timestamp,
        bool ended = false)
    {
        ArgumentNullException.ThrowIfNull(display);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!display.IsObserving || string.IsNullOrWhiteSpace(display.GameUuid) || display.Players.Count is not (3 or 4))
            return false;

        lock (_sync)
        {
            if (_current is null && _completedGames.Contains(display.GameUuid))
                return false;
            if (_current is not null && !string.Equals(_current.GameUuid, display.GameUuid, StringComparison.Ordinal))
                FinalizeCurrent(timestamp, "检测到新对局");
            _current ??= Session.Start(display.GameUuid, timestamp);
            if (_current.Entries.Count > 0 && _current.Entries[0].Players.Count != display.Players.Count)
                _current.Entries.Clear();

            var players = SnapshotPlayers(display);
            var scoreChanged = _current.Entries.Count == 0 ||
                               !_current.Entries[^1].Players.OrderBy(player => player.Seat)
                                   .Select(player => player.Score)
                                   .SequenceEqual(players.Select(player => player.Score));
            if (scoreChanged)
            {
                var eventName = _current.Entries.Count == 0
                    ? "首次记录"
                    : string.Equals(source, "人工确认", StringComparison.Ordinal) ? "人工修正" : "分数变化";
                _current.Entries.Add(new ScoreReportEntry(
                    timestamp,
                    eventName,
                    display.RoundLabel,
                    display.Honba,
                    display.RiichiSticks,
                    players));
            }

            _current.LatestPlayers = players;
            if (ended)
            {
                if (!scoreChanged)
                {
                    _current.Entries.Add(new ScoreReportEntry(
                        timestamp,
                        "终局确认",
                        display.RoundLabel,
                        display.Honba,
                        display.RiichiSticks,
                        players));
                }
                FinalizeCurrent(timestamp, "对局结束");
                return true;
            }

            if (scoreChanged)
                WriteCurrent(false, string.Empty, null);
            return scoreChanged;
        }
    }

    public bool SaveCurrent(DateTimeOffset timestamp)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_sync)
        {
            if (_current is null)
                return false;
            WriteCurrent(false, "进行中手动保存", timestamp);
            return true;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            if (_current is not null)
                FinalizeCurrent(DateTimeOffset.Now, "程序关闭（未收到终局）");
            _disposed = true;
        }
    }

    private void FinalizeCurrent(DateTimeOffset timestamp, string reason)
    {
        if (_current is null)
            return;
        WriteCurrent(true, reason, timestamp);
        _completedGames.Add(_current.GameUuid);
        _current = null;
    }

    private void WriteCurrent(bool completed, string reason, DateTimeOffset? finishedAt)
    {
        if (_current is null)
            return;
        Directory.CreateDirectory(_reportDirectory);
        var report = new MatchScoreReport(
            _current.GameUuid,
            _current.StartedAt,
            finishedAt,
            completed,
            reason,
            _current.Entries.ToArray());
        var basePath = Path.Combine(_reportDirectory, _current.FileStem);
        var jsonPath = basePath + ".json";
        var csvPath = basePath + ".csv";
        WriteAtomic(jsonPath, JsonSerializer.Serialize(report, JsonOptions), new UTF8Encoding(false));
        WriteAtomic(csvPath, BuildCsv(report), new UTF8Encoding(true));
        LastJsonPath = jsonPath;
        LastCsvPath = csvPath;
    }

    private static IReadOnlyList<ScoreReportPlayer> SnapshotPlayers(LiveDisplayState display) =>
        display.Players.OrderBy(player => player.Seat)
            .Select(player => new ScoreReportPlayer(player.Seat, player.Wind, player.Nickname, player.Score))
            .ToArray();

    private static string BuildCsv(MatchScoreReport report)
    {
        var builder = new StringBuilder();
        builder.Append("时间,事件,场局,本场,供托");
        var playerCount = report.Entries.Count == 0 ? 0 : report.Entries.Max(entry => entry.Players.Count);
        for (var seat = 0; seat < playerCount; seat++)
            builder.Append($",位置{seat + 1}昵称,位置{seat + 1}座位风,位置{seat + 1}分数,位置{seat + 1}变化");
        builder.AppendLine();

        int[]? previousScores = null;
        foreach (var entry in report.Entries)
        {
            builder.Append(Csv(entry.Timestamp.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)));
            builder.Append(',').Append(Csv(entry.Event));
            builder.Append(',').Append(Csv(entry.RoundLabel));
            builder.Append(',').Append(entry.Honba.ToString(CultureInfo.InvariantCulture));
            builder.Append(',').Append(entry.RiichiSticks.ToString(CultureInfo.InvariantCulture));
            var ordered = entry.Players.OrderBy(player => player.Seat).ToArray();
            foreach (var player in ordered)
            {
                var delta = previousScores is null || player.Seat >= previousScores.Length
                    ? 0 : player.Score - previousScores[player.Seat];
                builder.Append(',').Append(Csv(player.Nickname));
                builder.Append(',').Append(Csv(WindText.FromCode(player.Wind)));
                builder.Append(',').Append(player.Score.ToString(CultureInfo.InvariantCulture));
                builder.Append(',').Append(delta.ToString("+0;-0;0", CultureInfo.InvariantCulture));
            }
            builder.AppendLine();
            previousScores = ordered.Select(player => player.Score).ToArray();
        }
        return builder.ToString();
    }

    private static string Csv(string value) => CsvText.Escape(value);

    private static void WriteAtomic(string path, string content, Encoding encoding)
    {
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, content, encoding);
        File.Move(temporaryPath, path, true);
    }

    private sealed class Session
    {
        public required string GameUuid { get; init; }
        public required DateTimeOffset StartedAt { get; init; }
        public required string FileStem { get; init; }
        public List<ScoreReportEntry> Entries { get; } = [];
        public IReadOnlyList<ScoreReportPlayer> LatestPlayers { get; set; } = [];

        public static Session Start(string gameUuid, DateTimeOffset timestamp)
        {
            var safeUuid = new string(gameUuid.Where(character =>
                char.IsLetterOrDigit(character) || character is '-' or '_').ToArray());
            if (safeUuid.Length == 0)
                safeUuid = "unknown-game";
            if (safeUuid.Length > 48)
                safeUuid = safeUuid[..48];
            return new Session
            {
                GameUuid = gameUuid,
                StartedAt = timestamp,
                FileStem = $"{timestamp.LocalDateTime:yyyyMMdd-HHmmss}_{safeUuid}"
            };
        }
    }
}
