namespace Vantage.Core.Tests.Fixtures;

public static class HostKeyFixtures
{
    public const string Ed25519 = "AAAAC3NzaC1lZDI1NTE5AAAAIBK2uZ3mWIj+F5aNjoRzq111oT6F3WKpzIeGmlD8yLXI";
    public const string Ed25519Fingerprint = "SHA256:QH3eN9xtifKyOy6UK3WnLMgJAWCaJF/nu+M+ObFJZoE";

    public const string OtherEd25519 = "AAAAC3NzaC1lZDI1NTE5AAAAII486SsCeFsVt3GKJYDFdDzooeikOLTFoIXkT1xK/ngR";
    public const string OtherEd25519Fingerprint = "SHA256:W7bsbScqv1Tpw2iWjlL7kz352oFkwxOCG8b2ZLeTNgk";

    public const string Rsa =
        "AAAAB3NzaC1yc2EAAAADAQABAAABAQCyqez3jYVT7lsp5bjl415rOXeuz3JzHXn5GK/n8fWwCQdk1XdWXdXCnG4cnV2pG0fW/7oVAUBMXgWOKUkDXndpR+ga/" +
        "CyQdI+/aHJWsOmhLrd28hikdQJjnbFXkh0z10cPwLXztbtIH0jvLVkJjN/yoLukwJuhmCVGiYeiDMUlxKpsUL9m1yjMu9dg5E3Hnb3qNlWkVeLTah8anCwDm6/" +
        "mHEpniciVI6EJ98Yv5A37xY6DypuSa1SbpuhHtHG7sSUmUD4aVbKK3z3URY+2iflZKSaILJqHNX6yuKFlWib6zSkPYlJNYTCEECn0D3hXpL7EoC4+Selcr9nsga4Jbt1T";
    public const string RsaFingerprint = "SHA256:x4arlfxKH7DmcEu/p2J1Df9gsBIOaAx2Py7Yith2mzI";

    public const string Ecdsa =
        "AAAAE2VjZHNhLXNoYTItbmlzdHAyNTYAAAAIbmlzdHAyNTYAAABBBF9vnL6zc69Sdr8qD2zvZlDs4Qb1+ahJg8swOX3eP301fAIBzZVG/8iRxW3qzcbdcX+gp5aHXa3ZpZig+2JAiIo=";

    public const string HashedEd25519ForHashedLan =
        "|1|4I5OnCIG5Qyo0dqRfUjwTQLD2/0=|og7TS0vSEUOVdVu/8fDizyjBCas= ssh-ed25519 " + Ed25519;

    public const string HashedRsaForHashedLanPort2222 =
        "|1|XeSJ8v/HSPL7oE0KlTJqwNQ8QRA=|uVQGvR/ESPycoZ7GTxgQvBO6blc= ssh-rsa " + Rsa;

    public static byte[] Blob(string base64) => Convert.FromBase64String(base64);
}
