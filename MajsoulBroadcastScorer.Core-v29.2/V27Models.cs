using System.Text.Json;

namespace MajsoulBroadcastScorer.Core.V27;

public enum RiichiDisplayMode
{
    Auto,
    ForceOn,
    ForceOff
}

public enum AvatarDisplayMode
{
    Image,
    Transparent
}

public enum AvatarFrameDisplayMode
{
    Default,
    Custom,
    Hidden
}

public enum OverlayLayoutStyle
{
    HorizontalPlayers,
    VerticalAvatar,
    GapConditions,
    TeamMode,
    VerticalMatch,
    FrierenVerticalAvatar
}

public enum VerticalNameContent { PlayerName, TeamName }
public enum VerticalNumberContent { CalculationPoints, CurrentScore }

public static class WindText
{
    public static string FromCode(int wind) => wind switch
    {
        0 => "东",
        1 => "南",
        2 => "西",
        3 => "北",
        _ => "?"
    };

    public static int ToCode(string? wind) => wind?.Trim() switch
    {
        "东" => 0,
        "南" => 1,
        "西" => 2,
        "北" => 3,
        _ => -1
    };
}

public sealed class AssetLayout
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool LockAspectRatio { get; set; } = true;

    public AssetLayout Clamp(double maxWidth, double maxHeight)
    {
        Width = Math.Clamp(Width, 1, maxWidth);
        Height = Math.Clamp(Height, 1, maxHeight);
        X = Math.Clamp(X, 0, maxWidth - Width);
        Y = Math.Clamp(Y, 0, maxHeight - Height);
        return this;
    }
}

public sealed class PlayerSlotSettings
{
    public int Seat { get; set; }
    public long AccountId { get; set; }
    public bool NameLocked { get; set; }
    public string ManualName { get; set; } = string.Empty;
    public bool ScoreLocked { get; set; }
    public int ManualScore { get; set; } = 25000;
    public bool WindLocked { get; set; }
    public int ManualWind { get; set; } = -1;
    public decimal TotalPoints { get; set; }
    public RiichiDisplayMode RiichiMode { get; set; }
    public AvatarDisplayMode AvatarMode { get; set; }
    public string AvatarPath { get; set; } = string.Empty;
    public AvatarFrameDisplayMode AvatarFrameMode { get; set; }
    public string AvatarFramePath { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public bool TeamNameLocked { get; set; }
}

public sealed class V27Settings
{
    public int Version { get; set; } = 292;
    public string Department { get; set; } = "部门";
    public string MatchText { get; set; } = "当前回战";
    public string AccountName { get; set; } = string.Empty;
    public string ContestId { get; set; } = string.Empty;
    public OverlayLayoutStyle OverlayStyle { get; set; } = OverlayLayoutStyle.HorizontalPlayers;
    public VerticalNameContent VerticalName { get; set; } = VerticalNameContent.TeamName;
    public VerticalNumberContent VerticalNumber { get; set; } = VerticalNumberContent.CurrentScore;
    public int GapSelectedSeat { get; set; }
    public ReversalTargetMode GapTargetMode { get; set; } = ReversalTargetMode.PreviousRank;
    public bool ShowTotalPoints { get; set; } = true;
    public double ApiDisplayDelaySeconds { get; set; } = 1.5;
    public string LeftBoardPath { get; set; } = string.Empty;
    public double LeftBoardFocusX { get; set; } = 50;
    public double LeftBoardFocusY { get; set; } = 50;
    public string LogoPath { get; set; } = string.Empty;
    public string BottomLeftPath { get; set; } = string.Empty;
    public AssetLayout LogoLayout { get; set; } = new()
    {
        X = 35,
        Y = 32,
        Width = 300,
        Height = 135,
        LockAspectRatio = false
    };
    public AssetLayout BottomLeftLayout { get; set; } = new()
    {
        X = 0,
        Y = 856,
        Width = 370,
        Height = 224,
        LockAspectRatio = false
    };
    public List<PlayerSlotSettings> Players { get; set; } = CreateDefaultPlayers();

