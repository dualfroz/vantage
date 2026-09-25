using System.Text;
using Tmds.DBus.Protocol;
using Vantage.Core.Desktop;

namespace Vantage.Core.Secrets;

public sealed class SecretServiceStore : ISecretStore
{
    private const string Service = "org.freedesktop.secrets";
    private const string ServicePath = "/org/freedesktop/secrets";
    private const string ServiceInterface = "org.freedesktop.Secret.Service";
    private const string ItemInterface = "org.freedesktop.Secret.Item";
    private const string PromptInterface = "org.freedesktop.Secret.Prompt";
    private const string DefaultCollection = "/org/freedesktop/secrets/aliases/default";
    private const string NoObject = "/";

    private readonly SessionBus bus;
    private readonly Lazy<Task<string>> session;

    public SecretServiceStore(SessionBus bus)
    {
        this.bus = bus;
        session = new Lazy<Task<string>>(OpenSessionAsync);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await session.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
            var connection = await bus.ConnectionAsync().ConfigureAwait(false);
            var alias = await connection.CallMethodAsync(ReadAliasMessage(connection, "default"), ReadPath)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            return alias != NoObject;
        }
        catch (Exception ex) when (ex is DBusException or ConnectException or DisconnectedException or IOException)
        {
            return false;
        }
    }

    public async Task<string?> GetAsync(string reference, CancellationToken cancellationToken = default)
    {
        var connection = await bus.ConnectionAsync().ConfigureAwait(false);
        var (unlocked, locked) = await connection.CallMethodAsync(SearchMessage(connection, reference), ReadSearchReply).ConfigureAwait(false);
        if (unlocked.Count == 0 && locked.Count > 0)
            unlocked = await UnlockAsync(connection, locked, cancellationToken).ConfigureAwait(false);
        if (unlocked.Count == 0)
            return null;

        var sessionPath = await session.Value.ConfigureAwait(false);
        var secret = await connection.CallMethodAsync(GetSecretMessage(connection, unlocked[0], sessionPath), ReadSecretValue).ConfigureAwait(false);
        return Encoding.UTF8.GetString(secret);
    }

    public async Task SetAsync(string reference, string label, string secret, CancellationToken cancellationToken = default)
    {
        var connection = await bus.ConnectionAsync().ConfigureAwait(false);
        var sessionPath = await session.Value.ConfigureAwait(false);
        var prompt = await connection.CallMethodAsync(
            CreateItemMessage(connection, sessionPath, reference, label, secret),
            (message, _) =>
            {
                var reader = message.GetBodyReader();
                reader.ReadObjectPath();
                return reader.ReadObjectPath().ToString();
            }).ConfigureAwait(false);
        await CompletePromptAsync(connection, prompt, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string reference, CancellationToken cancellationToken = default)
    {
        var connection = await bus.ConnectionAsync().ConfigureAwait(false);
        var (unlocked, locked) = await connection.CallMethodAsync(SearchMessage(connection, reference), ReadSearchReply).ConfigureAwait(false);
        foreach (var item in unlocked.Concat(locked))
        {
            var prompt = await connection.CallMethodAsync(DeleteMessage(connection, item), ReadPath).ConfigureAwait(false);
            await CompletePromptAsync(connection, prompt, cancellationToken).ConfigureAwait(false);
        }
    }

    private static Dictionary<string, string> Attributes(string reference) => new()
    {
        ["application"] = "vantage",
        ["reference"] = reference
    };

    private async Task<string> OpenSessionAsync()
    {
        var connection = await bus.ConnectionAsync().ConfigureAwait(false);
        return await connection.CallMethodAsync(OpenSessionMessage(connection), (message, _) =>
        {
            var reader = message.GetBodyReader();
            reader.ReadVariant();
            return reader.ReadObjectPath().ToString();
        }).ConfigureAwait(false);
    }

    private static async Task<List<string>> UnlockAsync(Connection connection, IReadOnlyList<string> items, CancellationToken cancellationToken)
    {
        var (unlocked, prompt) = await connection.CallMethodAsync(UnlockMessage(connection, items), (message, _) =>
        {
            var reader = message.GetBodyReader();
            var paths = ReadPaths(ref reader);
            return (paths, reader.ReadObjectPath().ToString());
        }).ConfigureAwait(false);

        if (prompt == NoObject)
            return unlocked;

        await CompletePromptAsync(connection, prompt, cancellationToken).ConfigureAwait(false);
        return items.ToList();
    }

    private static async Task CompletePromptAsync(Connection connection, string prompt, CancellationToken cancellationToken)
    {
        if (prompt == NoObject)
            return;

        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var rule = new MatchRule
        {
            Type = MessageType.Signal,
            Interface = PromptInterface,
            Member = "Completed",
            Path = prompt
        };

        using var subscription = await connection.AddMatchAsync(
            rule,
            (message, _) => message.GetBodyReader().ReadBool(),
            (exception, dismissed, _, _) =>
            {
                if (exception is not null)
                    completed.TrySetException(exception);
                else
                    completed.TrySetResult(dismissed);
            },
            emitOnCapturedContext: false).ConfigureAwait(false);

        await connection.CallMethodAsync(PromptMessage(connection, prompt)).ConfigureAwait(false);

        if (await completed.Task.WaitAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("The keyring prompt was dismissed.");
    }

    private static MessageBuffer OpenSessionMessage(Connection connection)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(Service, ServicePath, ServiceInterface, "OpenSession", "sv");
        writer.WriteString("plain");
        writer.WriteVariantString("");
        return writer.CreateMessage();
    }

    private static MessageBuffer ReadAliasMessage(Connection connection, string alias)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(Service, ServicePath, ServiceInterface, "ReadAlias", "s");
        writer.WriteString(alias);
        return writer.CreateMessage();
    }

    private static MessageBuffer SearchMessage(Connection connection, string reference)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(Service, ServicePath, ServiceInterface, "SearchItems", "a{ss}");
        writer.WriteDictionary(Attributes(reference));
        return writer.CreateMessage();
    }

    private static MessageBuffer UnlockMessage(Connection connection, IReadOnlyList<string> items)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(Service, ServicePath, ServiceInterface, "Unlock", "ao");
        var array = writer.WriteArrayStart(DBusType.ObjectPath);
        foreach (var item in items)
            writer.WriteObjectPath(item);
        writer.WriteArrayEnd(array);
        return writer.CreateMessage();
    }

    private static MessageBuffer GetSecretMessage(Connection connection, string item, string sessionPath)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(Service, item, ItemInterface, "GetSecret", "o");
        writer.WriteObjectPath(sessionPath);
        return writer.CreateMessage();
    }

    private static MessageBuffer CreateItemMessage(Connection connection, string sessionPath, string reference, string label, string secret)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(Service, DefaultCollection, "org.freedesktop.Secret.Collection", "CreateItem", "a{sv}(oayays)b");
        var properties = writer.WriteArrayStart(DBusType.Struct);
        writer.WriteStructureStart();
        writer.WriteString("org.freedesktop.Secret.Item.Label");
        writer.WriteVariantString(label);
        writer.WriteStructureStart();
        writer.WriteString("org.freedesktop.Secret.Item.Attributes");
        writer.WriteSignature("a{ss}");
        writer.WriteDictionary(Attributes(reference));
        writer.WriteArrayEnd(properties);
        writer.WriteStructureStart();
        writer.WriteObjectPath(sessionPath);
        writer.WriteArray(Array.Empty<byte>());
        writer.WriteArray(Encoding.UTF8.GetBytes(secret));
        writer.WriteString("text/plain; charset=utf8");
        writer.WriteBool(true);
        return writer.CreateMessage();
    }

    private static MessageBuffer DeleteMessage(Connection connection, string item)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(Service, item, ItemInterface, "Delete");
        return writer.CreateMessage();
    }

    private static MessageBuffer PromptMessage(Connection connection, string prompt)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(Service, prompt, PromptInterface, "Prompt", "s");
        writer.WriteString("");
        return writer.CreateMessage();
    }

    private static string ReadPath(Message message, object? state) => message.GetBodyReader().ReadObjectPath().ToString();

    private static (List<string> Unlocked, List<string> Locked) ReadSearchReply(Message message, object? state)
    {
        var reader = message.GetBodyReader();
        var unlocked = ReadPaths(ref reader);
        var locked = ReadPaths(ref reader);
        return (unlocked, locked);
    }

    private static byte[] ReadSecretValue(Message message, object? state)
    {
        var reader = message.GetBodyReader();
        reader.AlignStruct();
        reader.ReadObjectPath();
        reader.ReadArray<byte>();
        return reader.ReadArray<byte>();
    }

    private static List<string> ReadPaths(ref Reader reader)
    {
        var paths = new List<string>();
        var end = reader.ReadArrayStart(DBusType.ObjectPath);
        while (reader.HasNext(end))
            paths.Add(reader.ReadObjectPath().ToString());
        return paths;
    }
}
