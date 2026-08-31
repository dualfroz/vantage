using Vantage.Core.Metrics;
using Xunit;

namespace Vantage.Core.Tests.Metrics;

public class MetricsHistoryTests
{
    [Fact]
    public void Snapshot_ReturnsValuesInInsertionOrder()
    {
        var history = new MetricsHistory(5);
        history.Add(1);
        history.Add(2);
        history.Add(3);

        Assert.Equal(new double[] { 1, 2, 3 }, history.Snapshot());
        Assert.Equal(3, history.Latest);
    }

    [Fact]
    public void Add_WrapsAroundAndKeepsMostRecentWindow()
    {
        var history = new MetricsHistory(3);
        for (var i = 1; i <= 5; i++)
            history.Add(i);

        Assert.Equal(3, history.Count);
        Assert.Equal(new double[] { 3, 4, 5 }, history.Snapshot());
        Assert.Equal(5, history.Latest);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MetricsHistory(0));
    }
}