    public void Normalize()
    {
        var migratingFromV27 = Version < 271;
        var migratingTeams = Version < 292;
        Version = 292;
        if (!Enum.IsDefined(VerticalName)) VerticalName = VerticalNameContent.TeamName;
        if (!Enum.IsDefined(VerticalNumber)) VerticalNumber = VerticalNumberContent.CurrentScore;
        LogoLayout ??= new AssetLayout { X = 35, Y = 32, Width = 300, Height = 135 };
        BottomLeftLayout ??= new AssetLayout { X = 0, Y = 856, Width = 370, Height = 224, LockAspectRatio = false };
        if (migratingFromV27)
            BottomLeftLayout.LockAspectRatio = false;
        LeftBoardFocusX = Math.Clamp(LeftBoardFocusX, 0, 100);
        LeftBoardFocusY = Math.Clamp(LeftBoardFocusY, 0, 100);
        GapSelectedSeat = Math.Clamp(GapSelectedSeat, 0, 3);
        ApiDisplayDelaySeconds = Math.Clamp(ApiDisplayDelaySeconds, 0, 30);
        if (!Enum.IsDefined(GapTargetMode))
            GapTargetMode = ReversalTargetMode.PreviousRank;
        LogoLayout.Clamp(1920, 1080);
        BottomLeftLayout.Clamp(1920, 1080);

        var bySeat = (Players ?? []).Where(player => player.Seat is >= 0 and <= 3)
            .GroupBy(player => player.Seat)
            .ToDictionary(group => group.Key, group => group.First());
        Players = Enumerable.Range(0, 4)
            .Select(seat => bySeat.TryGetValue(seat, out var player)
                ? player
                : new PlayerSlotSettings { Seat = seat })
            .OrderBy(player => player.Seat)
            .ToList();
        if (migratingTeams)
            foreach (var player in Players.Where(player => !string.IsNullOrWhiteSpace(player.TeamName)))
                player.TeamNameLocked = true;
    }

    public void ResetLogoPosition()
    {
        LogoLayout.X = 35;
        LogoLayout.Y = 32;
        LogoLayout.Clamp(1920, 1080);
    }

    public void ResetBottomLeftPosition()
    {
        BottomLeftLayout.X = 0;
        BottomLeftLayout.Y = 856;
        BottomLeftLayout.Width = 370;
        BottomLeftLayout.Height = 224;
        BottomLeftLayout.LockAspectRatio = false;
    }

    public void ResetLeftBoardFocus()
    {
        LeftBoardFocusX = 50;
        LeftBoardFocusY = 50;
    }

    public bool BindTotalsToPlayers(IReadOnlyList<BroadcastPlayerState> players)
    {
        ArgumentNullException.ThrowIfNull(players);
        Normalize();
        var byAccount = Players
            .Where(player => player.AccountId > 0)
            .GroupBy(player => player.AccountId)
            .ToDictionary(group => group.Key, group => group.First().TotalPoints);
        var bindings = Players.Select(player => (player.AccountId, player.TotalPoints)).ToArray();

        foreach (var player in players.Where(player => player.Seat is >= 0 and <= 3))
        {
            var target = Players[player.Seat];
            if (player.AccountId > 0 && byAccount.TryGetValue(player.AccountId, out var matched))
            {
                target.AccountId = player.AccountId;
                target.TotalPoints = matched;
            }
            else if (player.AccountId > 0)
            {
                // 新账号不能继承该座位上前一名选手的累计点。
                target.AccountId = player.AccountId;
                target.TotalPoints = 0m;
            }
        }

        return !bindings.SequenceEqual(Players.Select(player => (player.AccountId, player.TotalPoints)));
    }

