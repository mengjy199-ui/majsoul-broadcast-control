using System.IO;
using System.Text.Json;
using System.Windows;
using MajsoulBroadcastScorer.Core.V27;

namespace MajsoulBroadcastScorer.V27;

public partial class MainWindow
{
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
            return display.Players.Select(player => player.AccountId).Where(id => id > 0).Distinct()
                .ToDictionary(id => id, id => (decimal?)_sanmaLedger.Total(contest, id,
                    display.Ended ? null : display.GameUuid));
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
        SanmaCurrentButton.IsEnabled = _settings.SanmaShowCumulative;
        SanmaCumulativeButton.IsEnabled = !_settings.SanmaShowCumulative;
        SanmaStatusText.Text = _sanmaSaveError ?? (display.Players.Count == 3
            ? "终局自动累计，每 1000 点显示为 1。"
            : "请在观战登录中选择三人麻将对局。");
    }

    private void SanmaDisplay_Click(object sender, RoutedEventArgs e)
    {
        _settings.SanmaShowCumulative = ReferenceEquals(sender, SanmaCumulativeButton);
        RefreshAll();
        SaveRefresh();
    }
}
