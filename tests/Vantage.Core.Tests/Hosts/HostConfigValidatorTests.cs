using Vantage.Core.Hosts;
using Vantage.Core.Models;
using Xunit;

namespace Vantage.Core.Tests.Hosts;

public class HostConfigValidatorTests
{
    private static HostConfig Valid() => new()
    {
        Name = "edge",
        Address = "192.0.2.10",
        Port = 22,
        Username = "ops",
        AuthMethod = SshAuthMethod.PrivateKey,
        PrivateKeyPath = "~/.ssh/id_ed25519"
    };

    [Fact]
    public void ValidHost_HasNoErrors()
    {
        Assert.Empty(HostConfigValidator.Validate(Valid()));
    }

    [Fact]
    public void MissingAddressAndUser_AreReported()
    {
        var host = Valid();
        host.Address = " ";
        host.Username = "";

        var errors = HostConfigValidator.Validate(host);

        Assert.Contains("Address is required.", errors);
        Assert.Contains("Username is required.", errors);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void PortOutOfRange_IsReported(int port)
    {
        var host = Valid();
        host.Port = port;

        Assert.Contains("Port must be between 1 and 65535.", HostConfigValidator.Validate(host));
    }

    [Fact]
    public void KeyAuthWithoutPath_IsReported()
    {
        var host = Valid();
        host.PrivateKeyPath = null;

        Assert.Contains("Private key path is required for key authentication.", HostConfigValidator.Validate(host));
    }

    [Fact]
    public void PasswordAuthWithoutPassword_IsReported()
    {
        var host = Valid();
        host.AuthMethod = SshAuthMethod.Password;

        Assert.Contains("Password is required for password authentication.", HostConfigValidator.Validate(host));
    }

    [Theory]
    [InlineData("var/lib")]
    [InlineData("")]
    public void RelativeDiskMount_IsReported(string mount)
    {
        var host = Valid();
        host.DiskMount = mount;

        Assert.Contains("Disk mount must be an absolute path such as /.", HostConfigValidator.Validate(host));
    }

    [Theory]
    [InlineData("ftp://192.0.2.10/metrics")]
    [InlineData("not a url")]
    public void BadMetricsEndpoint_IsReported(string endpoint)
    {
        var host = Valid();
        host.MetricsEndpoint = endpoint;

        Assert.Contains("Metrics endpoint must be an absolute http or https URL.", HostConfigValidator.Validate(host));
    }
}
