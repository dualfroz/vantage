using Vantage.Core.Prometheus;
using Vantage.Core.Tests.Fixtures;
using Xunit;

namespace Vantage.Core.Tests.Prometheus;

public class PrometheusParserTests
{
    [Fact]
    public void Parse_SkipsCommentsAndBlankLines()
    {
        var samples = PrometheusTextParser.Parse(PrometheusFixtures.Mixed);
        Assert.DoesNotContain(samples, s => s.Name.StartsWith("#"));
    }

    [Fact]
    public void Parse_ReadsLabelsValueAndTimestamp()
    {
        var samples = PrometheusTextParser.Parse(PrometheusFixtures.Mixed);

        var post200 = samples.Single(s =>
            s.Name == "http_requests_total" && s.HasLabel("code", "200"));
        Assert.Equal("post", post200.Label("method"));
        Assert.Equal(1027, post200.Value);
        Assert.Equal(1395066363000, post200.TimestampMs);
    }

    [Fact]
    public void Parse_MetricWithoutLabels()
    {
        var samples = PrometheusTextParser.Parse(PrometheusFixtures.Mixed);
        var bare = samples.Single(s => s.Name == "metric_without_labels");
        Assert.Empty(bare.Labels);
        Assert.Equal(12.47, bare.Value, 3);
    }

    [Fact]
    public void Parse_HandlesEscapedLabelValues()
    {
        var samples = PrometheusTextParser.Parse(PrometheusFixtures.Mixed);
        var msdos = samples.Single(s => s.Name == "msdos_file_access_time_seconds");

        Assert.Equal("C:\\DIR\\FILE.TXT", msdos.Label("path"));
        Assert.Contains("\n", msdos.Label("error"));
        Assert.Contains("\"FILE.TXT\"", msdos.Label("error"));
    }

    [Fact]
    public void Parse_HandlesInfinityAndNaN()
    {
        var samples = PrometheusTextParser.Parse(PrometheusFixtures.Mixed);

        Assert.True(double.IsPositiveInfinity(samples.Single(s => s.Name == "something_weird").Value));
        Assert.True(double.IsNaN(samples.Single(s => s.Name == "gauge_nan_value").Value));
    }
}
