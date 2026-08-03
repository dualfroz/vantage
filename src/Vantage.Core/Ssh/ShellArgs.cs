namespace Vantage.Core.Ssh;

public static class ShellArgs
{
    public static string Quote(string value) => "'" + value.Replace("'", "'\\''") + "'";
}
