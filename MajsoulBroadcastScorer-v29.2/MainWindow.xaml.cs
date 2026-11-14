using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using MajsoulBroadcastScorer.Core.V27;
using MajsoulBroadcastScorer.V27.Services;

namespace MajsoulBroadcastScorer.V27;

public partial class MainWindow : Window
{
    private readonly SettingsStore _settingsStore;
    private readonly BroadcastApiClient _apiClient;
    private readonly ObsOverlayServer _obsServer;
    private readonly BackendProcessManager _backendManager;
    private V27Settings _settings;
    private bool _savingChanges;
    private DateTimeOffset? _lastManualSaveAt;
    private readonly OverlayStylePublication _stylePublication;
    private readonly VerticalCardPublication _verticalCardPublication;
    private readonly LiveStateReducer _reducer;
    private readonly LiveStateReducer _reportReducer;
    private readonly TotalPointsDraft _totalPointsDraft;
    private readonly PlayerPresentationDraft _playerPresentationDraft = new();
    private readonly MatchScoreReportRecorder _scoreReportRecorder;
    private readonly ObservableCollection<PlayerEditorViewModel> _playerEditors = [];
    private readonly ObservableCollection<ContestGameOption> _contestGames = [];
    private readonly DelayedStateQueue<BroadcastApiState> _apiStateQueue = new();
    private readonly DispatcherTimer _apiDelayTimer;
    private readonly string?[] _rawScoreInputs = new string?[4];
    private readonly string?[] _rawTotalInputs = new string?[4];
    private readonly string?[] _avatarPreviewKeys = new string?[4];
    private bool _apiConnectionAvailable;
    private string _latestApiStatus = "观战 API 未连接";
    private DateTimeOffset? _lastApiReceivedAt;
    private DateTimeOffset? _lastPublishedAt;
    private string? _calculationKey;
    private IReadOnlyDictionary<int, ReversalPlayerResult> _calculationCache =
        new Dictionary<int, ReversalPlayerResult>();
    private bool _updatingUi;
    private bool _controlAvailable;
    private bool _controlProbeStarted;
    private bool _controlBusy;

    public MainWindow(
        SettingsStore settingsStore,
        V27Settings settings,
        BroadcastApiClient apiClient,
        ObsOverlayServer obsServer,
        BackendProcessManager backendManager)
    {
        _settingsStore = settingsStore;
        _settings = settings;
        _settings.Normalize();
        if (_settings.OverlayStyle == OverlayLayoutStyle.HorizontalPlayers)
            _settings.OverlayStyle = OverlayLayoutStyle.VerticalAvatar;
        foreach (var player in _settings.Players)
            player.AvatarMode = AvatarDisplayMode.Image;
        _stylePublication = new OverlayStylePublication(_settings.OverlayStyle);
        _verticalCardPublication = new VerticalCardPublication(_settings);
        _apiClient = apiClient;
        _obsServer = obsServer;
        _backendManager = backendManager;
        _reducer = new LiveStateReducer(_settings);
        _reportReducer = new LiveStateReducer(_settings);
        _totalPointsDraft = new TotalPointsDraft(_settings);
        _scoreReportRecorder = new MatchScoreReportRecorder(Path.Combine(AppContext.BaseDirectory, "reports"));
        _apiDelayTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _apiDelayTimer.Tick += ApiDelayTimer_Tick;

        InitializeComponent();
        for (var seat = 0; seat < 4; seat++)
            _playerEditors.Add(new PlayerEditorViewModel(seat, PlayerEditorChanged));
        PlayerEditorsControl.ItemsSource = _playerEditors;
        ContestGamesList.ItemsSource = _contestGames;
        RoundRecordsList.ItemsSource = _roundReportRows;
        InitializeControls();

        _apiClient.StatusChanged += ApiClient_StatusChanged;
        _apiClient.StateChanged += ApiClient_StateChanged;
        _apiClient.AuthChanged += ApiClient_AuthChanged;
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
        _apiDelayTimer.Start();
        RefreshAll();
    }

    private void InitializeControls()
    {
        _updatingUi = true;
        DepartmentTextBox.Text = _settings.Department;
        MatchTextBox.Text = _settings.MatchText;
        AccountNameTextBox.Text = _settings.AccountName;
        ContestIdTextBox.Text = _settings.ContestId;
        BackendStatusText.Text = "观战后台：" + _backendManager.Status;
        ApiDelayTextBox.Text = _settings.ApiDisplayDelaySeconds.ToString("0.##", CultureInfo.CurrentCulture);
        OverlayStyleComboBox.SelectedValue = _stylePublication.PreviewStyle switch
        {
            OverlayLayoutStyle.VerticalAvatar => "竖排头像",
            OverlayLayoutStyle.FrierenVerticalAvatar => "竖排头像·芙莉莲",
            OverlayLayoutStyle.VerticalMatch => "竖排头像·赛事展示",
            OverlayLayoutStyle.TeamMode => "队伍模式",
            OverlayLayoutStyle.GapConditions => "分差与逆转",
            _ => "竖排头像"
        };
        GapTargetComboBox.SelectedValue = _settings.GapTargetMode switch
        {
            ReversalTargetMode.TopTwo => "逆转前二",
            ReversalTargetMode.FirstPlace => "逆转一位",
            _ => "逆转上一位"
        };
        ShowTotalCheckBox.IsChecked = _settings.ShowTotalPoints;
        VerticalNameComboBox.SelectedValue = _verticalCardPublication.Preview.Name == VerticalNameContent.TeamName ? "所属队伍" : "选手姓名";
        VerticalNumberComboBox.SelectedValue = _verticalCardPublication.Preview.Number == VerticalNumberContent.CurrentScore ? "当前分数（持有点数）" : "精算点";
        WriteLeftBoardFocusControls();
        WriteLayoutControls();
        UpdateConfirmChangesButton();
        _updatingUi = false;
        UpdateStyleControls();
        UpdateControlButtons();
    }

    private void ApiClient_StatusChanged(object? sender, string status) => Dispatcher.BeginInvoke(() =>
    {
        _latestApiStatus = status;
        _apiConnectionAvailable = _apiClient.IsConnected;
        _reducer.SetConnection(_apiConnectionAvailable, status);
        UpdateStatusOnly(_reducer.Current);
        if (!_apiConnectionAvailable)
        {
            _controlAvailable = false;
            _controlProbeStarted = false;
            SetAuthenticationState(false);
        }
        else if (!_controlProbeStarted)
        {
            _ = ProbeBackendControlAsync();
        }
    });

    private void ApiClient_AuthChanged(object? sender, BroadcastAuthState auth) => Dispatcher.BeginInvoke(() =>
    {
        AccountStatusText.Text = auth.Account is null
            ? $"雀魂账号：未登录（连接 {auth.Connection}）"
            : $"雀魂账号：{auth.Account.Nickname}（ID {auth.Account.AccountId}）";
        SetAuthenticationState(_apiConnectionAvailable && auth.Account is not null && auth.Connection == "connected");
    });

    private void ApiClient_StateChanged(object? sender, BroadcastApiState state) => Dispatcher.BeginInvoke(() =>
    {
        _lastApiReceivedAt = DateTimeOffset.Now;
        ObserveRoundDetails(state);
        try
        {
            if (_reportReducer.Apply(state))
            {
                var timestamp = state.ServerTime > 0
                    ? DateTimeOffset.FromUnixTimeMilliseconds(state.ServerTime)
                    : DateTimeOffset.Now;
                _scoreReportRecorder.Observe(
                    _reportReducer.Current,
                    "API",
                    timestamp,
                    state.Observe?.Ended == true);
            }
        }
        catch (InvalidDataException)
        {
            // 无效原始快照不进入正式报告，延迟播出侧会显示具体冻结原因。
        }
        catch (IOException exception)
        {
            ReportStatusText.Text = "报告写入失败：" + exception.Message;
        }
        var incomingGameUuid = state.Observe?.GameUuid;
        _apiStateQueue.ChangeScope(incomingGameUuid);
        _apiStateQueue.Enqueue(state, DateTimeOffset.UtcNow);
        if (!ApplyReadyApiStates())
            UpdateStatusOnly(_reducer.Current);
    });

