using System.Runtime.InteropServices;
using MajsoulBroadcastScorer.V27.Services;

namespace MajsoulBroadcastScorer.Tests.V27;

public sealed class ClipboardRetryTests
{
    [Fact]
    public async Task ClipboardBusy_RetriesUntilSuccess()
    {
        var calls = 0;

        var result = await ClipboardRetry.TrySetAsync(() =>
        {
            calls++;
            if (calls < 3)
                throw new ExternalException("clipboard busy");
        }, attempts: 4, delayMilliseconds: 1);

        Assert.True(result.Success);
        Assert.Equal(3, calls);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task ClipboardAlwaysBusy_ReturnsFailureInsteadOfThrowing()
    {
        var result = await ClipboardRetry.TrySetAsync(
            () => throw new ExternalException("clipboard busy"),
            attempts: 3,
            delayMilliseconds: 1);

        Assert.False(result.Success);
        Assert.IsType<ExternalException>(result.Error);
    }
}
