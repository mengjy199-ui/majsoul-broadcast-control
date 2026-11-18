using System.IO;
using System.Text.Json;
using System.Windows;
using MajsoulBroadcastScorer.Core.V27;

namespace MajsoulBroadcastScorer.V27;

public partial class MainWindow
{
    private bool _sanmaGapChanged;
    private readonly Dictionary<(string Contest, long AccountId), (string Text, decimal RecordedPrevious)> _sanmaPriorDrafts = [];

    private void RecordSanmaResult(BroadcastObserveState observe)
    {
        _sanmaContestByGame.TryAdd(observe.GameUuid, _settings.ContestId);
        if (!observe.Ended) return;
        try
        {
            _sanmaLedger.Record(_sanmaContestByGame[observe.GameUuid], observe);
            _sanmaSaveError = observe.Players.Any(player => player.AccountId <= 0)
                ? "缺少选手账号，本半庄未计入累计。"
                : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            _sanmaSaveError = "三人南累计保存失败。";
        }
    }

    private string ResolveSanmaContest(LiveDisplayState display) =>
        _sanmaLedger.GetContest(display.GameUuid,
            _sanmaContestByGame.GetValueOrDefault(display.GameUuid, _settings.ContestId));

    private Dictionary<long, decimal> GetSanmaPreviousPoints(LiveDisplayState display)
    {
        try
        {
            var contest = ResolveSanmaContest(display);
            return display.Players.Where(player => player.AccountId > 0)
                .ToDictionary(player => player.AccountId, player =>
                    _sanmaLedger.Total(contest, player.AccountId, display.GameUuid) +
                    _settings.SanmaPriorAdjustment(contest, player.AccountId));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            _sanmaSaveError = "三人南累计读取失败。";
            return [];
        }
    }

    private void UpdateSanmaPriorEditor(PlayerEditorViewModel editor, LiveDisplayState display,
        DisplayPlayerState player, IReadOnlyDictionary<long, decimal> previous)
    {
        editor.TotalLabel = "此前累计得失分";
        editor.CanEditTotal = previous.TryGetValue(player.AccountId, out var value);
        if (!editor.CanEditTotal)
        {
            editor.Total = "—";
            return;
        }
        var key = (Contest: ResolveSanmaContest(display), player.AccountId);
        editor.Total = _sanmaPriorDrafts.TryGetValue(key, out var draft) ? draft.Text : FormatSigned(value);
    }

    private void StageSanmaPrior(PlayerEditorViewModel editor)
    {
        var display = _reducer.Current;
        var player = display.Players.FirstOrDefault(player => player.Seat == editor.Seat);
        if (!editor.CanEditTotal || player is null || player.AccountId <= 0)
            return;
        var key = (Contest: ResolveSanmaContest(display), player.AccountId);
        var recorded = _sanmaPriorDrafts.TryGetValue(key, out var existing) ? existing.RecordedPrevious
            : _sanmaLedger.Total(key.Contest, player.AccountId, display.GameUuid);
        _sanmaPriorDrafts[key] = (editor.Total, recorded);
    }

    private Task SaveManualSettingsAsync(V27Settings candidate)
    {
        foreach (var (key, draft) in _sanmaPriorDrafts)
        {
            if (!TryParseDecimal(draft.Text, out var prior))
                throw new InvalidDataException("此前累计得失分格式不正确。");
            candidate.SetSanmaPriorAdjustment(key.Contest, key.AccountId, Math.Round(prior, 1) - draft.RecordedPrevious);
        }
        return _settingsStore.SaveAsync(candidate);
    }

    private void UpdateSanmaControls(LiveDisplayState display)
    {
        var sanmaStyle = _stylePublication.PublishedStyle == OverlayLayoutStyle.FrierenSanma;
        SanmaDisplayControls.Visibility = sanmaStyle ? Visibility.Visible : Visibility.Collapsed;
        var legacyTotals = !sanmaStyle && display.Players.Count == 4;
        ShowTotalCheckBox.Visibility = legacyTotals ? Visibility.Visible : Visibility.Collapsed;
        if (_stylePublication.PublishedStyle == OverlayLayoutStyle.FrierenVerticalAvatar && display.Players.Count == 3)
            OverlayStyleStatusText.Text = "当前是三人桌观战。请在「观战登录」选择四人对局。";
        SanmaCurrentButton.IsEnabled = _settings.SanmaNumber != SanmaNumberContent.CurrentScore;
        SanmaThisGameButton.IsEnabled = _settings.SanmaNumber != SanmaNumberContent.ThisGame;
        SanmaCumulativeButton.IsEnabled = _settings.SanmaNumber != SanmaNumberContent.Cumulative;
        SanmaStatusText.Text = _sanmaSaveError ?? (display.Players.Count == 3
            ? "此前累计可手填，确认后生效。终局自动入账。"
            : "开始三人观战后可填写此前累计。");
    }

    private void SanmaDisplay_Click(object sender, RoutedEventArgs e)
    {
        _settings.SanmaNumber = ReferenceEquals(sender, SanmaCumulativeButton) ? SanmaNumberContent.Cumulative
            : ReferenceEquals(sender, SanmaThisGameButton) ? SanmaNumberContent.ThisGame
            : SanmaNumberContent.CurrentScore;
        _settings.SanmaShowCumulative = _settings.SanmaNumber == SanmaNumberContent.Cumulative;
        RefreshAll();
        SaveRefresh();
    }

    private void SanmaGap_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingUi || SanmaGapValue is null)
            return;
        _settings.SanmaPlayerGap = (int)Math.Round(e.NewValue);
        SanmaGapValue.Text = $"{_settings.SanmaPlayerGap} px";
        _sanmaGapChanged = true;
        UpdateObs(_reducer.Current);
    }

    private void SanmaGap_Commit(object sender, RoutedEventArgs e)
    {
        if (!_sanmaGapChanged)
            return;
        _sanmaGapChanged = false;
        SaveRefresh();
    }
}
