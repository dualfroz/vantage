using Tmds.DBus.Protocol;

namespace Vantage.Core.Desktop;

public sealed class SessionBus
{
    private readonly Lazy<Task<Connection>> connection;

    public SessionBus(string address)
    {
        connection = new Lazy<Task<Connection>>(() => ConnectAsync(address));
    }

    public static SessionBus? ForCurrentUser() =>
        OperatingSystem.IsLinux() && Address.Session is { Length: > 0 } address ? new SessionBus(address) : null;

    public Task<Connection> ConnectionAsync() => connection.Value;

    private static async Task<Connection> ConnectAsync(string address)
    {
        var bus = new Connection(address);
        await bus.ConnectAsync().ConfigureAwait(false);
        return bus;
    }
}
