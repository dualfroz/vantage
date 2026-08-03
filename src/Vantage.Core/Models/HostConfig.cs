namespace Vantage.Core.Models;

public enum SshAuthMethod
{
    PrivateKey,
    Password,
    Agent
}

public sealed class HostConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public int Port { get; set; } = 22;
    public string Username { get; set; } = "";
    public SshAuthMethod AuthMethod { get; set; } = SshAuthMethod.PrivateKey;
    public string? PrivateKeyPath { get; set; }
    public string? PrivateKeyPassphrase { get; set; }
    public string? Password { get; set; }
    public string DiskMount { get; set; } = "/";
    public string? MetricsEndpoint { get; set; }

    public string ResolveMetricsEndpoint() =>
        string.IsNullOrWhiteSpace(MetricsEndpoint)
            ? $"http://{Address}:9100/metrics"
            : MetricsEndpoint;
}
