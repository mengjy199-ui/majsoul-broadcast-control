using System.IO;
using System.Text.Json;
using System.Windows;
using MajsoulBroadcastScorer.Core.V27;

namespace MajsoulBroadcastScorer.V27;

public partial class MainWindow
{
    private bool _sanmaGapChanged;

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

    private Dictionary<long, decimal?> GetSanmaTotals(LiveDisplayState display)
    {
        try
        {
            var contest = _sanmaLedger.GetContest(display.GameUuid,
                _sanmaContestByGame.GetValueOrDefault(display.GameUuid, _settings.ContestId));
            return display.Players.Where(player => player.AccountId > 0)
                .ToDictionary(player => player.AccountId, player => (decimal?)_sanmaLedger.Total(
                    contest, player.AccountId, display.Ended ? null : display.GameUuid));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            _sanmaSaveError = "三人南累计读取失败。";
            return [];
        }
    }

    private void UpdateSanmaControls(LiveDisplayState display)
    {
        var sanmaStyle = _stylePublication.PublishedStyle == OverlayLayoutStyle.FrierenSanma;
        SanmaDisplayControls.Visibility = sanmaStyle ? Visibility.Visible : Visibility.Collapsed;
        var legacyTotals = !sanmaStyle && display.Players.Count == 4;
        ShowTotalCheckBox.Visibility = legacyTotals ? Visibility.Visible : Visibility.Collapsed;
        PlayerEditorsControl.Tag = legacyTotals ? Visibility.Visible : Visibility.Collapsed;
        SanmaCurrentButton.IsEnabled = _settings.SanmaNumber != SanmaNumberContent.CurrentScore;
        SanmaThisGameButton.IsEnabled = _settings.SanmaNumber != SanmaNumberContent.ThisGame;
        SanmaCumulativeButton.IsEnabled = _settings.SanmaNumber != SanmaNumberContent.Cumulative;
        SanmaStatusText.Text = _sanmaSaveError ?? (display.Players.Count == 3
            ? "局中实时更新，终局自动记入累计。"
            : "请在观战登录中选择三人麻将对局。");
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
