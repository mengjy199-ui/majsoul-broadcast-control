using System.Collections.ObjectModel;
using System.IO;
using MajsoulBroadcastScorer.Core.V27;

namespace MajsoulBroadcastScorer.V27;

public partial class MainWindow
{
    private readonly MatchRoundReportRecorder _roundReportRecorder = new(Path.Combine(AppContext.BaseDirectory, "reports"));
    private readonly ObservableCollection<RoundReportRow> _roundReportRows = [];
    private string? _roundReportError;

    private void ObserveRoundDetails(BroadcastApiState state)
    {
        if (state.Observe is not { } observe) return;
        if (observe.RoundResultSchema != 1)
        {
            _roundReportError = "当前观战接口未提供逐局详情；请使用本版附带的观战后台。分数记录仍会保存。";
            UpdateReportStatus();
            return;
        }
        try
        {
            var timestamp = state.ServerTime > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(state.ServerTime)
                : DateTimeOffset.Now;
            if (_roundReportRecorder.Observe(observe, timestamp))
                RefreshRoundReportRows();
            if (!_roundReportRecorder.HasPendingSave) _roundReportError = null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            RefreshRoundReportRows();
            _roundReportError = "逐局报告尚未保存，正在保留数据等待重试：" + exception.Message;
        }
        UpdateReportStatus();
    }
    private void RefreshRoundReportRows()
    {
        _roundReportRows.Clear();
        foreach (var result in _roundReportRecorder.Results)
            _roundReportRows.Add(RoundResultFormatter.CreateRow(result, _roundReportRecorder.Players));
        RoundRecordsEmptyState.Visibility = _roundReportRows.Count == 0
            ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }

}
