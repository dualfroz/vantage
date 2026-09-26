using Renci.SshNet;
using Renci.SshNet.Common;
using Vantage.Core.Models;
using Vantage.Core.Ssh;
using Xunit;
using static Vantage.Core.Tests.Fixtures.HostKeyFixtures;

namespace Vantage.Core.Tests.Ssh;

public class SshNetCommandRunnerTests : IDisposable
{
    private readonly string tempDir = Path.Combine(Path.GetTempPath(), "vantage-runner-" + Guid.NewGuid().ToString("N"));

    private static readonly HostConfig Edge = new() { Name = "edge", Address = "edge.lan", Port = 22, Username = "ops" };

    private KnownHostsStore Store(params string[] lines)
    {
        Directory.CreateDirectory(tempDir);
        var userFile = Path.Combine(tempDir, "known_hosts");
        File.WriteAllLines(userFile, lines);
        return new KnownHostsStore(Path.Combine(tempDir, "app_known_hosts"), new[] { userFile });
    }

    private static ConnectionInfo Info() => new("edge.lan", 22, "ops", new PasswordAuthenticationMethod("ops", "unused"));

    private static HostKeyEventArgs Received(string algorithm, string keyBase64) =>
        new(Info().HostKeyAlgorithms[algorithm](Blob(keyBase64)));

    [Fact]
    public void VerifyHostKey_KnownKey_IsTrusted()
    {
        var args = Received("ssh-ed25519", Ed25519);

        var rejected = SshNetCommandRunner.VerifyHostKey(Store("edge.lan ssh-ed25519 " + Ed25519), Edge, args);

        Assert.True(args.CanTrust);
        Assert.Null(rejected);
    }

    [Fact]
    public void VerifyHostKey_RsaSha2Algorithm_MatchesSshRsaEntry()
    {
        var args = Received("rsa-sha2-512", Rsa);

        var rejected = SshNetCommandRunner.VerifyHostKey(Store("edge.lan ssh-rsa " + Rsa), Edge, args);

        Assert.True(args.CanTrust);
        Assert.Null(rejected);
    }

    [Fact]
    public void VerifyHostKey_UnknownHost_IsRejectedWithThePresentedKey()
    {
        var args = Received("ssh-ed25519", Ed25519);

        var rejected = SshNetCommandRunner.VerifyHostKey(Store(), Edge, args);

        Assert.False(args.CanTrust);
        Assert.Equal(HostKeyStatus.Unknown, rejected!.Status);
        Assert.Equal(Ed25519Fingerprint, rejected.Presented.Fingerprint);
        Assert.Equal(Blob(Ed25519), rejected.Presented.KeyBlob);
    }

    [Fact]
    public void VerifyHostKey_ChangedKey_IsRejected()
    {
        var args = Received("ssh-ed25519", OtherEd25519);

        var rejected = SshNetCommandRunner.VerifyHostKey(Store("edge.lan ssh-ed25519 " + Ed25519), Edge, args);

        Assert.False(args.CanTrust);
        Assert.Equal(HostKeyStatus.Changed, rejected!.Status);
    }

    [Fact]
    public void PreferKnownKeyTypes_MovesKnownAlgorithmsFirstAndKeepsTheRest()
    {
        var info = Info();
        var before = info.HostKeyAlgorithms.Keys.ToArray();

        SshNetCommandRunner.PreferKnownKeyTypes(info, new[] { "ssh-rsa" });

        var after = info.HostKeyAlgorithms.Keys.ToArray();
        Assert.Equal(new[] { "rsa-sha2-512", "rsa-sha2-256", "ssh-rsa" }, after.Take(3));
        Assert.Equal(before.OrderBy(name => name), after.OrderBy(name => name));
        Assert.Equal(before.Where(name => !name.Contains("rsa")), after.Skip(3));
    }

    [Fact]
    public void PreferKnownKeyTypes_LeavesOrderAloneWithoutUsableKnownTypes()
    {
        var info = Info();
        var before = info.HostKeyAlgorithms.Keys.ToArray();

        SshNetCommandRunner.PreferKnownKeyTypes(info, Array.Empty<string>());
        SshNetCommandRunner.PreferKnownKeyTypes(info, new[] { "sk-ssh-ed25519@openssh.com" });

        Assert.Equal(before, info.HostKeyAlgorithms.Keys);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, recursive: true);
    }
}
