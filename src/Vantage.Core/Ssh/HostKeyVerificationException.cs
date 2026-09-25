namespace Vantage.Core.Ssh;

public sealed class HostKeyVerificationException : Exception
{
    public HostKeyVerificationException(HostKeyCheck check)
        : base(Describe(check))
    {
        Check = check;
    }

    public HostKeyCheck Check { get; }

    private static string Describe(HostKeyCheck check)
    {
        var key = check.Presented;
        var presented = $"{key.KeyType} {key.Fingerprint}";
        return check.Status switch
        {
            HostKeyStatus.Changed =>
                $"HOST KEY CHANGED for {key.Endpoint}. The host presented {presented}, but " +
                string.Join("; ", check.Conflicts.Select(known => $"{known.Source} line {known.Line} expects {known.Fingerprint}")) +
                ". Someone may be intercepting the connection, or the host was reinstalled. The connection was refused; " +
                "if the change is expected, delete the old line and test the connection again.",
            HostKeyStatus.Revoked =>
                $"The host key of {key.Endpoint} ({presented}) is marked @revoked in known_hosts. The connection was refused.",
            _ =>
                $"The host key of {key.Endpoint} is not known. The host presented {presented}. Compare it with the " +
                "host's own key, then trust it with Test connection in the host editor or add it to ~/.ssh/known_hosts."
        };
    }
}
