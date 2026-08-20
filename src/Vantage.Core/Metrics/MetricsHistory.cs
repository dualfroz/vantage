namespace Vantage.Core.Metrics;

public sealed class MetricsHistory
{
    private readonly double[] buffer;
    private int count;
    private int head;

    public MetricsHistory(int capacity)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity), "History capacity must be positive");
        buffer = new double[capacity];
    }

    public int Capacity => buffer.Length;
    public int Count => count;
    public double Latest => count == 0 ? 0 : buffer[(head - 1 + buffer.Length) % buffer.Length];

    public void Add(double value)
    {
        buffer[head] = value;
        head = (head + 1) % buffer.Length;
        if (count < buffer.Length)
            count++;
    }

    public IReadOnlyList<double> Snapshot()
    {
        var ordered = new double[count];
        var start = (head - count + buffer.Length) % buffer.Length;
        for (var i = 0; i < count; i++)
            ordered[i] = buffer[(start + i) % buffer.Length];
        return ordered;
    }
}