    private static List<PlayerSlotSettings> CreateDefaultPlayers() =>
        Enumerable.Range(0, 4).Select(seat => new PlayerSlotSettings { Seat = seat }).ToList();
}

public sealed record DisplayPlayerState(
    int Seat,
    int Wind,
    string Nickname,
    int Score,
    decimal TotalPoints,
    bool Riichi,
    AvatarDisplayMode AvatarMode,
    string AvatarPath,
    AvatarFrameDisplayMode AvatarFrameMode,
    string AvatarFramePath,
    string TeamName);

public sealed record LiveDisplayState(
    bool ApiConnected,
    bool IsObserving,
    string ConnectionText,
    string GameUuid,
    string RoundLabel,
    int Honba,
    int RiichiSticks,
    long LastScoreUpdateUnixMs,
    bool ShowTotalPoints,
    IReadOnlyList<DisplayPlayerState> Players);

public sealed class LiveStateReducer
{
    private readonly ApiPlayerMemory[] _memory = Enumerable.Range(0, 4)
        .Select(seat => new ApiPlayerMemory(seat, $"选手{seat + 1}", 25000, -1, false, 0, string.Empty))
        .ToArray();
    private V27Settings _settings;
    private bool _connected;
    private bool _observing;
    private string _connectionText = "观战 API 未连接";
    private string _gameUuid = string.Empty;
    private string _roundLabel = "等待观战";
    private int _honba;
    private int _riichiSticks;
    private long _lastScoreUpdateUnixMs;

    public LiveStateReducer(V27Settings settings)
    {
        _settings = settings;
        _settings.Normalize();
    }

    public LiveDisplayState Current => Build();

    public void UpdateSettings(V27Settings settings)
    {
        settings.Normalize();
        _settings = settings;
    }

    public void SetConnection(bool connected, string text)
    {
        _connected = connected;
        _connectionText = text;
    }

    public bool Apply(BroadcastApiState state, bool bindPlayerTotals = false)
    {
        _connected = true;
        _connectionText = state.Observe is null ? "API 已连接，当前未观战" : "API 已连接，正在观战";
        if (state.Observe is null)
        {
            _observing = false;
            return false;
        }

        BroadcastApiProtocol.Validate(state.Observe);
        var observe = state.Observe;
        int[]? finalScores = null;
        if (observe.Ended)
        {
            if (!BroadcastApiProtocol.TryGetFinalScores(observe, out var validatedFinalScores))
                return false;
            finalScores = validatedFinalScores;
        }
        if (bindPlayerTotals)
            _settings.BindTotalsToPlayers(observe.Players);
        var scoreChanged = false;
        foreach (var player in observe.Players)
        {
            var previous = _memory[player.Seat];
            var changedPlayer = _gameUuid != observe.GameUuid ||
                (previous.AccountId > 0 && player.AccountId > 0 && previous.AccountId != player.AccountId);
            var nextScore = finalScores?[player.Seat] ?? player.Score;
            scoreChanged |= previous.Score != nextScore;
            _memory[player.Seat] = previous with
            {
                Nickname = string.IsNullOrWhiteSpace(player.Nickname) ? previous.Nickname : player.Nickname.Trim(),
                Score = nextScore,
                Wind = player.Wind,
                Riichi = player.Riichi,
                AccountId = player.AccountId,
                TeamName = !string.IsNullOrWhiteSpace(player.TeamName)
                    ? player.TeamName.Trim()
                    : changedPlayer ? string.Empty : previous.TeamName
            };
        }

        _observing = true;
        _gameUuid = observe.GameUuid;
        _roundLabel = observe.Round.Label;
        _honba = observe.Round.Ben;
        _riichiSticks = observe.RiichiSticks;
        if (scoreChanged)
            _lastScoreUpdateUnixMs = state.ServerTime > 0
                ? state.ServerTime
                : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _connectionText = "API 已连接，正在观战";
        return true;
    }

    public static bool ResolveRiichi(bool apiValue, RiichiDisplayMode mode) => mode switch
    {
        RiichiDisplayMode.ForceOn => true,
        RiichiDisplayMode.ForceOff => false,
        _ => apiValue
    };

