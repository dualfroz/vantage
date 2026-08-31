using Vantage.Core.Health;
using Xunit;

namespace Vantage.Core.Tests.Health;

public class TcpEndpointTests
{
    [Fact]
    public void Parse_HostAndPort()
    {
        var endpoint = TcpEndpoint.Parse("db.internal:5432");
        Assert.Equal("db.internal", endpoint.Host);
        Assert.Equal(5432, endpoint.Port);
    }

    [Fact]
    public void Parse_IPv6InBrackets()
    {
        var endpoint = TcpEndpoint.Parse("[::1]:8080");
        Assert.Equal("::1", endpoint.Host);
        Assert.Equal(8080, endpoint.Port);
    }

    [Theory]
    [InlineData("nohost")]
    [InlineData("host:")]
    [InlineData("host:70000")]
    [InlineData("host:abc")]
    public void Parse_RejectsMalformedTargets(string target)
    {
        Assert.Throws<FormatException>(() => TcpEndpoint.Parse(target));
    }
}
