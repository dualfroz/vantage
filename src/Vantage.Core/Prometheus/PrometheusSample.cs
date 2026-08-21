namespace Vantage.Core.Prometheus;

public sealed record PrometheusSample(
    string Name,
    IReadOnlyDictionary<string, string> Labels,
    double Value,
    long? TimestampMs)
{
    public string? Label(string key) => Labels.TryGetValue(key, out var value) ? value : null;

    public bool HasLabel(string key, string expected) =>
        Labels.TryGetValue(key, out var value) && value == expected;
}
