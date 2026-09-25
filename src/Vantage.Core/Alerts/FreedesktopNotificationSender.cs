using Tmds.DBus.Protocol;
using Vantage.Core.Desktop;

namespace Vantage.Core.Alerts;

public interface INotificationSender
{
    Task SendAsync(string title, string body, AlertSeverity severity, CancellationToken cancellationToken = default);
}

public sealed class FreedesktopNotificationSender : INotificationSender
{
    private readonly SessionBus bus;

    public FreedesktopNotificationSender(SessionBus bus)
    {
        this.bus = bus;
    }

    public async Task SendAsync(string title, string body, AlertSeverity severity, CancellationToken cancellationToken = default)
    {
        var connection = await bus.ConnectionAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        await connection.CallMethodAsync(NotifyMessage(connection, title, body, severity), (message, _) => message.GetBodyReader().ReadUInt32())
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public static byte Urgency(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => 2,
        AlertSeverity.Warning => 1,
        _ => 0
    };

    public static string EscapeMarkup(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static MessageBuffer NotifyMessage(Connection connection, string title, string body, AlertSeverity severity)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(
            "org.freedesktop.Notifications",
            "/org/freedesktop/Notifications",
            "org.freedesktop.Notifications",
            "Notify",
            "susssasa{sv}i");
        writer.WriteString("Vantage");
        writer.WriteUInt32(0);
        writer.WriteString("");
        writer.WriteString(title);
        writer.WriteString(EscapeMarkup(body));
        writer.WriteArray(Array.Empty<string>());
        var hints = writer.WriteArrayStart(DBusType.Struct);
        writer.WriteStructureStart();
        writer.WriteString("urgency");
        writer.WriteVariantByte(Urgency(severity));
        writer.WriteArrayEnd(hints);
        writer.WriteInt32(-1);
        return writer.CreateMessage();
    }
}
