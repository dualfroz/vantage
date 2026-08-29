namespace Vantage.Core.Tests.Fixtures;

public static class PrometheusFixtures
{
    public const string Mixed =
        "# HELP http_requests_total The total number of HTTP requests.\n" +
        "# TYPE http_requests_total counter\n" +
        "http_requests_total{method=\"post\",code=\"200\"} 1027 1395066363000\n" +
        "http_requests_total{method=\"post\",code=\"400\"} 3\n" +
        "\n" +
        "metric_without_labels 12.47\n" +
        "msdos_file_access_time_seconds{path=\"C:\\\\DIR\\\\FILE.TXT\",error=\"Cannot find file:\\n\\\"FILE.TXT\\\"\"} 1.2\n" +
        "something_weird{problem=\"division by zero\"} +Inf\n" +
        "gauge_nan_value NaN\n";

    public static string NodeExporter(
        long cpu0Idle,
        long cpu1Idle,
        long cpu0User,
        long cpu1User) =>
        "# HELP node_cpu_seconds_total Seconds the CPUs spent in each mode.\n" +
        "# TYPE node_cpu_seconds_total counter\n" +
        $"node_cpu_seconds_total{{cpu=\"0\",mode=\"idle\"}} {cpu0Idle}\n" +
        $"node_cpu_seconds_total{{cpu=\"0\",mode=\"user\"}} {cpu0User}\n" +
        "node_cpu_seconds_total{cpu=\"0\",mode=\"system\"} 50\n" +
        $"node_cpu_seconds_total{{cpu=\"1\",mode=\"idle\"}} {cpu1Idle}\n" +
        $"node_cpu_seconds_total{{cpu=\"1\",mode=\"user\"}} {cpu1User}\n" +
        "node_cpu_seconds_total{cpu=\"1\",mode=\"system\"} 60\n" +
        "# TYPE node_memory_MemTotal_bytes gauge\n" +
        "node_memory_MemTotal_bytes 8000000000\n" +
        "node_memory_MemAvailable_bytes 2000000000\n" +
        "node_filesystem_size_bytes{mountpoint=\"/boot\"} 500000000\n" +
        "node_filesystem_avail_bytes{mountpoint=\"/boot\"} 400000000\n" +
        "node_filesystem_size_bytes{mountpoint=\"/\"} 100000000000\n" +
        "node_filesystem_avail_bytes{mountpoint=\"/\"} 60000000000\n" +
        "node_load1 0.5\n" +
        "node_load5 0.4\n" +
        "node_load15 0.3\n" +
        "node_boot_time_seconds 1000000\n" +
        "node_time_seconds 1086400\n";

    public static string NodeScrape1() => NodeExporter(1000, 900, 100, 120);

    public static string NodeScrape2() => NodeExporter(1004, 904, 101, 121);
}
