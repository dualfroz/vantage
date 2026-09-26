using Vantage.Core.Ssh;
using Xunit;
using static Vantage.Core.Tests.Fixtures.HostKeyFixtures;

namespace Vantage.Core.Tests.Ssh;

public class KnownHostsTests : IDisposable
{
    private readonly string tempDir = Path.Combine(Path.GetTempPath(), "vantage-known-hosts-" + Guid.NewGuid().ToString("N"));

    private string UserFile => Path.Combine(tempDir, "user_known_hosts");

    private string AppFile => Path.Combine(tempDir, "app", "known_hosts");

    private KnownHostsStore Store(params string[] userLines)
    {
        Directory.CreateDirectory(tempDir);
        File.WriteAllLines(UserFile, userLines);
        return new KnownHostsStore(AppFile, new[] { UserFile, Path.Combine(tempDir, "missing") });
    }

    [Fact]
    public void Fingerprint_AndKeyType_MatchSshKeygen()
    {
        Assert.Equal(Ed25519Fingerprint, HostKeyBlob.Fingerprint(Blob(Ed25519)));
        Assert.Equal(RsaFingerprint, HostKeyBlob.Fingerprint(Blob(Rsa)));
        Assert.Equal("ssh-ed25519", HostKeyBlob.KeyType(Blob(Ed25519)));
        Assert.Equal("ssh-rsa", HostKeyBlob.KeyType(Blob(Rsa)));
        Assert.Equal("ecdsa-sha2-nistp256", HostKeyBlob.KeyType(Blob(Ecdsa)));
    }

    [Fact]
    public void PlainEntry_TrustsAnyListedNameCaseInsensitively()
    {
        var store = Store("# homelab", "", "edge.lan,192.0.2.10 ssh-ed25519 " + Ed25519 + " root@edge");

        Assert.Equal(HostKeyStatus.Trusted, store.Check("192.0.2.10", 22, Blob(Ed25519)).Status);
        Assert.Equal(HostKeyStatus.Trusted, store.Check("EDGE.lan", 22, Blob(Ed25519)).Status);
        Assert.Equal(HostKeyStatus.Unknown, store.Check("other.lan", 22, Blob(Ed25519)).Status);
    }

    [Fact]
    public void NonStandardPort_UsesBracketedHostAndPort()
    {
        var store = Store("[edge.lan]:2222 ssh-ed25519 " + Ed25519);

        Assert.Equal(HostKeyStatus.Trusted, store.Check("edge.lan", 2222, Blob(Ed25519)).Status);
        Assert.Equal(HostKeyStatus.Unknown, store.Check("edge.lan", 22, Blob(Ed25519)).Status);
        Assert.Equal(HostKeyStatus.Unknown, store.Check("edge.lan", 2200, Blob(Ed25519)).Status);
    }

    [Fact]
    public void HashedEntries_FromSshKeygen_MatchHostAndPort()
    {
        var store = Store(HashedEd25519ForHashedLan, HashedRsaForHashedLanPort2222);

        Assert.Equal(HostKeyStatus.Trusted, store.Check("hashed.lan", 22, Blob(Ed25519)).Status);
        Assert.Equal(HostKeyStatus.Trusted, store.Check("hashed.lan", 2222, Blob(Rsa)).Status);
        Assert.Equal(HostKeyStatus.Unknown, store.Check("hashed.lan", 22, Blob(Rsa)).Status);
        Assert.Equal(HostKeyStatus.Unknown, store.Check("hashed.lan.example", 22, Blob(Ed25519)).Status);
    }

    [Fact]
    public void DifferentKeyOfTheSameType_IsChangedWithBothFingerprints()
    {
        var store = Store("edge.lan ssh-rsa " + Rsa, "edge.lan ssh-ed25519 " + Ed25519);

        var check = store.Check("edge.lan", 22, Blob(OtherEd25519));

        Assert.Equal(HostKeyStatus.Changed, check.Status);
        Assert.Equal(OtherEd25519Fingerprint, check.Presented.Fingerprint);
        var conflict = Assert.Single(check.Conflicts);
        Assert.Equal(Ed25519Fingerprint, conflict.Fingerprint);
        Assert.Equal(UserFile, conflict.Source);
        Assert.Equal(2, conflict.Line);
    }

    [Fact]
    public void KeyOfAnotherType_IsUnknownNotChanged()
    {
        var store = Store("edge.lan ssh-ed25519 " + Ed25519);

        Assert.Equal(HostKeyStatus.Unknown, store.Check("edge.lan", 22, Blob(Ecdsa)).Status);
    }

