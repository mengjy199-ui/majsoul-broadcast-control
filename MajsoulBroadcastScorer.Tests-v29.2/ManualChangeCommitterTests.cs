using MajsoulBroadcastScorer.Core.V27;

namespace MajsoulBroadcastScorer.Tests.V27;

public sealed class ManualChangeCommitterTests
{
    [Fact]
    public async Task EditDuringSaveStaysPendingAndDoesNotChangeSavedCandidate()
    {
        var current = new V27Settings();
        var presentation = new PlayerPresentationDraft();
        var totals = new TotalPointsDraft(current);
        presentation.StageName(0, "本次保存");
        totals.Set(0, 12.3m);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = ManualChangeCommitter.SaveAsync(current, presentation, totals, _ => completion.Task);
        presentation.StageName(0, "后来上传或修改");
        totals.Set(0, 20m);
        completion.SetResult();
        var saved = await pending;
        Assert.Equal("本次保存", saved.Players[0].ManualName);
        Assert.Equal(12.3m, saved.Players[0].TotalPoints);
        Assert.True(presentation.HasPendingChanges);
        Assert.True(totals.HasPendingChanges);
        Assert.Equal("后来上传或修改", presentation.GetName(0, string.Empty));
        Assert.Equal(20m, totals.Get(0));
    }
    [Fact]
    public async Task FailedSaveKeepsBothDraftsAndCurrentSettings()
    {
        var current = new V27Settings();
        var presentation = new PlayerPresentationDraft();
        var totals = new TotalPointsDraft(current);
        presentation.StageName(0, "待保存昵称");
        totals.Set(0, 12.3m);
        await Assert.ThrowsAsync<IOException>(() => ManualChangeCommitter.SaveAsync(current, presentation, totals,
            _ => Task.FromException(new IOException("fixture: no disk space"))));
        Assert.True(presentation.HasPendingChanges);
        Assert.True(totals.HasPendingChanges);
        Assert.False(current.Players[0].NameLocked);
        Assert.Equal(0m, current.Players[0].TotalPoints);
    }

    [Fact]
    public async Task SuccessfulSaveContainsBothDraftsBeforeTheyAreCleared()
    {
        var current = new V27Settings();
        var presentation = new PlayerPresentationDraft();
        var totals = new TotalPointsDraft(current);
        presentation.StageName(0, "新昵称");
        presentation.StageScore(0, 32100);
        totals.Set(0, 12.3m);
        var candidate = await ManualChangeCommitter.SaveAsync(current, presentation, totals, settings =>
        {
            Assert.Equal("新昵称", settings.Players[0].ManualName);
            Assert.Equal(32100, settings.Players[0].ManualScore);
            Assert.Equal(12.3m, settings.Players[0].TotalPoints);
            Assert.True(presentation.HasPendingChanges);
            Assert.True(totals.HasPendingChanges);
            return Task.CompletedTask;
        });
        Assert.False(presentation.HasPendingChanges);
        Assert.False(totals.HasPendingChanges);
        Assert.Equal("新昵称", candidate.Players[0].ManualName);
        Assert.False(current.Players[0].NameLocked);
    }
}