    private LiveDisplayState Build()
    {
        var players = _settings.Players.OrderBy(player => player.Seat).Select(setting =>
        {
            var api = _memory[setting.Seat];
            return new DisplayPlayerState(
                setting.Seat,
                setting.WindLocked ? setting.ManualWind : api.Wind,
                setting.NameLocked && !string.IsNullOrWhiteSpace(setting.ManualName)
                    ? setting.ManualName.Trim()
                    : api.Nickname,
                setting.ScoreLocked ? setting.ManualScore : api.Score,
                setting.TotalPoints,
                ResolveRiichi(api.Riichi, setting.RiichiMode),
                setting.AvatarMode,
                setting.AvatarPath,
                setting.AvatarFrameMode,
                setting.AvatarFramePath,
                setting.TeamNameLocked ? setting.TeamName : api.TeamName);
        }).ToArray();

        return new LiveDisplayState(
            _connected,
            _observing,
            _connectionText,
            _gameUuid,
            _roundLabel,
            _honba,
            _riichiSticks,
            _lastScoreUpdateUnixMs,
            _settings.ShowTotalPoints,
            players);
    }

    private sealed record ApiPlayerMemory(int Seat, string Nickname, int Score, int Wind, bool Riichi, long AccountId, string TeamName);
}

public static class V27SettingsJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string Serialize(V27Settings settings)
    {
        settings.Normalize();
        return JsonSerializer.Serialize(settings, Options);
    }

    public static V27Settings Deserialize(string json)
    {
        var settings = JsonSerializer.Deserialize<V27Settings>(json, Options) ?? new V27Settings();
        settings.Normalize();
        return settings;
    }
}

public sealed class TotalPointsDraft
{
    private readonly decimal[] _values = new decimal[4];
    private readonly bool[] _dirty = new bool[4];
    private int _revision;
    internal int Revision => _revision;
    internal void AcceptIfUnchanged(int revision)
    {
        if (_revision != revision) return;
        Array.Fill(_dirty, false);
        _revision++;
    }

    public TotalPointsDraft(V27Settings settings)
    {
        settings.Normalize();
        foreach (var player in settings.Players)
            _values[player.Seat] = player.TotalPoints;
    }

    public bool HasPendingChanges => _dirty.Any(value => value);

    public decimal Get(int seat) => _values[ValidateSeat(seat)];

    public void Set(int seat, decimal value)
    {
        seat = ValidateSeat(seat);
        _values[seat] = Math.Round(value, 1);
        _dirty[seat] = true;
        _revision++;
    }

    public void SetAll(IReadOnlyDictionary<int, decimal> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count != 4 || Enumerable.Range(0, 4).Any(seat => !values.ContainsKey(seat)))
            throw new ArgumentException("必须同时提供四个座位的累计精算点。", nameof(values));

        foreach (var seat in Enumerable.Range(0, 4))
            Set(seat, values[seat]);
    }

    public bool IsDirty(int seat) => _dirty[ValidateSeat(seat)];

    public void Rebind(IReadOnlyDictionary<int, int> seatMoves)
    {
        ArgumentNullException.ThrowIfNull(seatMoves);
        var values = new decimal[4];
        var dirty = new bool[4];
        var assigned = new HashSet<int>();
        for (var oldSeat = 0; oldSeat < 4; oldSeat++)
        {
            var newSeat = seatMoves.TryGetValue(oldSeat, out var movedSeat) ? movedSeat : oldSeat;
            newSeat = ValidateSeat(newSeat);
            if (!assigned.Add(newSeat))
                throw new ArgumentException("累计精算点的换座目标必须唯一。", nameof(seatMoves));
            values[newSeat] = _values[oldSeat];
            dirty[newSeat] = _dirty[oldSeat];
        }

        Array.Copy(values, _values, values.Length);
        Array.Copy(dirty, _dirty, dirty.Length);
        if (seatMoves.Any(item => item.Key != item.Value))
            _revision++;
    }

    public void Apply(V27Settings settings) => ApplyTo(settings, true);

    public void CopyTo(V27Settings settings) => ApplyTo(settings, false);

    private void ApplyTo(V27Settings settings, bool clearDraft)
    {
        settings.Normalize();
        foreach (var player in settings.Players)
        {
            if (_dirty[player.Seat])
                player.TotalPoints = _values[player.Seat];
            if (clearDraft) _dirty[player.Seat] = false;
        }
    }

    private static int ValidateSeat(int seat) => seat is >= 0 and <= 3
        ? seat
        : throw new ArgumentOutOfRangeException(nameof(seat));
}

