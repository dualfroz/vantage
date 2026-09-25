using System.Text.Json.Serialization;

namespace Vantage.Core.Models;

public enum SshAuthMethod
{
    PrivateKey,
    Password
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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PrivateKeyPassphrase { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PrivateKeyPassphraseRef { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Password { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PasswordRef { get; set; }
    public bool UseSudo { get; set; }
    public string DiskMount { get; set; } = "/";
    public string? MetricsEndpoint { get; set; }

    public string ResolveMetricsEndpoint() =>
        string.IsNullOrWhiteSpace(MetricsEndpoint)
            ? $"http://{Address}:9100/metrics"
            : MetricsEndpoint;

    public HostConfig Clone() => (HostConfig)MemberwiseClone();
}