    [Fact]
    public void RevokedKey_IsRefusedEvenWhenAlsoListed()
    {
        var store = Store("edge.lan ssh-ed25519 " + Ed25519, "@revoked * ssh-ed25519 " + Ed25519);

        Assert.Equal(HostKeyStatus.Revoked, store.Check("edge.lan", 22, Blob(Ed25519)).Status);
    }

    [Fact]
    public void Patterns_SupportWildcardsAndNegation_AndSkipCertAuthorities()
    {
        var store = Store(
            "@cert-authority *.lan ssh-ed25519 " + OtherEd25519,
            "*.lan,!secret.lan ssh-ed25519 " + Ed25519,
            "192.0.2.? ssh-rsa " + Rsa,
            "broken-line-without-key");

        Assert.Equal(HostKeyStatus.Trusted, store.Check("nas.lan", 22, Blob(Ed25519)).Status);
        Assert.Equal(HostKeyStatus.Unknown, store.Check("secret.lan", 22, Blob(Ed25519)).Status);
        Assert.Equal(HostKeyStatus.Changed, store.Check("nas.lan", 22, Blob(OtherEd25519)).Status);
        Assert.Equal(HostKeyStatus.Trusted, store.Check("192.0.2.7", 22, Blob(Rsa)).Status);
        Assert.Equal(HostKeyStatus.Unknown, store.Check("192.0.2.70", 22, Blob(Rsa)).Status);
    }

    [Fact]
    public void KnownKeyTypes_ListsTypesForTheHostInFileOrder()
    {
        var store = Store("edge.lan ssh-rsa " + Rsa, "other.lan ecdsa-sha2-nistp256 " + Ecdsa, "edge.lan ssh-ed25519 " + Ed25519);

        Assert.Equal(new[] { "ssh-rsa", "ssh-ed25519" }, store.KnownKeyTypes("edge.lan", 22));
        Assert.Empty(store.KnownKeyTypes("edge.lan", 2222));
    }

    [Fact]
    public void Trust_AppendsToTheAppFileAndLeavesTheUserFileAlone()
    {
        var store = Store("edge.lan ssh-rsa " + Rsa);
        var userBefore = File.ReadAllText(UserFile);
        var candidate = store.Check("nas.lan", 2222, Blob(Ed25519)).Presented;

        store.Trust(candidate);

        Assert.Equal(userBefore, File.ReadAllText(UserFile));
        Assert.Equal(new[] { "[nas.lan]:2222 ssh-ed25519 " + Ed25519 }, File.ReadAllLines(AppFile));
        Assert.Equal(HostKeyStatus.Trusted, store.Check("nas.lan", 2222, Blob(Ed25519)).Status);
    }

    [Fact]
    public void Trust_StartsANewLineWhenTheFileDoesNotEndWithOne()
    {
        var store = Store();
        Directory.CreateDirectory(Path.GetDirectoryName(AppFile)!);
        File.WriteAllText(AppFile, "edge.lan ssh-rsa " + Rsa);

        store.Trust(store.Check("nas.lan", 22, Blob(Ed25519)).Presented);

        Assert.Equal(new[] { "edge.lan ssh-rsa " + Rsa, "nas.lan ssh-ed25519 " + Ed25519 }, File.ReadAllLines(AppFile));
    }

    [Fact]
    public void Trust_RefusesAChangedKey()
    {
        var store = Store("edge.lan ssh-ed25519 " + Ed25519);
        var candidate = store.Check("edge.lan", 22, Blob(OtherEd25519)).Presented;

        var error = Assert.Throws<HostKeyVerificationException>(() => store.Trust(candidate));

        Assert.Equal(HostKeyStatus.Changed, error.Check.Status);
        Assert.False(File.Exists(AppFile));
    }

    [Fact]
    public void ExceptionMessages_NameTheEndpointAndFingerprints()
    {
        var store = Store("edge.lan ssh-ed25519 " + Ed25519);

        var unknown = new HostKeyVerificationException(store.Check("nas.lan", 2222, Blob(Ed25519)));
        var changed = new HostKeyVerificationException(store.Check("edge.lan", 22, Blob(OtherEd25519)));

        Assert.Contains("nas.lan:2222 is not known", unknown.Message);
        Assert.Contains("ssh-ed25519 " + Ed25519Fingerprint, unknown.Message);
        Assert.StartsWith("HOST KEY CHANGED for edge.lan:22", changed.Message);
        Assert.Contains(OtherEd25519Fingerprint, changed.Message);
        Assert.Contains($"{UserFile} line 1 expects {Ed25519Fingerprint}", changed.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, recursive: true);
    }
}