public sealed class PlayerPresentationDraft
{
    private readonly Slot[] _slots = Enumerable.Range(0, 4).Select(_ => new Slot()).ToArray();
    private int _revision;
    internal int Revision => _revision;
    internal void AcceptIfUnchanged(int revision)
    {
        if (_revision != revision) return;
        for (var seat = 0; seat < _slots.Length; seat++) _slots[seat] = new Slot();
        _revision++;
    }

    public bool HasPendingChanges => _slots.Any(slot =>
        slot.NameDirty || slot.ScoreDirty || slot.AvatarDirty || slot.AvatarFrameDirty || slot.TeamNameDirty);

    public bool IsNameDirty(int seat) => SlotAt(seat).NameDirty;
    public bool IsScoreDirty(int seat) => SlotAt(seat).ScoreDirty;
    public bool IsAvatarDirty(int seat) => SlotAt(seat).AvatarDirty;
    public bool IsAvatarFrameDirty(int seat) => SlotAt(seat).AvatarFrameDirty;
    public bool IsTeamNameDirty(int seat) => SlotAt(seat).TeamNameDirty;
    public bool IsNameRestorePending(int seat) => SlotAt(seat).RestoreNameFromApi;
    public bool IsScoreRestorePending(int seat) => SlotAt(seat).RestoreScoreFromApi;
    public bool IsTeamRestorePending(int seat) => SlotAt(seat).RestoreTeamFromApi;

    public string GetName(int seat, string fallback)
    {
        var slot = SlotAt(seat);
        return slot.NameDirty && !slot.RestoreNameFromApi ? slot.Name : fallback;
    }

    public int GetScore(int seat, int fallback)
    {
        var slot = SlotAt(seat);
        return slot.ScoreDirty && !slot.RestoreScoreFromApi ? slot.Score : fallback;
    }

    public string GetAvatarPath(int seat, string fallback)
    {
        var slot = SlotAt(seat);
        return slot.AvatarDirty && slot.AvatarPath is not null ? slot.AvatarPath : fallback;
    }

    public AvatarDisplayMode GetAvatarMode(int seat, AvatarDisplayMode fallback)
    {
        var slot = SlotAt(seat);
        return slot.AvatarDirty && slot.AvatarMode is not null ? slot.AvatarMode.Value : fallback;
    }

    public string GetAvatarFramePath(int seat, string fallback)
    {
        var slot = SlotAt(seat);
        return slot.AvatarFrameDirty && slot.AvatarFramePath is not null ? slot.AvatarFramePath : fallback;
    }

    public AvatarFrameDisplayMode GetAvatarFrameMode(int seat, AvatarFrameDisplayMode fallback)
    {
        var slot = SlotAt(seat);
        return slot.AvatarFrameDirty && slot.AvatarFrameMode is not null
            ? slot.AvatarFrameMode.Value
            : fallback;
    }

    public string GetTeamName(int seat, string fallback)
    {
        var slot = SlotAt(seat);
        return slot.TeamNameDirty && !slot.RestoreTeamFromApi ? slot.TeamName : fallback;
    }

    public void StageName(int seat, string value)
    {
        var slot = SlotAt(seat);
        _revision++;
        slot.Name = value;
        slot.NameDirty = true;
        slot.RestoreNameFromApi = false;
    }

    public void StageScore(int seat, int value)
    {
        var slot = SlotAt(seat);
        _revision++;
        slot.Score = value;
        slot.ScoreDirty = true;
        slot.RestoreScoreFromApi = false;
    }

    public void StageRestoreName(int seat)
    {
        var slot = SlotAt(seat);
        _revision++;
        slot.NameDirty = true;
        slot.RestoreNameFromApi = true;
    }

