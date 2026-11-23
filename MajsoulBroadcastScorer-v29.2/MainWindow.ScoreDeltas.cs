using System.IO;
using System.Text.Json;
using System.Windows;
using MajsoulBroadcastScorer.Core.V27;

namespace MajsoulBroadcastScorer.V27;

public partial class MainWindow
{
    private bool _sanmaGapChanged;
    private readonly ScoreDeltaLedger _sanmaLedger = new(Path.Combine(AppContext.BaseDirectory, "reports", "sanma-totals.json"), 3);
    private readonly ScoreDeltaLedger _fourPlayerLedger = new(Path.Combine(AppContext.BaseDirectory, "reports", "frieren-four-totals.json"), 4);
    private readonly Dictionary<string, string> _scoreDeltaContestByGame = new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> _scoreDeltaSaveErrors = [];
    private readonly Dictionary<(int PlayerCount, string Contest, long AccountId), (string Text, decimal RecordedPrevious)> _scoreDeltaPriorDrafts = [];

    private static int ScoreDeltaPlayerCount(OverlayLayoutStyle style) => style switch
    {
        OverlayLayoutStyle.FrierenSanma => 3,
        OverlayLayoutStyle.FrierenVerticalAvatar => 4,
        _ => 0
    };

    private ScoreDeltaLedger GetScoreDeltaLedger(int playerCount) => playerCount == 3 ? _sanmaLedger : _fourPlayerLedger;

    private void RecordScoreDeltaResult(BroadcastObserveState observe)
    {
        _scoreDeltaContestByGame.TryAdd(observe.GameUuid, _settings.ContestId);
        if (!observe.Ended) return;
        var playerCount = observe.Players.Count;
        try
        {
            GetScoreDeltaLedger(playerCount).Record(_scoreDeltaContestByGame[observe.GameUuid], observe);
            if (observe.Players.Any(player => player.AccountId <= 0))
                _scoreDeltaSaveErrors[playerCount] = "缺少选手账号，本半庄未计入累计。";
            else
                _scoreDeltaSaveErrors.Remove(playerCount);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            _scoreDeltaSaveErrors[playerCount] = "累计得失分保存失败。";
        }
    }

    private string ResolveScoreDeltaContest(LiveDisplayState display, ScoreDeltaLedger ledger) =>
        ledger.GetContest(display.GameUuid,
            _scoreDeltaContestByGame.GetValueOrDefault(display.GameUuid, _settings.ContestId));

    private Dictionary<long, decimal> GetScoreDeltaPreviousPoints(LiveDisplayState display)
    {
        var playerCount = display.Players.Count;
        var ledger = GetScoreDeltaLedger(playerCount);
        try
        {
            var contest = ResolveScoreDeltaContest(display, ledger);
            return display.Players.Where(player => player.AccountId > 0)
                .ToDictionary(player => player.AccountId, player =>
                    ledger.Total(contest, player.AccountId, display.GameUuid) +
                    _settings.ScoreDeltaPriorAdjustment(playerCount, contest, player.AccountId));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            _scoreDeltaSaveErrors[playerCount] = "累计得失分读取失败。";
            return [];
        }
    }

    private void UpdateScoreDeltaPriorEditor(PlayerEditorViewModel editor, LiveDisplayState display,
        DisplayPlayerState player, IReadOnlyDictionary<long, decimal> previous)
    {
        editor.TotalLabel = "此前累计得失分";
        editor.CanEditTotal = previous.TryGetValue(player.AccountId, out var value);
        if (!editor.CanEditTotal)
        {
            editor.Total = "—";
            return;
        }
        var playerCount = display.Players.Count;
        var key = (playerCount, ResolveScoreDeltaContest(display, GetScoreDeltaLedger(playerCount)), player.AccountId);
        editor.Total = _scoreDeltaPriorDrafts.TryGetValue(key, out var draft) ? draft.Text : FormatSigned(value);
    }

    private void StageScoreDeltaPrior(PlayerEditorViewModel editor)
    {
        var display = _reducer.Current;
        var player = display.Players.FirstOrDefault(player => player.Seat == editor.Seat);
        if (!editor.CanEditTotal || player is null || player.AccountId <= 0)
            return;
        var playerCount = display.Players.Count;
        var ledger = GetScoreDeltaLedger(playerCount);
        var contest = ResolveScoreDeltaContest(display, ledger);
        var key = (playerCount, contest, player.AccountId);
        var recorded = _scoreDeltaPriorDrafts.TryGetValue(key, out var existing) ? existing.RecordedPrevious
            : ledger.Total(contest, player.AccountId, display.GameUuid);
        _scoreDeltaPriorDrafts[key] = (editor.Total, recorded);
    }

    private Task SaveManualSettingsAsync(V27Settings candidate)
    {
        foreach (var (key, draft) in _scoreDeltaPriorDrafts)
        {
            if (!TryParseDecimal(draft.Text, out var prior))
                throw new InvalidDataException("此前累计得失分格式不正确。");
            candidate.SetScoreDeltaPriorAdjustment(key.PlayerCount, key.Contest, key.AccountId,
                Math.Round(prior, 1) - draft.RecordedPrevious);
        }
        return _settingsStore.SaveAsync(candidate);
    }

    private void UpdateScoreDeltaControls(LiveDisplayState display)
    {
        var playerCount = ScoreDeltaPlayerCount(_stylePublication.PublishedStyle);
        var sanmaStyle = playerCount == 3;
        ScoreDeltaDisplayControls.Visibility = playerCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        SanmaGapControls.Visibility = sanmaStyle ? Visibility.Visible : Visibility.Collapsed;
        SanmaThisGameButton.Visibility = sanmaStyle ? Visibility.Visible : Visibility.Collapsed;
        var legacyTotals = playerCount == 0 && display.Players.Count == 4;
        ShowTotalCheckBox.Visibility = legacyTotals ? Visibility.Visible : Visibility.Collapsed;
        if (playerCount == 0) return;

        var tableName = sanmaStyle ? "三人" : "四人";
        if (display.Players.Count != playerCount)
            OverlayStyleStatusText.Text = $"请在「观战登录」选择{tableName}对局。";
        CurrentScoreButton.IsEnabled = sanmaStyle ? _settings.SanmaNumber != SanmaNumberContent.CurrentScore
            : _settings.FrierenFourShowCumulative;
        SanmaThisGameButton.IsEnabled = _settings.SanmaNumber != SanmaNumberContent.ThisGame;
        CumulativeScoreDeltaButton.IsEnabled = sanmaStyle ? _settings.SanmaNumber != SanmaNumberContent.Cumulative
            : !_settings.FrierenFourShowCumulative;
        ScoreDeltaStatusText.Text = _scoreDeltaSaveErrors.GetValueOrDefault(playerCount) ??
            (display.Players.Count == playerCount && display.Players.Any(player => player.AccountId > 0)
                ? "此前累计可手填，确认后生效。终局自动入账。"
                : $"开始{tableName}观战后可填写此前累计。");
    }

    private void ScoreDeltaDisplay_Click(object sender, RoutedEventArgs e)
    {
        var cumulative = ReferenceEquals(sender, CumulativeScoreDeltaButton);
        if (_stylePublication.PublishedStyle == OverlayLayoutStyle.FrierenSanma)
        {
            _settings.SanmaNumber = cumulative ? SanmaNumberContent.Cumulative
                : ReferenceEquals(sender, SanmaThisGameButton) ? SanmaNumberContent.ThisGame
                : SanmaNumberContent.CurrentScore;
            _settings.SanmaShowCumulative = cumulative;
        }
        else
            _settings.FrierenFourShowCumulative = cumulative;
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
