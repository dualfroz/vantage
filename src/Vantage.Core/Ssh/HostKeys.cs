using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Vantage.Core.Ssh;

public enum HostKeyStatus
{
    Trusted,
    Unknown,
    Changed,
    Revoked
}

public sealed record HostKeyCandidate(string Host, int Port, string KeyType, string Fingerprint, byte[] KeyBlob)
{
    public string Endpoint => $"{Host}:{Port}";

    public static HostKeyCandidate From(string host, int port, byte[] keyBlob) =>
        new(host.Trim(), port, HostKeyBlob.KeyType(keyBlob), HostKeyBlob.Fingerprint(keyBlob), keyBlob);
}

public sealed record KnownHostMatch(string KeyType, string Fingerprint, string Source, int Line);

public sealed record HostKeyCheck(HostKeyStatus Status, HostKeyCandidate Presented, IReadOnlyList<KnownHostMatch> Conflicts);

public static class HostKeyBlob
{
    public static string KeyType(byte[] keyBlob)
    {
        if (keyBlob.Length < 4)
            return "unknown";

        var length = BinaryPrimitives.ReadInt32BigEndian(keyBlob);
        return length > 0 && length <= keyBlob.Length - 4
            ? Encoding.ASCII.GetString(keyBlob, 4, length)
            : "unknown";
    }

    public static string Fingerprint(byte[] keyBlob) =>
        "SHA256:" + Convert.ToBase64String(SHA256.HashData(keyBlob)).TrimEnd('=');
}