    public void StageRestoreScore(int seat)
    {
        var slot = SlotAt(seat);
        _revision++;
        slot.ScoreDirty = true;
        slot.RestoreScoreFromApi = true;
    }

    public void StageAvatarPath(int seat, string path)
    {
        var slot = SlotAt(seat);
        _revision++;
        slot.AvatarPath = path;
        slot.AvatarDirty = true;
    }

    public void StageAvatarMode(int seat, AvatarDisplayMode mode)
    {
        var slot = SlotAt(seat);
        _revision++;
        slot.AvatarMode = mode;
        slot.AvatarDirty = true;
    }

    public void StageAvatarFramePath(int seat, string path)
    {
        var slot = SlotAt(seat);
        _revision++;
        slot.AvatarFramePath = path;
        slot.AvatarFrameDirty = true;
    }

    public void StageAvatarFrameMode(int seat, AvatarFrameDisplayMode mode)
    {
        var slot = SlotAt(seat);
        _revision++;
        slot.AvatarFrameMode = mode;
        slot.AvatarFrameDirty = true;
    }

    public void StageTeamName(int seat, string value)
    {
        var slot = SlotAt(seat);
        _revision++;
        slot.TeamName = value;
        slot.TeamNameDirty = true;
        slot.RestoreTeamFromApi = false;
    }

    public void StageRestoreTeam(int seat)
    {
        var slot = SlotAt(seat);
        _revision++;
        slot.TeamNameDirty = true;
        slot.RestoreTeamFromApi = true;
    }

    public void Apply(V27Settings settings) => ApplyTo(settings, true);

    public void CopyTo(V27Settings settings) => ApplyTo(settings, false);

    private void ApplyTo(V27Settings settings, bool clearDraft)
    {
        settings.Normalize();
        foreach (var player in settings.Players)
        {
            var slot = SlotAt(player.Seat);
            if (slot.NameDirty)
            {
                player.NameLocked = !slot.RestoreNameFromApi;
                if (!slot.RestoreNameFromApi)
                    player.ManualName = slot.Name;
            }
            if (slot.ScoreDirty)
            {
                player.ScoreLocked = !slot.RestoreScoreFromApi;
                if (!slot.RestoreScoreFromApi)
                    player.ManualScore = slot.Score;
            }
            if (slot.AvatarDirty)
            {
                if (slot.AvatarPath is not null)
                    player.AvatarPath = slot.AvatarPath;
                if (slot.AvatarMode is not null)
                    player.AvatarMode = slot.AvatarMode.Value;
            }
            if (slot.AvatarFrameDirty)
            {
                if (slot.AvatarFramePath is not null)
                    player.AvatarFramePath = slot.AvatarFramePath;
                if (slot.AvatarFrameMode is not null)
                    player.AvatarFrameMode = slot.AvatarFrameMode.Value;
            }
            if (slot.TeamNameDirty)
            {
                player.TeamNameLocked = !slot.RestoreTeamFromApi;
                if (!slot.RestoreTeamFromApi) player.TeamName = slot.TeamName.Trim();
            }
            if (clearDraft) _slots[player.Seat] = new Slot();
        }
    }

    private Slot SlotAt(int seat) => seat is >= 0 and <= 3
        ? _slots[seat]
        : throw new ArgumentOutOfRangeException(nameof(seat));

    private sealed class Slot
    {
        public bool NameDirty { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool RestoreNameFromApi { get; set; }
        public bool ScoreDirty { get; set; }
        public int Score { get; set; }
        public bool RestoreScoreFromApi { get; set; }
        public bool AvatarDirty { get; set; }
        public string? AvatarPath { get; set; }
        public AvatarDisplayMode? AvatarMode { get; set; }
        public bool AvatarFrameDirty { get; set; }
        public string? AvatarFramePath { get; set; }
        public AvatarFrameDisplayMode? AvatarFrameMode { get; set; }
        public bool TeamNameDirty { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public bool RestoreTeamFromApi { get; set; }
    }
}
