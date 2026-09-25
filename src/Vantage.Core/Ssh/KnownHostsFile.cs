using System.Security.Cryptography;
using System.Text;

namespace Vantage.Core.Ssh;

public sealed record KnownHostEntry(string Source, int Line, string HostPatterns, string KeyType, byte[] KeyBlob, bool Revoked)
{
    public bool Matches(string host, int port) => KnownHostsFile.PatternsMatch(HostPatterns, KnownHostsFile.LookupName(host, port));
}

public static class KnownHostsFile
{
    private const string HashedPrefix = "|1|";

    public static string LookupName(string host, int port)
    {
        var name = host.Trim().ToLowerInvariant();
        return port == 22 ? name : $"[{name}]:{port}";
    }

    public static string FormatLine(HostKeyCandidate key) =>
        $"{LookupName(key.Host, key.Port)} {key.KeyType} {Convert.ToBase64String(key.KeyBlob)}";

    public static IReadOnlyList<KnownHostEntry> Parse(string source, IEnumerable<string> lines)
    {
        var entries = new List<KnownHostEntry>();
        var lineNumber = 0;
        foreach (var rawLine in lines)
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;

            var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var offset = 0;
            var revoked = false;
            if (fields[0].StartsWith('@'))
            {
                if (fields[0] != "@revoked")
                    continue;
                revoked = true;
                offset = 1;
            }

            if (fields.Length < offset + 3 || !TryDecode(fields[offset + 2], out var keyBlob))
                continue;

            entries.Add(new KnownHostEntry(source, lineNumber, fields[offset], fields[offset + 1], keyBlob, revoked));
        }

        return entries;
    }

    public static bool PatternsMatch(string patterns, string lookupName)
    {
        if (patterns.StartsWith(HashedPrefix, StringComparison.Ordinal))
            return HashedMatches(patterns, lookupName);

        var matched = false;
        foreach (var pattern in patterns.Split(','))
        {
            var negated = pattern.StartsWith('!');
            if (!Glob(negated ? pattern[1..].ToLowerInvariant() : pattern.ToLowerInvariant(), lookupName))
                continue;
            if (negated)
                return false;
            matched = true;
        }

        return matched;
    }

    private static bool HashedMatches(string field, string lookupName)
    {
        var parts = field.Split('|');
        if (parts.Length != 4 || !TryDecode(parts[2], out var salt) || !TryDecode(parts[3], out var expected))
            return false;

        using var hmac = new HMACSHA1(salt);
        var actual = hmac.ComputeHash(Encoding.UTF8.GetBytes(lookupName));
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static bool Glob(string pattern, string name)
    {
        int p = 0, n = 0, star = -1, resume = 0;
        while (n < name.Length)
        {
            if (p < pattern.Length && (pattern[p] == '?' || pattern[p] == name[n]))
            {
                p++;
                n++;
            }
            else if (p < pattern.Length && pattern[p] == '*')
            {
                star = p++;
                resume = n;
            }
            else if (star >= 0)
            {
                p = star + 1;
                n = ++resume;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && pattern[p] == '*')
            p++;
        return p == pattern.Length;
    }

    private static bool TryDecode(string base64, out byte[] bytes)
    {
        bytes = new byte[base64.Length];
        if (!Convert.TryFromBase64String(base64, bytes, out var written) || written == 0)
            return false;
        bytes = bytes[..written];
        return true;
    }
}
