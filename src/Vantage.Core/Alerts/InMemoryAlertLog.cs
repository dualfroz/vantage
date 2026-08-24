namespace Vantage.Core.Alerts;

public sealed class InMemoryAlertLog : IAlertLog
{
    private readonly object gate = new();
    private readonly LinkedList<AlertLogEntry> entries = new();
    private readonly int capacity;

    public InMemoryAlertLog(int capacity = 500)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        this.capacity = capacity;
    }

    public void Append(AlertLogEntry entry)
    {
        lock (gate)
        {
            entries.AddLast(entry);
            while (entries.Count > capacity)
                entries.RemoveFirst();
        }
    }

    public IReadOnlyList<AlertLogEntry> Recent(int max)
    {
        lock (gate)
        {
            return entries.Reverse().Take(max).ToArray();
        }
    }
}
