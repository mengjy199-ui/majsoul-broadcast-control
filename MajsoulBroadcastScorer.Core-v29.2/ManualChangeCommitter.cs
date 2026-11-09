using System.Text.Json;

namespace MajsoulBroadcastScorer.Core.V27;

public static class ManualChangeCommitter
{
    public static async Task<V27Settings> SaveAsync(
        V27Settings current,
        PlayerPresentationDraft presentation,
        TotalPointsDraft totals,
        Func<V27Settings, Task> persist)
    {
        ArgumentNullException.ThrowIfNull(persist);
        var presentationRevision = presentation.Revision;
        var totalsRevision = totals.Revision;
        var candidate = V27SettingsJson.Deserialize(JsonSerializer.Serialize(current, V27SettingsJson.Options));
        presentation.CopyTo(candidate);
        totals.CopyTo(candidate);
        await persist(candidate);
        // 草稿只在文件保存成功后清除，保存失败可直接重试。
        presentation.AcceptIfUnchanged(presentationRevision);
        totals.AcceptIfUnchanged(totalsRevision);
        return candidate;
    }
}