    private void ApiDelayTimer_Tick(object? sender, EventArgs e) => ApplyReadyApiStates();

    private bool ApplyReadyApiStates()
    {
        var ready = _apiStateQueue.DrainReady(
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(_settings.ApiDisplayDelaySeconds));
        if (ready.Count == 0)
            return false;

        var bindingsChanged = false;
        foreach (var state in ready)
        {
            var beforeBindings = _settings.Players
                .Select(player => (player.Seat, player.AccountId, player.TotalPoints))
                .ToArray();
            try
            {
                if (_reducer.Apply(state, bindPlayerTotals: true))
                {
                    var currentSeatByAccount = _settings.Players
                        .Where(player => player.AccountId > 0)
                        .ToDictionary(player => player.AccountId, player => player.Seat);
                    var seatMoves = beforeBindings
                        .Where(player => player.AccountId > 0 &&
                            currentSeatByAccount.TryGetValue(player.AccountId, out var seat) &&
                            seat != player.Seat)
                        .ToDictionary(
                            player => player.Seat,
                            player => currentSeatByAccount[player.AccountId]);
                    if (seatMoves.Count > 0)
                    {
                        _totalPointsDraft.Rebind(seatMoves);
                        ReorderSeatInputs(_rawTotalInputs, seatMoves);
                    }
                    bindingsChanged |= !beforeBindings.SequenceEqual(_settings.Players
                        .Select(player => (player.Seat, player.AccountId, player.TotalPoints)));
                }
            }
            catch (InvalidDataException exception)
            {
                _reducer.SetConnection(true, "收到无效快照，已冻结最后数据：" + exception.Message);
            }
        }

        _reducer.SetConnection(_apiConnectionAvailable, _latestApiStatus);

        _lastPublishedAt = DateTimeOffset.Now;
        RefreshAll();
        if (bindingsChanged)
            _ = SaveRefreshAsync();
        return true;
    }

