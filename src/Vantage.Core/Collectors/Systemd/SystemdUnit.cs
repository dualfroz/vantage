namespace Vantage.Core.Collectors.Systemd;

public readonly record struct SystemdUnit(
    string Unit,
    string Load,
    string Active,
    string Sub,
    string Description)
{
    public bool IsRunning => Active == "active" && Sub == "running";
    public bool IsFailed => Active == "failed" || Sub == "failed";
}
