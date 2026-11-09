using MajsoulBroadcastScorer.Core.V27;

namespace MajsoulBroadcastScorer.Tests.V27;

public sealed class DelayedStateQueueTests
{
    [Fact]
    public void ZeroDelay_ReleasesStateImmediately()
    {
        var queue = new DelayedStateQueue<string>();
        var now = DateTimeOffset.UtcNow;
        queue.Enqueue("score", now);

        Assert.Equal(["score"], queue.DrainReady(now, TimeSpan.Zero));
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Delay_HoldsThenReleasesStatesInReceiveOrder()
    {
        var queue = new DelayedStateQueue<string>();
        var start = DateTimeOffset.UtcNow;
        queue.Enqueue("first", start);
        queue.Enqueue("second", start.AddMilliseconds(200));

        Assert.Empty(queue.DrainReady(start.AddSeconds(1.4), TimeSpan.FromSeconds(1.5)));
        Assert.Equal(["first"], queue.DrainReady(start.AddSeconds(1.5), TimeSpan.FromSeconds(1.5)));
        Assert.Equal(["second"], queue.DrainReady(start.AddSeconds(1.7), TimeSpan.FromSeconds(1.5)));
    }

    [Fact]
    public void ChangingDelay_ReevaluatesPendingStates()
    {
        var queue = new DelayedStateQueue<int>();
        var start = DateTimeOffset.UtcNow;
        queue.Enqueue(1, start);

        Assert.Empty(queue.DrainReady(start.AddSeconds(2), TimeSpan.FromSeconds(5)));
        Assert.Equal([1], queue.DrainReady(start.AddSeconds(2), TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Settings_NormalizeClampsDelayToSupportedRange()
    {
        var settings = new V27Settings { ApiDisplayDelaySeconds = 99 };
        settings.Normalize();
        Assert.Equal(30, settings.ApiDisplayDelaySeconds);

        settings.ApiDisplayDelaySeconds = -1;
        settings.Normalize();
        Assert.Equal(0, settings.ApiDisplayDelaySeconds);
    }

    [Fact]
    public void Capacity_DropsOldestStates_InsteadOfGrowingWithoutLimit()
    {
        var queue = new DelayedStateQueue<int>(2);
        var now = DateTimeOffset.UtcNow;
        queue.Enqueue(1, now);
        queue.Enqueue(2, now);
        queue.Enqueue(3, now);

        Assert.Equal(1, queue.DroppedCount);
        Assert.Equal([2, 3], queue.DrainReady(now, TimeSpan.Zero));
    }

    [Fact]
    public void ChangeScope_ClearsPendingStatesFromPreviousGame()
    {
        var queue = new DelayedStateQueue<string>();
        var now = DateTimeOffset.UtcNow;
        Assert.False(queue.ChangeScope("game-a"));
        queue.Enqueue("old-game", now);

        Assert.True(queue.ChangeScope("game-b"));
        queue.Enqueue("new-game", now);

        Assert.Equal(["new-game"], queue.DrainReady(now, TimeSpan.Zero));
    }
}
