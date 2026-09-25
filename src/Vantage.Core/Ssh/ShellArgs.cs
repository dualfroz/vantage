using Vantage.Core.Models;

namespace Vantage.Core.Ssh;

public static class ShellArgs
{
    public static string Quote(string value) => "'" + value.Replace("'", "'\\''") + "'";

    public static string Privileged(HostConfig host, string command) =>
        host.UseSudo ? "sudo -n " + command : command;

    public static string ExpandHome(string path, string? home = null)
    {
        if (path != "~" && !path.StartsWith("~/", StringComparison.Ordinal))
            return path;

        home ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return path == "~" ? home : Path.Combine(home, path[2..]);
    }
}
