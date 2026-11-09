namespace MajsoulBroadcastScorer.Core.V27;

public sealed class DelayedStateQueue<T>
{
    private readonly Queue<Entry> _entries = new();
    private readonly int _maximumCount;
    private string? _scope;

    public DelayedStateQueue(int maximumCount = 512)
    {
        if (maximumCount < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumCount));
        _maximumCount = maximumCount;
    }

    public int Count => _entries.Count;
    public long DroppedCount { get; private set; }

    public bool ChangeScope(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope) || string.Equals(scope, _scope, StringComparison.Ordinal))
            return false;
        var changed = _scope is not null;
        _scope = scope;
        if (changed)
            Clear();
        return changed;
    }

    public void Enqueue(T value, DateTimeOffset receivedAt)
    {
        while (_entries.Count >= _maximumCount)
        {
            _entries.Dequeue();
            DroppedCount++;
        }
        _entries.Enqueue(new Entry(receivedAt, value));
    }

    public IReadOnlyList<T> DrainReady(DateTimeOffset now, TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delay));

        var ready = new List<T>();
        while (_entries.TryPeek(out var entry) && now - entry.ReceivedAt >= delay)
        {
            _entries.Dequeue();
            ready.Add(entry.Value);
        }

        return ready;
    }

    public void Clear() => _entries.Clear();

    private sealed record Entry(DateTimeOffset ReceivedAt, T Value);
}
