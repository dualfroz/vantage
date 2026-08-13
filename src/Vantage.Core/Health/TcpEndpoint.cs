namespace Vantage.Core.Health;

public readonly record struct TcpEndpoint(string Host, int Port)
{
    public static TcpEndpoint Parse(string target)
    {
        var trimmed = target.Trim();

        if (trimmed.StartsWith('['))
        {
            var close = trimmed.IndexOf(']');
            if (close < 0)
                throw new FormatException($"Malformed IPv6 endpoint: '{target}'");

            var host = trimmed[1..close];
            var rest = trimmed[(close + 1)..].TrimStart(':');
            return new TcpEndpoint(host, ParsePort(rest, target));
        }

        var lastColon = trimmed.LastIndexOf(':');
        if (lastColon <= 0 || lastColon == trimmed.Length - 1)
            throw new FormatException($"TCP target must be host:port, got '{target}'");

        return new TcpEndpoint(trimmed[..lastColon], ParsePort(trimmed[(lastColon + 1)..], target));
    }

    private static int ParsePort(string value, string original)
    {
        if (!int.TryParse(value, out var port) || port is < 1 or > 65535)
            throw new FormatException($"Invalid port in TCP target '{original}'");
        return port;
    }
}
