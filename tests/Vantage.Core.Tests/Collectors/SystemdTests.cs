using Vantage.Core.Collectors.Systemd;
using Vantage.Core.Models;
using Vantage.Core.Tests.Fakes;
using Vantage.Core.Tests.Fixtures;
using Xunit;

namespace Vantage.Core.Tests.Collectors;

public class SystemdTests
{
    [Fact]
    public void Parser_ReadsUnitColumns()
    {
        var units = SystemdUnitParser.Parse(CollectorFixtures.SystemctlOutput);

        var docker = units.Single(u => u.Unit == "docker.service");
        Assert.Equal("loaded", docker.Load);
        Assert.Equal("active", docker.Active);
        Assert.Equal("running", docker.Sub);
        Assert.Equal("Docker Application Container Engine", docker.Description);
        Assert.True(docker.IsRunning);
    }

    [Fact]
    public void Parser_StripsLeadingStatusMarker()
    {
        var units = SystemdUnitParser.Parse(CollectorFixtures.SystemctlOutput);

        var backup = units.Single(u => u.Unit == "backup.service");
        Assert.Equal("failed", backup.Active);
        Assert.True(backup.IsFailed);
    }

    [Fact]
    public void Parser_KeepsOnlyServiceUnits()
    {
        var units = SystemdUnitParser.Parse(CollectorFixtures.SystemctlOutput);
        Assert.DoesNotContain(units, u => u.Unit.EndsWith(".timer"));
        Assert.Equal(4, units.Count);
    }

    [Fact]
    public async Task SshSystemdProvider_UsesListUnitsCommand()
    {
        var runner = new FakeSshCommandRunner(CollectorFixtures.SystemctlOutput);
        var provider = new SshSystemdProvider(runner);

        var units = await provider.GetServicesAsync(new HostConfig(), CancellationToken.None);

        Assert.Contains("list-units", runner.Commands.Single());
        Assert.Contains(units, u => u.Unit == "nginx.service");
    }

    [Fact]
    public async Task MockSystemdProvider_IncludesFailedUnit()
    {
        var provider = new MockSystemdProvider();
        var units = await provider.GetServicesAsync(new HostConfig(), CancellationToken.None);

        Assert.Contains(units, u => u.IsFailed);
        Assert.Contains(units, u => u.IsRunning);
    }
}
