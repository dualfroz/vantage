namespace Vantage.Core.Ssh;

public interface IHostKeyStore
{
    string TrustedKeysPath { get; }

    HostKeyCheck Check(string host, int port, byte[] keyBlob);

    IReadOnlyList<string> KnownKeyTypes(string host, int port);

    void Trust(HostKeyCandidate key);
}

public sealed class KnownHostsStore : IHostKeyStore
{
    private readonly object gate = new();
    private readonly IReadOnlyList<string> readOnlyPaths;

    public KnownHostsStore(string trustedKeysPath, IEnumerable<string> readOnlyPaths)
    {
        TrustedKeysPath = trustedKeysPath;
        this.readOnlyPaths = readOnlyPaths.ToArray();
    }

    public string TrustedKeysPath { get; }

    public static KnownHostsStore ForCurrentUser(string configDirectory)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new KnownHostsStore(
            Path.Combine(configDirectory, "known_hosts"),
            new[] { Path.Combine(home, ".ssh", "known_hosts"), "/etc/ssh/ssh_known_hosts" });
    }

    public HostKeyCheck Check(string host, int port, byte[] keyBlob)
    {
        var presented = HostKeyCandidate.From(host, port, keyBlob);
        var matching = Entries().Where(entry => entry.Matches(host, port)).ToArray();

        if (matching.Any(entry => entry.Revoked && SameKey(entry, keyBlob)))
            return new HostKeyCheck(HostKeyStatus.Revoked, presented, Array.Empty<KnownHostMatch>());

        if (matching.Any(entry => !entry.Revoked && SameKey(entry, keyBlob)))
            return new HostKeyCheck(HostKeyStatus.Trusted, presented, Array.Empty<KnownHostMatch>());

        var conflicts = matching
            .Where(entry => !entry.Revoked && entry.KeyType == presented.KeyType)
            .Select(entry => new KnownHostMatch(entry.KeyType, HostKeyBlob.Fingerprint(entry.KeyBlob), entry.Source, entry.Line))
            .ToArray();

        return new HostKeyCheck(conflicts.Length > 0 ? HostKeyStatus.Changed : HostKeyStatus.Unknown, presented, conflicts);
    }

    public IReadOnlyList<string> KnownKeyTypes(string host, int port) => Entries()
        .Where(entry => !entry.Revoked && entry.Matches(host, port))
        .Select(entry => entry.KeyType)
        .Distinct()
        .ToArray();

    public void Trust(HostKeyCandidate key)
    {
        lock (gate)
        {
            var check = Check(key.Host, key.Port, key.KeyBlob);
            if (check.Status is HostKeyStatus.Changed or HostKeyStatus.Revoked)
                throw new HostKeyVerificationException(check);
            if (check.Status == HostKeyStatus.Trusted)
                return;

            var directory = Path.GetDirectoryName(TrustedKeysPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var separator = File.Exists(TrustedKeysPath) && !EndsWithNewline(TrustedKeysPath) ? "\n" : "";
            File.AppendAllText(TrustedKeysPath, separator + KnownHostsFile.FormatLine(key) + "\n");
        }
    }

    private IEnumerable<KnownHostEntry> Entries() => readOnlyPaths
        .Append(TrustedKeysPath)
        .Where(File.Exists)
        .SelectMany(path => KnownHostsFile.Parse(path, File.ReadLines(path)));

    private static bool SameKey(KnownHostEntry entry, byte[] keyBlob) => entry.KeyBlob.AsSpan().SequenceEqual(keyBlob);

    private static bool EndsWithNewline(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length == 0)
            return true;
        stream.Seek(-1, SeekOrigin.End);
        return stream.ReadByte() == '\n';
    }
}