    private static void ReorderSeatInputs(string?[] values, IReadOnlyDictionary<int, int> seatMoves)
    {
        var previous = (string?[])values.Clone();
        for (var oldSeat = 0; oldSeat < values.Length; oldSeat++)
        {
            var newSeat = seatMoves.TryGetValue(oldSeat, out var movedSeat) ? movedSeat : oldSeat;
            values[newSeat] = previous[oldSeat];
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        CloseToolWindows();
        _apiDelayTimer.Stop();
        _apiDelayTimer.Tick -= ApiDelayTimer_Tick;
        _apiClient.StatusChanged -= ApiClient_StatusChanged;
        _apiClient.StateChanged -= ApiClient_StateChanged;
        _apiClient.AuthChanged -= ApiClient_AuthChanged;
        _apiStateQueue.Clear();
        try { _scoreReportRecorder.Dispose(); } catch (IOException) { }
        try { _roundReportRecorder.Dispose(); } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }

    private async Task ProbeBackendControlAsync()
    {
        _controlProbeStarted = true;
        BackendStatusText.Text = "观战后台：正在确认统一控制通道…";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        try
        {
            var reply = await _apiClient.SendControlAsync("host.status", cancellationToken: timeout.Token);
            _controlAvailable = reply.Ok;
            BackendStatusText.Text = reply.Ok
                ? "观战后台：统一控制已就绪"
                : "观战后台：数据可读，控制不可用（" + (reply.Error ?? "未知原因") + "）";
        }
        catch (Exception exception)
        {
            _controlAvailable = false;
            BackendStatusText.Text = "观战后台：数据可读，控制不可用（" + FriendlyControlError(exception) + "）";
        }
        UpdateControlButtons();
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        var accountName = AccountNameTextBox.Text.Trim();
        var password = PasswordInput.Password;
        if (accountName.Length == 0 || password.Length == 0)
        {
            ControlStatusText.Text = "请输入账号和密码。";
            return;
        }
        await RunControlActionAsync("正在登录…", async () =>
        {
            var reply = await _apiClient.SendControlAsync("auth.login", new { accountName, password });
            EnsureControlSuccess(reply);
            _settings.AccountName = accountName;
            SetAuthenticationState(true);
            PasswordInput.Clear();
            await _settingsStore.SaveAsync(_settings);
            ControlStatusText.Text = "登录成功。";
        });
    }

    private async void Logout_Click(object sender, RoutedEventArgs e) =>
        await RunControlActionAsync("正在退出登录…", async () =>
        {
            EnsureControlSuccess(await _apiClient.SendControlAsync("auth.logout"));
            SetAuthenticationState(false);
            PasswordInput.Clear();
            ControlStatusText.Text = "已退出登录。";
        });

    private async void FetchGames_Click(object sender, RoutedEventArgs e)
    {
        if (!_isAuthenticated)
        {
            ControlStatusText.Text = "请先登录，再输入大会 ID 获取对局。";
            return;
        }
        if (!int.TryParse(ContestIdTextBox.Text.Trim(), out var contestId) || contestId <= 0)
        {
            ControlStatusText.Text = "请输入有效的大会 ID。";
            return;
        }
        await RunControlActionAsync("正在获取观战列表…", async () =>
        {
            var reply = await _apiClient.SendControlAsync("contest.fetch", new { contestId });
            EnsureControlSuccess(reply);
            _contestGames.Clear();
            var overview = RequiredControlProperty(reply, "overview");
            var realtime = overview.TryGetProperty("realtimeObserve", out var realtimeValue) && realtimeValue.ValueKind == JsonValueKind.True;
            if (overview.TryGetProperty("games", out var games) && games.ValueKind == JsonValueKind.Array)
            {
                foreach (var game in games.EnumerateArray())
                {
                    var uuid = game.TryGetProperty("uuid", out var uuidValue) ? uuidValue.GetString() ?? string.Empty : string.Empty;
                    if (uuid.Length == 0) continue;
                    var names = game.TryGetProperty("players", out var players) && players.ValueKind == JsonValueKind.Array
                        ? string.Join(" / ", players.EnumerateArray().Select(PlayerNameFromGame).Where(name => name.Length > 0))
                        : "参赛选手待定";
                    var start = game.TryGetProperty("startTime", out var startValue) && startValue.TryGetInt64(out var seconds)
                        ? DateTimeOffset.FromUnixTimeSeconds(seconds).LocalDateTime.ToString("MM-dd HH:mm")
                        : "时间未知";
                    _contestGames.Add(new ContestGameOption(uuid, $"{start}　{names}"));
                }
            }
            _settings.ContestId = contestId.ToString(CultureInfo.InvariantCulture);
            await _settingsStore.SaveAsync(_settings);
            ObserveModeComboBox.Items.OfType<ComboBoxItem>()
                .First(item => Equals(item.Tag, "realtime")).IsEnabled = realtime;
            if (!realtime && ObserveModeComboBox.SelectedIndex == 1)
                ObserveModeComboBox.SelectedIndex = 0;
            ControlStatusText.Text = _contestGames.Count == 0
                ? "当前没有正在进行的对局。"
                : $"已找到 {_contestGames.Count} 场对局，请选择后开始观战。";
            ContestGamesList.SelectedIndex = _contestGames.Count > 0 ? 0 : -1;
        });
    }

    private async void StartObserve_Click(object sender, RoutedEventArgs e)
    {
        if (!_isAuthenticated)
        {
            ControlStatusText.Text = "请先登录并获取对局，再开始观战。";
            return;
        }
        if (ContestGamesList.SelectedItem is not ContestGameOption game)
        {
            ControlStatusText.Text = "请先选择一场对局。";
            return;
        }
        var mode = (ObserveModeComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "delayed";
        await RunControlActionAsync("正在进入观战…", async () =>
        {
            EnsureControlSuccess(await _apiClient.SendControlAsync("observe.start", new { gameUuid = game.Uuid, mode }));
            ControlStatusText.Text = mode == "realtime" ? "已开始实时观战。" : "已开始延迟观战。";
        });
    }

    private async void StopObserve_Click(object sender, RoutedEventArgs e) =>
        await RunControlActionAsync("正在停止观战…", async () =>
        {
            EnsureControlSuccess(await _apiClient.SendControlAsync("observe.stop"));
            ControlStatusText.Text = "已停止观战，OBS 保留最后有效数据。";
        });

    private async Task RunControlActionAsync(string progress, Func<Task> action)
    {
        if (!_controlAvailable || _controlBusy)
            return;
        _controlBusy = true;
        ControlStatusText.Text = progress;
        UpdateControlButtons();
        try { await action(); }
        catch (Exception exception) { ControlStatusText.Text = FriendlyControlError(exception); }
        finally
        {
            _controlBusy = false;
            UpdateControlButtons();
        }
    }

    private void UpdateControlButtons()
    {
        if (LoginButton is null) return;
        var enabled = _apiConnectionAvailable && _controlAvailable && !_controlBusy;
        LoginButton.IsEnabled = enabled && !_isAuthenticated;
        LogoutButton.IsEnabled = enabled && _isAuthenticated;
        AccountNameTextBox.IsEnabled = enabled && !_isAuthenticated;
        PasswordInput.IsEnabled = enabled && !_isAuthenticated;
        ContestAccessPanel.IsEnabled = enabled && _isAuthenticated;
        FetchGamesButton.IsEnabled = enabled && _isAuthenticated;
        StartObserveButton.IsEnabled = enabled && _isAuthenticated && ContestGamesList.SelectedItem is ContestGameOption;
        StopObserveButton.IsEnabled = enabled && _isAuthenticated;
    }

    private static void EnsureControlSuccess(ControlReply reply)
    {
        if (!reply.Ok)
            throw new InvalidOperationException(reply.Error ?? "后台操作失败。");
    }

    private static JsonElement RequiredControlProperty(ControlReply reply, string name)
    {
        EnsureControlSuccess(reply);
        if (reply.Data is not { } data || !data.TryGetProperty(name, out var value))
            throw new InvalidDataException("后台返回的数据不完整。");
        return value;
    }

    private static string PlayerNameFromGame(JsonElement player) =>
        player.TryGetProperty("nickname", out var nickname) ? nickname.GetString()?.Trim() ?? string.Empty : string.Empty;

    private static string FriendlyControlError(Exception exception) => exception switch
    {
        OperationCanceledException => "观战后台响应超时，请检查后台状态后重试。",
        _ => exception.Message
    };

    private void RefreshAll()
    {
        _reducer.UpdateSettings(_settings);
        var display = _reducer.Current;
        UpdateStatusOnly(display);
        UpdateGapPlayerOptions(display);
        UpdatePlayerEditors(display);
        UpdateAssetLabels();
        UpdateStyleControls();
        UpdateReportStatus();
        UpdateCumulativeSettlement(display);
        UpdateObs(display);
    }

    private void UpdateConnectionIndicator()
    {
        var connected = _apiConnectionAvailable && _isAuthenticated;
        ApiStatusText.Text = connected ? "已连接" : "未连接";
        ConnectionIndicator.Fill = connected
            ? System.Windows.Media.Brushes.LimeGreen
            : System.Windows.Media.Brushes.IndianRed;
    }

    private void UpdateStatusOnly(LiveDisplayState display)
    {
        var scoreUpdate = display.LastScoreUpdateUnixMs > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(display.LastScoreUpdateUnixMs).LocalDateTime.ToString("HH:mm:ss")
            : "等待变化";
        UpdateConnectionIndicator();
        ApiDataStatusText.Text = display.IsObserving
            ? $"数据：{display.RoundLabel}　{display.Honba} 本场　{display.RiichiSticks} 供托　" +
              $"{(display.Ended ? "半庄已结束" : "进行中")}　分数更新 {scoreUpdate}"
            : "数据：尚未收到有效观战画面，继续保留最后一次选手资料";
        ApiDelayStatusText.Text =
            $"当前 {_settings.ApiDisplayDelaySeconds:0.##} 秒　待播 {_apiStateQueue.Count} 条" +
            (_apiStateQueue.DroppedCount > 0 ? $"　已丢弃过旧数据 {_apiStateQueue.DroppedCount} 条" : string.Empty) +
            $"　收包 {FormatTime(_lastApiReceivedAt)}　播出 {FormatTime(_lastPublishedAt)}";
        ObsStatusText.Text = ObsOverlayServer.OverlayUrl;
    }

    private void UpdateStyleControls()
    {
        var previewStyle = _stylePublication.PreviewStyle;
        VerticalMatchSettingsGroup.Visibility = previewStyle == OverlayLayoutStyle.VerticalMatch ? Visibility.Visible : Visibility.Collapsed;
        GapSettingsGroup.Visibility = previewStyle == OverlayLayoutStyle.GapConditions
            ? Visibility.Visible
            : Visibility.Collapsed;
        EventCopyGroup.Visibility = Visibility.Collapsed;
        LiveAssetsGroup.Visibility = Visibility.Collapsed;
        OverlayStyleStatusText.Text = $"当前样式：{StyleName(_stylePublication.PublishedStyle)} · 选择后自动同步 OBS";
    }

    private static string StyleName(OverlayLayoutStyle style) => style switch
    {
        OverlayLayoutStyle.VerticalAvatar => "竖排头像",
            OverlayLayoutStyle.FrierenVerticalAvatar => "竖排头像·芙莉莲",
        OverlayLayoutStyle.VerticalMatch => "竖排头像·赛事展示",
        OverlayLayoutStyle.TeamMode => "队伍模式",
        OverlayLayoutStyle.GapConditions => "分差与逆转",
        _ => "竖排头像"
    };

    private void UpdateGapPlayerOptions(LiveDisplayState display)
    {
        var expected = display.Players.OrderBy(player => player.Seat)
            .Select(player => (player.Seat, Text: $"{player.Nickname}（位置 {player.Seat + 1}）"))
            .ToArray();
        var current = GapPlayerComboBox.Items.OfType<ComboBoxItem>()
            .Select(item => (Seat: item.Tag is int seat ? seat : -1, Text: item.Content?.ToString() ?? string.Empty))
            .ToArray();
        _updatingUi = true;
        if (!current.SequenceEqual(expected))
        {
            GapPlayerComboBox.Items.Clear();
            foreach (var player in expected)
            {
                GapPlayerComboBox.Items.Add(new ComboBoxItem { Content = player.Text, Tag = player.Seat });
            }
        }
        GapPlayerComboBox.SelectedValue = _settings.GapSelectedSeat;
        _updatingUi = false;
    }

    private void UpdatePlayerEditors(LiveDisplayState display)
    {
        foreach (var shown in display.Players)
        {
            var editor = _playerEditors[shown.Seat];
            var setting = _settings.Players[shown.Seat];
            editor.UpdateSilently(() =>
            {
                editor.Name = _playerPresentationDraft.GetName(shown.Seat, shown.Nickname);
                editor.Score = _rawScoreInputs[shown.Seat] ??
                    _playerPresentationDraft.GetScore(shown.Seat, shown.Score).ToString(CultureInfo.InvariantCulture);
                editor.Wind = WindText.FromCode(shown.Wind);
                editor.Total = _rawTotalInputs[shown.Seat] ?? FormatSigned(_totalPointsDraft.IsDirty(shown.Seat)
                    ? _totalPointsDraft.Get(shown.Seat)
                    : setting.TotalPoints);
                editor.RiichiMode = setting.RiichiMode switch
                {
                    RiichiDisplayMode.ForceOn => "强制 ON",
                    RiichiDisplayMode.ForceOff => "强制 OFF",
                    _ => "自动跟随 API"
                };
                var avatarPath = _playerPresentationDraft.GetAvatarPath(shown.Seat, setting.AvatarPath);
                var resolvedAvatar = _settingsStore.ResolveAsset(avatarPath);
                var avatarPreviewKey = resolvedAvatar is null ? string.Empty : $"{resolvedAvatar}|{VersionOf(resolvedAvatar)}";
                if (_avatarPreviewKeys[shown.Seat] != avatarPreviewKey)
                {
                    _avatarPreviewKeys[shown.Seat] = avatarPreviewKey;
                    editor.AvatarPreview = LoadBitmap(resolvedAvatar, 192);
                    editor.AvatarPreviewText = resolvedAvatar is null ? "未上传" : "无法预览";
                }
                editor.AvatarStatus = _playerPresentationDraft.IsAvatarDirty(shown.Seat)
                    ? "待更新"
                    : _settingsStore.ResolveAsset(avatarPath) is null ? "未上传" : "已上传";
                var frameMode = _playerPresentationDraft.GetAvatarFrameMode(
                    shown.Seat, setting.AvatarFrameMode);
                var framePath = _playerPresentationDraft.GetAvatarFramePath(
                    shown.Seat, setting.AvatarFramePath);
                editor.AvatarFrameMode = frameMode switch
                {
                    AvatarFrameDisplayMode.Custom => "自定义头像框",
                    AvatarFrameDisplayMode.Hidden => "隐藏头像框",
                    _ => "默认边框"
                };
                editor.AvatarFrameStatus = _playerPresentationDraft.IsAvatarFrameDirty(shown.Seat)
                    ? "待更新"
                    : frameMode != AvatarFrameDisplayMode.Custom
                        ? frameMode == AvatarFrameDisplayMode.Hidden ? "已隐藏" : "默认"
                        : _settingsStore.ResolveAsset(framePath) is null ? "未上传" : "已上传";
                editor.TeamName = _playerPresentationDraft.GetTeamName(shown.Seat, shown.TeamName);
                editor.TeamSource = _playerPresentationDraft.IsTeamNameDirty(shown.Seat)
                    ? _playerPresentationDraft.IsTeamRestorePending(shown.Seat) ? "待恢复 API" : "待更新"
                    : setting.TeamNameLocked ? "手动锁定" : string.IsNullOrWhiteSpace(shown.TeamName) ? "API 暂无队伍" : "API";
                editor.NameSource = _playerPresentationDraft.IsNameDirty(shown.Seat)
                    ? _playerPresentationDraft.IsNameRestorePending(shown.Seat) ? "待恢复 API" : "待更新"
                    : setting.NameLocked ? "手动" : "API";
                editor.ScoreSource = _playerPresentationDraft.IsScoreDirty(shown.Seat)
                    ? _playerPresentationDraft.IsScoreRestorePending(shown.Seat) ? "待恢复 API" : "待更新"
                    : setting.ScoreLocked ? "手动锁定（API不会更新）" : "API";
                editor.WindSource = setting.WindLocked ? "手动" : "API";
            });
        }
    }

    private void PlayerEditorChanged(PlayerEditorViewModel editor, string propertyName)
    {
        if (_updatingUi)
            return;
        var setting = _settings.Players[editor.Seat];
        switch (propertyName)
        {
            case nameof(PlayerEditorViewModel.Name):
                _playerPresentationDraft.StageName(editor.Seat, editor.Name);
                UpdateConfirmChangesButton();
                return;
            case nameof(PlayerEditorViewModel.Score):
                _rawScoreInputs[editor.Seat] = editor.Score;
                if (int.TryParse(editor.Score, out var score))
                    _playerPresentationDraft.StageScore(editor.Seat, score);
                UpdateConfirmChangesButton();
                UpdateInputStatus();
                return;
            case nameof(PlayerEditorViewModel.Wind):
                setting.WindLocked = true;
                setting.ManualWind = WindText.ToCode(editor.Wind);
                break;
            case nameof(PlayerEditorViewModel.Total):
                _rawTotalInputs[editor.Seat] = editor.Total;
                if (TryParseDecimal(editor.Total, out var total))
                    _totalPointsDraft.Set(editor.Seat, total);
                UpdateConfirmChangesButton();
                UpdateInputStatus();
                return;
            case nameof(PlayerEditorViewModel.RiichiMode):
                setting.RiichiMode = editor.RiichiMode switch
                {
                    "强制 ON" => RiichiDisplayMode.ForceOn,
                    "强制 OFF" => RiichiDisplayMode.ForceOff,
                    _ => RiichiDisplayMode.Auto
                };
                break;
            case nameof(PlayerEditorViewModel.AvatarFrameMode):
                _playerPresentationDraft.StageAvatarFrameMode(editor.Seat, editor.AvatarFrameMode switch
                {
                    "自定义头像框" => AvatarFrameDisplayMode.Custom,
                    "隐藏头像框" => AvatarFrameDisplayMode.Hidden,
                    _ => AvatarFrameDisplayMode.Default
                });
                UpdateConfirmChangesButton();
                return;
            case nameof(PlayerEditorViewModel.TeamName):
                _playerPresentationDraft.StageTeamName(editor.Seat, editor.TeamName);
                UpdateConfirmChangesButton();
                return;
            default:
                return;
        }
        SaveRefresh();
    }

    private async void ConfirmChanges_Click(object sender, RoutedEventArgs e)
    {
        if (_savingChanges) return;
        var invalidScoreSeat = Enumerable.Range(0, 4)
            .FirstOrDefault(seat => _rawScoreInputs[seat] is not null && !int.TryParse(_rawScoreInputs[seat], out _), -1);
        if (invalidScoreSeat >= 0)
        {
            PlayerInputStatusText.Text = $"位置 {invalidScoreSeat + 1} 的分数不是有效整数，请修正后再确认。";
            return;
        }
        var invalidTotalSeat = Enumerable.Range(0, 4)
            .FirstOrDefault(seat => _rawTotalInputs[seat] is not null && !TryParseDecimal(_rawTotalInputs[seat]!, out _), -1);
        if (invalidTotalSeat >= 0)
        {
            PlayerInputStatusText.Text = $"位置 {invalidTotalSeat + 1} 的累计精算点格式不正确，请修正后再确认。";
            return;
        }
        if (!HasPendingChanges()) return;
        _savingChanges = true;
        SetManualEditingEnabled(false);
        UpdateConfirmChangesButton();
        try
        {
            var saved = await ManualChangeCommitter.SaveAsync(_settings, _playerPresentationDraft,
                _totalPointsDraft, _settingsStore.SaveAsync);
            _settings = saved;
            Array.Fill(_rawScoreInputs, null);
            Array.Fill(_rawTotalInputs, null);
            _reducer.UpdateSettings(_settings);
            _reportReducer.UpdateSettings(_settings);
            _lastManualSaveAt = DateTimeOffset.Now;
            PlayerInputStatusText.Text = string.Empty;
            try
            {
                _scoreReportRecorder.Observe(_reportReducer.Current, "人工确认", DateTimeOffset.Now);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _roundReportError = "修改已保存，分数报告写入失败：" + exception.Message;
            }
            RefreshAll();
        }
        catch (Exception exception)
        {
            PlayerInputStatusText.Text = "保存失败，修改已保留，可再次确认：" + exception.Message;
        }
        finally
        {
            _savingChanges = false;
            SetManualEditingEnabled(true);
            UpdateConfirmChangesButton();
        }
    }

    private void SetManualEditingEnabled(bool enabled)
    {
        MainContentRoot.IsEnabled = enabled;
        foreach (var pair in _toolWindows)
            if (pair.Key != "preview") pair.Value.IsEnabled = enabled;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_savingChanges)
        {
            e.Cancel = true;
            PlayerInputStatusText.Text = "正在保存修改，请稍候再退出。";
            return;
        }
        _closingToolWindows = true;
    }

    private bool HasPendingChanges() =>
        _playerPresentationDraft.HasPendingChanges || _totalPointsDraft.HasPendingChanges ||
        _rawScoreInputs.Any(value => value is not null) || _rawTotalInputs.Any(value => value is not null);

    private void UpdateConfirmChangesButton()
    {
        if (ConfirmChangesButton is null) return;
        ConfirmChangesButton.IsEnabled = HasPendingChanges() && !_savingChanges;
        ConfirmChangesButton.Content = _savingChanges ? "保存中…" : "确认修改";
        ChangeStatusText.Text = _savingChanges ? "正在保存选手资料与累计精算点…"
            : HasPendingChanges() ? "有待确认修改；资料和精算点将一起更新。"
            : _lastManualSaveAt is { } saved ? $"修改已保存并同步 · {saved:HH:mm:ss}"
            : "暂无待确认修改";
    }

    private void UpdateInputStatus()
    {
        var invalidScore = Enumerable.Range(0, 4)
            .FirstOrDefault(seat => _rawScoreInputs[seat] is not null && !int.TryParse(_rawScoreInputs[seat], out _), -1);
        if (invalidScore >= 0)
        {
            PlayerInputStatusText.Text = $"位置 {invalidScore + 1} 的分数不是有效整数。当前输入会保留，但不会播出。";
            return;
        }
        var invalidTotal = Enumerable.Range(0, 4)
            .FirstOrDefault(seat => _rawTotalInputs[seat] is not null && !TryParseDecimal(_rawTotalInputs[seat]!, out _), -1);
        PlayerInputStatusText.Text = invalidTotal >= 0
            ? $"位置 {invalidTotal + 1} 的 START 精算点格式不正确。当前输入会保留，但不会播出。"
            : string.Empty;
    }

    private void StageProjectedTotals_Click(object sender, RoutedEventArgs e)
    {
        var display = _reducer.Current;
        if (!display.Ended)
        {
            CumulativeSettlementHintText.Text = "本半庄尚未结束，暂不能累计。";
            return;
        }
        if (_settings.IsSettlementApplied(display.GameUuid))
        {
            CumulativeSettlementHintText.Text = "本半庄已经计入累计，不能重复相加。";
            return;
        }
        if (_totalPointsDraft.SourceSettlementGameUuid is { } pendingGameUuid &&
            !string.Equals(pendingGameUuid, display.GameUuid, StringComparison.Ordinal))
        {
            CumulativeSettlementHintText.Text = "上一半庄结果尚未确认，请先点击“确认修改”。";
            return;
        }
        try
        {
            var projectedTotals = CalculateReversalResults(display)
                .ToDictionary(item => item.Key, item => item.Value.ProjectedTotalPoints);
            _totalPointsDraft.SetAll(projectedTotals, display.GameUuid);
            Array.Fill(_rawTotalInputs, null);
            UpdatePlayerEditors(display);
            UpdateConfirmChangesButton();
            UpdateCumulativeSettlement(display);
        }
        catch (ArgumentException)
        {
            MessageBox.Show(
                "请先确认四名选手的座位风和当前分数，再填入下一场累计。",
                "无法计算累计精算点",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void UpdateCumulativeSettlement(LiveDisplayState display)
    {
        if (StageProjectedTotalsButton is null || CumulativeSettlementHintText is null)
            return;
        StageProjectedTotalsButton.IsEnabled = false;
        if (!display.IsObserving || string.IsNullOrWhiteSpace(display.GameUuid))
        {
            StageProjectedTotalsButton.Content = "等待开始观战";
            CumulativeSettlementHintText.Text = "开始观战后，半庄结束时会在这里显示可累计结果。";
            return;
        }
        if (!display.Ended)
        {
            StageProjectedTotalsButton.Content = "等待半庄结束";
            CumulativeSettlementHintText.Text = "本半庄尚未结束，暂不能累计；同一半庄只计算一次。";
            return;
        }
        if (_settings.IsSettlementApplied(display.GameUuid))
        {
            StageProjectedTotalsButton.Content = "本半庄已累计";
            CumulativeSettlementHintText.Text = "本半庄已经计入累计，不能重复相加。如需更正，可在选手卡中手动修改后确认。";
            return;
        }
        if (_totalPointsDraft.SourceSettlementGameUuid is { } pendingGameUuid)
        {
            StageProjectedTotalsButton.Content = string.Equals(pendingGameUuid, display.GameUuid, StringComparison.Ordinal)
                ? "已填入，等待确认"
                : "请先确认上一半庄";
            CumulativeSettlementHintText.Text = string.Equals(pendingGameUuid, display.GameUuid, StringComparison.Ordinal)
                ? "本半庄结果已经填入，请点击“确认修改”完成累计。"
                : "上一半庄结果尚未确认，请先点击“确认修改”。";
            return;
        }

        var results = GetCachedReversalResults(display);
        if (results.Count != 4)
        {
            StageProjectedTotalsButton.Content = "等待完整终局数据";
            CumulativeSettlementHintText.Text = "半庄已结束，等待四名选手的座位风和最终分数。";
            return;
        }

        StageProjectedTotalsButton.IsEnabled = true;
        StageProjectedTotalsButton.Content = "填入本半庄累计结果";
        CumulativeSettlementHintText.Text = "本半庄只会累计一次：" + Environment.NewLine +
            string.Join(Environment.NewLine, display.Players.OrderBy(player => player.Seat).Select(player =>
            {
                var result = results[player.Seat];
                var name = string.IsNullOrWhiteSpace(player.Nickname)
                    ? $"选手 {player.Seat + 1}"
                    : player.Nickname;
                return $"{name}：本半庄 {FormatSigned(result.CurrentPoints)}，累计后 {FormatSigned(result.ProjectedTotalPoints)}";
            }));
    }

    private void RestoreApi_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: PlayerEditorViewModel editor } button)
            return;
        switch (button.Tag as string)
        {
            case "name":
                _playerPresentationDraft.StageRestoreName(editor.Seat);
                UpdatePlayerEditors(_reducer.Current);
                UpdateConfirmChangesButton();
                return;
            case "score":
                _rawScoreInputs[editor.Seat] = null;
                _playerPresentationDraft.StageRestoreScore(editor.Seat);
                UpdatePlayerEditors(_reducer.Current);
                UpdateConfirmChangesButton();
                return;
            case "team":
                _playerPresentationDraft.StageRestoreTeam(editor.Seat);
                UpdatePlayerEditors(_reducer.Current);
                UpdateConfirmChangesButton();
                return;
            case "wind":
                _settings.Players[editor.Seat].WindLocked = false;
                break;
        }
        SaveRefresh();
    }

    private async void UploadAvatar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: PlayerEditorViewModel editor }) return;
        var path = PickImage();
        if (path is null) return;
        try
        {
            var assetKey = $"avatar-{editor.Seat}-{DateTime.UtcNow.Ticks}";
            var pendingPath = await _settingsStore.ImportAssetAsync(path, assetKey);
            _playerPresentationDraft.StageAvatarPath(editor.Seat, pendingPath);
            _playerPresentationDraft.StageAvatarMode(editor.Seat, AvatarDisplayMode.Image);
            UpdatePlayerEditors(_reducer.Current);
            UpdateConfirmChangesButton();
        }
        catch (Exception exception) { MessageBox.Show(exception.Message, "上传头像失败"); }
    }

    private async void UploadAvatarFrame_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: PlayerEditorViewModel editor }) return;
        var path = PickImage();
        if (path is null) return;
        try
        {
            var assetKey = $"avatar-frame-{editor.Seat}-{DateTime.UtcNow.Ticks}";
            var pendingPath = await _settingsStore.ImportAssetAsync(path, assetKey);
            _playerPresentationDraft.StageAvatarFramePath(editor.Seat, pendingPath);
            _playerPresentationDraft.StageAvatarFrameMode(editor.Seat, AvatarFrameDisplayMode.Custom);
            UpdatePlayerEditors(_reducer.Current);
            UpdateConfirmChangesButton();
        }
        catch (Exception exception) { MessageBox.Show(exception.Message, "上传头像框失败"); }
    }

    private void SaveCurrentReport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var scoreSaved = _scoreReportRecorder.SaveCurrent(DateTimeOffset.Now);
            var roundsSaved = _roundReportRecorder.SaveCurrent();
            _roundReportError = null;
            ReportStatusText.Text = scoreSaved || roundsSaved
                ? "对局报告已保存；逐局详情为 CSV、JSON 和文本格式。"
                : "尚未收到可保存的对局数据。";
        }
        catch (IOException exception)
        {
            ReportStatusText.Text = "报告保存失败：" + exception.Message;
        }
    }

    private void OpenReportsFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_scoreReportRecorder.ReportDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                ArgumentList = { _scoreReportRecorder.ReportDirectory },
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            ReportStatusText.Text = "无法打开报告目录：" + exception.Message;
        }
    }

    private void UpdateReportStatus()
    {
        if (ReportStatusText is null)
            return;
        ReportStatusText.Text = _roundReportError ?? (string.IsNullOrWhiteSpace(_roundReportRecorder.GameUuid)
            ? "等待对局；逐局结算与分数记录保存在程序目录的 reports 文件夹。"
            : $"对局 {_roundReportRecorder.GameUuid}：已记录 {_roundReportRecorder.Count} 局结算、{_scoreReportRecorder.CurrentEntryCount} 条分数状态。");
    }

    private async void UploadAsset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string kind) return;
        var path = PickImage();
        if (path is null) return;
        try
        {
            var relative = await _settingsStore.ImportAssetAsync(path, kind);
            switch (kind)
            {
                case "left-board": _settings.LeftBoardPath = relative; break;
                case "logo":
                    _settings.LogoPath = relative;
                    ApplyOriginalAspect(_settings.LogoLayout, relative);
                    break;
                case "bottom-left":
                    _settings.BottomLeftPath = relative;
                    ApplyOriginalAspect(_settings.BottomLeftLayout, relative);
                    break;
            }
            await SaveRefreshAsync();
        }
        catch (Exception exception) { MessageBox.Show(exception.Message, "上传素材失败"); }
    }

    private static string? PickImage()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "图片|*.png;*.jpg;*.jpeg;*.webp;*.gif;*.bmp",
            CheckFileExists = true
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private void TextSettings_Changed(object sender, TextChangedEventArgs e)
    {
        if (_updatingUi) return;
        _settings.Department = DepartmentTextBox.Text;
        _settings.MatchText = MatchTextBox.Text;
        SaveRefresh();
    }

    private void OverlayStyle_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingUi || OverlayStyleComboBox.SelectedValue is not string value)
            return;
        var selectedStyle = value switch
        {
            "竖排头像" => OverlayLayoutStyle.VerticalAvatar,
            "竖排头像·芙莉莲" => OverlayLayoutStyle.FrierenVerticalAvatar,
            "竖排头像·赛事展示" => OverlayLayoutStyle.VerticalMatch,
            "队伍模式" => OverlayLayoutStyle.TeamMode,
            "分差与逆转" => OverlayLayoutStyle.GapConditions,
            _ => OverlayLayoutStyle.VerticalAvatar
        };
        _stylePublication.Select(selectedStyle);
        _stylePublication.Confirm();
        _settings.OverlayStyle = _stylePublication.PublishedStyle;
        SaveRefresh();
    }

    private void VerticalContent_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingUi) return;
        _verticalCardPublication.Select(
            VerticalNameComboBox.SelectedValue as string == "选手姓名" ? VerticalNameContent.PlayerName : VerticalNameContent.TeamName,
            VerticalNumberComboBox.SelectedValue as string == "精算点" ? VerticalNumberContent.CalculationPoints : VerticalNumberContent.CurrentScore);
        _verticalCardPublication.Confirm(_settings);
        SaveRefresh();
    }

    private void GapSettings_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingUi)
            return;
        if (GapPlayerComboBox.SelectedValue is int seat)
            _settings.GapSelectedSeat = seat;
        _settings.GapTargetMode = (GapTargetComboBox.SelectedValue as string) switch
        {
            "逆转前二" => ReversalTargetMode.TopTwo,
            "逆转一位" => ReversalTargetMode.FirstPlace,
            _ => ReversalTargetMode.PreviousRank
        };
        SaveRefresh();
    }

    private void ShowTotal_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingUi) return;
        _settings.ShowTotalPoints = ShowTotalCheckBox.IsChecked == true;
        SaveRefresh();
    }

    private void ApiDelayText_Changed(object sender, TextChangedEventArgs e)
    {
        if (_updatingUi || ApplyApiDelayButton is null)
            return;
        ApplyApiDelayButton.IsEnabled = true;
        ApplyApiDelayButton.Content = "应用延迟";
    }

    private async void ApplyApiDelay_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseApiDelay(ApiDelayTextBox.Text, out var delay))
        {
            MessageBox.Show("延迟时间请输入 0～30 之间的数字。", "无法应用延迟", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _settings.ApiDisplayDelaySeconds = delay;
        ApiDelayTextBox.Text = delay.ToString("0.##", CultureInfo.CurrentCulture);
        ApplyApiDelayButton.IsEnabled = false;
        ApplyApiDelayButton.Content = "延迟已应用";
        ApplyReadyApiStates();
        await SaveRefreshAsync();
    }

    private static bool TryParseApiDelay(string text, out double delay)
    {
        var parsed = double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out delay) ||
                     double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out delay);
        return parsed && double.IsFinite(delay) && delay is >= 0 and <= 30;
    }

    private void LayoutText_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingUi || LogoX is null) return;
        if (TryReadLayout(LogoX, LogoY, LogoW, LogoH, LogoLock, _settings.LogoLayout) |
            TryReadLayout(BottomX, BottomY, BottomW, BottomH, BottomLock, _settings.BottomLeftLayout))
            SaveRefresh();
    }

    private void LeftBoardFocus_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingUi || LeftBoardFocusXValue is null)
            return;
        _settings.LeftBoardFocusX = LeftBoardFocusXSlider.Value;
        _settings.LeftBoardFocusY = LeftBoardFocusYSlider.Value;
        UpdateLeftBoardFocusLabels();
        UpdateObs(_reducer.Current);
    }

    private void LeftBoardFocus_Commit(object sender, RoutedEventArgs e)
    {
        if (_updatingUi)
            return;
        SaveRefresh();
    }

    private void ResetAssetPosition_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string kind })
            return;
        switch (kind)
        {
            case "logo":
                _settings.ResetLogoPosition();
                WriteLayoutControls();
                break;
            case "bottom-left":
                _settings.ResetBottomLeftPosition();
                WriteLayoutControls();
                break;
            case "left-board":
                _settings.ResetLeftBoardFocus();
                WriteLeftBoardFocusControls();
                break;
            default:
                return;
        }
        SaveRefresh();
    }

    private static bool TryReadLayout(TextBox x, TextBox y, TextBox width, TextBox height, CheckBox locked, AssetLayout target)
    {
        if (!double.TryParse(x.Text, out var nextX) || !double.TryParse(y.Text, out var nextY) ||
            !double.TryParse(width.Text, out var nextWidth) || !double.TryParse(height.Text, out var nextHeight))
            return false;
        var lockRatio = locked.IsChecked == true;
        if (lockRatio && target.Width > 0 && target.Height > 0)
        {
            var ratio = target.Width / target.Height;
            if (nextWidth != target.Width && nextHeight == target.Height)
                nextHeight = nextWidth / ratio;
            else if (nextHeight != target.Height && nextWidth == target.Width)
                nextWidth = nextHeight * ratio;
        }
        var changed = target.X != nextX || target.Y != nextY || target.Width != nextWidth || target.Height != nextHeight || target.LockAspectRatio != lockRatio;
        if (!changed) return false;
        target.X = nextX;
        target.Y = nextY;
        target.Width = nextWidth;
        target.Height = nextHeight;
        target.LockAspectRatio = lockRatio;
        target.Clamp(1920, 1080);
        return true;
    }

    private void ApplyOriginalAspect(AssetLayout layout, string relativePath)
    {
        if (!layout.LockAspectRatio || LoadBitmap(_settingsStore.ResolveAsset(relativePath)) is not { } bitmap || bitmap.PixelHeight == 0)
            return;
        layout.Height = layout.Width * bitmap.PixelHeight / bitmap.PixelWidth;
        layout.Clamp(1920, 1080);
        WriteLayoutControls();
    }

    private void WriteLayoutControls()
    {
        _updatingUi = true;
        WriteLayout(_settings.LogoLayout, LogoX, LogoY, LogoW, LogoH, LogoLock);
        WriteLayout(_settings.BottomLeftLayout, BottomX, BottomY, BottomW, BottomH, BottomLock);
        _updatingUi = false;
    }

    private void WriteLeftBoardFocusControls()
    {
        _updatingUi = true;
        LeftBoardFocusXSlider.Value = _settings.LeftBoardFocusX;
        LeftBoardFocusYSlider.Value = _settings.LeftBoardFocusY;
        UpdateLeftBoardFocusLabels();
        _updatingUi = false;
    }

    private void UpdateLeftBoardFocusLabels()
    {
        LeftBoardFocusXValue.Text = $"{LeftBoardFocusXSlider.Value:0}%";
        LeftBoardFocusYValue.Text = $"{LeftBoardFocusYSlider.Value:0}%";
    }

    private static void WriteLayout(AssetLayout layout, TextBox x, TextBox y, TextBox width, TextBox height, CheckBox locked)
    {
        x.Text = Math.Round(layout.X).ToString(CultureInfo.InvariantCulture);
        y.Text = Math.Round(layout.Y).ToString(CultureInfo.InvariantCulture);
        width.Text = Math.Round(layout.Width).ToString(CultureInfo.InvariantCulture);
        height.Text = Math.Round(layout.Height).ToString(CultureInfo.InvariantCulture);
        locked.IsChecked = layout.LockAspectRatio;
    }

    private void UpdateAssetLabels()
    {
        LeftBoardStatus.Text = _settingsStore.ResolveAsset(_settings.LeftBoardPath) is null ? "未上传" : "已上传";
        LogoStatus.Text = _settingsStore.ResolveAsset(_settings.LogoPath) is null ? "未上传" : "已上传";
        BottomLeftStatus.Text = _settingsStore.ResolveAsset(_settings.BottomLeftPath) is null ? "未上传" : "已上传";
    }

    private async Task InitializePreviewAsync()
    {
        try
        {
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MajsoulBroadcastScorer-v0.292",
                "WebView2");
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
            if (_closingToolWindows) return;
            await PreviewWebView.EnsureCoreWebView2Async(environment);
            if (_closingToolWindows) return;
            PreviewWebView.DefaultBackgroundColor = System.Drawing.Color.Transparent;
            PreviewWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            PreviewWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            PreviewWebView.CoreWebView2.WebMessageReceived += PreviewWebMessageReceived;
            PreviewWebView.Source = new Uri(ObsOverlayServer.OverlayUrl + "?preview=1");
            PreviewErrorText.Visibility = Visibility.Collapsed;
            ResizePreview();
        }
        catch (Exception exception)
        {
            if (_closingToolWindows) return;
            PreviewErrorText.Text = "实时预览启动失败，但 OBS 页面仍可使用。请安装或修复 Microsoft Edge WebView2 Runtime。\n\n" + exception.Message;
            PreviewErrorText.Visibility = Visibility.Visible;
        }
    }

    private void PreviewHost_SizeChanged(object sender, SizeChangedEventArgs e) => ResizePreview();

    private void ResizePreview()
    {
        if (PreviewHost.ActualWidth <= 0 || PreviewHost.ActualHeight <= 0)
            return;
        const double ratio = 16d / 9d;
        if (PreviewHost.ActualWidth / PreviewHost.ActualHeight > ratio)
        {
            PreviewWebView.Height = PreviewHost.ActualHeight;
            PreviewWebView.Width = PreviewHost.ActualHeight * ratio;
        }
        else
        {
            PreviewWebView.Width = PreviewHost.ActualWidth;
            PreviewWebView.Height = PreviewHost.ActualWidth / ratio;
        }
    }

    private void PreviewWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var root = document.RootElement;
            if (root.GetProperty("type").GetString() != "asset-moved")
                return;
            var kind = root.GetProperty("kind").GetString();
            var layout = kind == "logo"
                ? _settings.LogoLayout
                : kind == "bottom-left" ? _settings.BottomLeftLayout : null;
            if (layout is null)
                return;
            layout.X = root.GetProperty("x").GetDouble();
            layout.Y = root.GetProperty("y").GetDouble();
            layout.Clamp(1920, 1080);
            WriteLayoutControls();
            SaveRefresh();
        }
        catch (JsonException)
        {
            // 忽略网页发来的无效拖动消息。
        }
    }

    private static BitmapImage? LoadBitmap(string? path, int decodePixelWidth = 0)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            if (decodePixelWidth > 0) bitmap.DecodePixelWidth = decodePixelWidth;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch { return null; }
    }

    private void UpdateObs(LiveDisplayState display)
    {
        var paths = new Dictionary<string, string?>
        {
            ["art-avatar-frame"] = Path.Combine(AppContext.BaseDirectory, "Assets", "Overlay", "mint-avatar-frame.png"),
            ["art-frieren-avatar-frame"] = Path.Combine(AppContext.BaseDirectory, "Assets", "Overlay", "frieren-avatar-frame.png"),
            ["art-name-divider"] = Path.Combine(AppContext.BaseDirectory, "Assets", "Overlay", "mint-name-divider.png"),
            ["left-board"] = _settingsStore.ResolveAsset(_settings.LeftBoardPath),
            ["logo"] = _settingsStore.ResolveAsset(_settings.LogoPath),
            ["bottom-left"] = _settingsStore.ResolveAsset(_settings.BottomLeftPath),
            ["font-source-han-serif-cn-digits"] = Path.Combine(
                AppContext.BaseDirectory, "Assets", "Fonts", "SourceHanSerifCN-Digits.woff2"),
            ["font-source-han-serif-cn-lower"] = Path.Combine(
                AppContext.BaseDirectory, "Assets", "Fonts", "SourceHanSerifCN-LatinLower.woff2")
        };
        IReadOnlyDictionary<int, ReversalPlayerResult> calculatedBySeat =
            new Dictionary<int, ReversalPlayerResult>();
        calculatedBySeat = GetCachedReversalResults(display);

        var players = new List<ObsPlayerState>();
        foreach (var player in display.Players)
        {
            var path = _settingsStore.ResolveAsset(player.AvatarPath);
            var framePath = _settingsStore.ResolveAsset(player.AvatarFramePath);
            paths[$"avatar-{player.Seat}"] = path;
            paths[$"avatar-frame-{player.Seat}"] = framePath;
            calculatedBySeat.TryGetValue(player.Seat, out var points);
            players.Add(new ObsPlayerState(
                player.Seat,
                WindText.FromCode(player.Wind),
                player.Nickname,
                player.TeamName,
                player.Score,
                player.TotalPoints,
                points?.CurrentPoints ?? 0,
                points?.ProjectedTotalPoints ?? player.TotalPoints,
                player.Riichi,
                false,
                path is not null,
                VersionOf(path),
                player.AvatarFrameMode switch
                {
                    AvatarFrameDisplayMode.Custom => "custom",
                    AvatarFrameDisplayMode.Hidden => "hidden",
                    _ => "default"
                },
                framePath is not null,
                VersionOf(framePath)));
        }

        IReadOnlyList<ObsReversalPlayerState> reversals = [];
        ObsSelectedReversalState? selectedReversal = null;
        if (_stylePublication.PublishedStyle == OverlayLayoutStyle.GapConditions ||
            _stylePublication.PreviewStyle == OverlayLayoutStyle.GapConditions)
        {
            var inputs = display.Players.OrderBy(player => player.Seat)
                .Select(player => new ReversalPlayerInput(
                    player.Seat,
                    player.Wind,
                    player.Score,
                    player.TotalPoints))
                .ToArray();
            try
            {
                var calculated = ReversalConditionCalculator.AnalyzeSelected(
                    inputs,
                    display.Honba,
                    display.RiichiSticks,
                    _settings.GapSelectedSeat,
                    _settings.GapTargetMode);
                selectedReversal = new ObsSelectedReversalState(
                    calculated.Seat,
                    calculated.Rank,
                    display.Players.Single(player => player.Seat == calculated.Seat).TotalPoints,
                    calculated.CurrentPoints,
                    calculated.ProjectedTotalPoints,
                    calculated.GapToTarget,
                    calculated.TargetSeat,
                    calculated.TargetSeat is null
                        ? _settings.GapTargetMode == ReversalTargetMode.TopTwo ? "保持前二" : "保持一位"
                        : _settings.GapTargetMode switch
                        {
                            ReversalTargetMode.TopTwo => "逆转前二",
                            ReversalTargetMode.FirstPlace => "逆转一位",
                            _ => "逆转上一位"
                        },
                    calculated.DirectRon?.Display,
                    calculated.OtherRon?.Display,
                    calculated.Tsumo?.Display,
                    calculated.SixYakumanInsufficient,
                    calculated.OpponentThresholds.Select(item => new ObsOpponentThresholdState(
                        item.OpponentSeat,
                        item.DropByRon is null
                            ? "-"
                            : calculated.TargetSeat is null && _settings.GapTargetMode == ReversalTargetMode.TopTwo
                                ? $"{item.DropByRon.Display} 掉出前二"
                                : $"{item.DropByRon.Display} 掉至{Math.Min(4, calculated.Rank + 1)}位",
                        calculated.TargetSeat is null
                            ? _settings.GapTargetMode == ReversalTargetMode.TopTwo
                                ? "任意和牌保持前二"
                                : "任意和牌保持一位"
                            : item.RonToTarget?.Display ?? "六倍役满仍不足")).ToArray());
            }
            catch (ArgumentException)
            {
                // 座位风尚未齐全时保留选手数据，新样式显示等待 API。
            }
        }

        ObsOverlayState CreateState(OverlayLayoutStyle style, bool preview) => new(
            style switch
            {
                OverlayLayoutStyle.VerticalAvatar => "verticalAvatar",
                OverlayLayoutStyle.FrierenVerticalAvatar => "frierenVerticalAvatar",
                OverlayLayoutStyle.VerticalMatch => "verticalMatch",
                OverlayLayoutStyle.TeamMode => "teamMode",
                OverlayLayoutStyle.GapConditions => "gapConditions",
                _ => "horizontalPlayers"
            },
            _settings.Department,
            _settings.MatchText,
            display.RoundLabel,
            display.Honba,
            display.RiichiSticks,
            display.ShowTotalPoints,
            paths["left-board"] is not null,
            VersionOf(paths["left-board"]),
            _settings.LeftBoardFocusX,
            _settings.LeftBoardFocusY,
            Layout(_settings.LogoLayout, paths["logo"]),
            Layout(_settings.BottomLeftLayout, paths["bottom-left"]),
            _settings.GapSelectedSeat,
            players,
            reversals,
            style == OverlayLayoutStyle.GapConditions ? selectedReversal : null)
        {
            VerticalNameMode = (preview ? _verticalCardPublication.Preview : _verticalCardPublication.Published).NameKey,
            VerticalNumberMode = (preview ? _verticalCardPublication.Preview : _verticalCardPublication.Published).NumberKey
        };

        _obsServer.Update(
            CreateState(_stylePublication.PublishedStyle, false),
            CreateState(_stylePublication.PreviewStyle, true),
            paths);
    }

    private static IReadOnlyDictionary<int, ReversalPlayerResult> CalculateReversalResults(LiveDisplayState display)
    {
        var inputs = display.Players.OrderBy(player => player.Seat)
            .Select(player => new ReversalPlayerInput(
                player.Seat,
                player.Wind,
                player.Score,
                player.TotalPoints))
            .ToArray();
        return ReversalConditionCalculator.Calculate(inputs, display.Honba, display.RiichiSticks)
            .ToDictionary(item => item.Seat);
    }

    private IReadOnlyDictionary<int, ReversalPlayerResult> GetCachedReversalResults(LiveDisplayState display)
    {
        var key = string.Join('|', new[] { display.Honba.ToString(), display.RiichiSticks.ToString() }
            .Concat(display.Players.OrderBy(player => player.Seat)
                .Select(player => $"{player.Seat}:{player.Wind}:{player.Score}:{player.TotalPoints}")));
        if (string.Equals(key, _calculationKey, StringComparison.Ordinal))
            return _calculationCache;

        _calculationKey = key;
        try { _calculationCache = CalculateReversalResults(display); }
        catch (ArgumentException) { _calculationCache = new Dictionary<int, ReversalPlayerResult>(); }
        return _calculationCache;
    }

    private static ObsAssetLayout Layout(AssetLayout layout, string? path) =>
        new(path is not null, VersionOf(path), layout.X, layout.Y, layout.Width, layout.Height);

    private static long VersionOf(string? path) => path is not null && File.Exists(path)
        ? File.GetLastWriteTimeUtc(path).Ticks
        : 0;

    private async void SaveRefresh() => await SaveRefreshAsync();

    private async Task SaveRefreshAsync()
    {
        try
        {
            await _settingsStore.SaveAsync(_settings);
            if (!Dispatcher.CheckAccess())
                await Dispatcher.InvokeAsync(RefreshAll);
            else
                RefreshAll();
        }
        catch (Exception exception)
        {
            if (!Dispatcher.CheckAccess())
                await Dispatcher.InvokeAsync(() => PlayerInputStatusText.Text = "设置保存失败：" + exception.Message);
            else
                PlayerInputStatusText.Text = "设置保存失败：" + exception.Message;
        }
    }

    private static string FormatSigned(decimal value) =>
        value.ToString(value >= 0 ? "+0.0" : "0.0", CultureInfo.InvariantCulture);

    private static string FormatTime(DateTimeOffset? value) => value?.ToString("HH:mm:ss") ?? "--:--:--";

    private static bool TryParseDecimal(string value, out decimal result) =>
        decimal.TryParse(value.Trim().Replace("pt", string.Empty, StringComparison.OrdinalIgnoreCase),
            NumberStyles.Number | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out result);
}

public sealed record ContestGameOption(string Uuid, string Label);
