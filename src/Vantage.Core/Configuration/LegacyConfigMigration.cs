using System.Text.Json.Nodes;

namespace Vantage.Core.Configuration;

public static class LegacyConfigMigration
{
    private static readonly string[] DefaultKeyNames = { "id_ed25519", "id_ecdsa", "id_rsa" };

    public static IReadOnlyList<string> Apply(JsonNode root, string homeDirectory)
    {
        var notices = new List<string>();
        if (root["hosts"] is not JsonArray hosts)
            return notices;

        foreach (var host in hosts.OfType<JsonObject>())
        {
            if (!string.Equals(Text(host, "authMethod"), "agent", StringComparison.OrdinalIgnoreCase))
                continue;

            host["authMethod"] = "privateKey";
            var name = Text(host, "name") is { Length: > 0 } hostName ? hostName : Text(host, "address") ?? "A host";
            var lead = $"{name} used SSH agent authentication, which Vantage does not support.";

            if (Text(host, "privateKeyPath") is { Length: > 0 } existingKey)
            {
                notices.Add($"{lead} It now uses its configured private key {existingKey}.");
            }
            else if (DefaultKey(homeDirectory) is { } defaultKey)
            {
                host["privateKeyPath"] = defaultKey;
                notices.Add($"{lead} It now uses the default key {defaultKey}; pick another key in the host editor if the agent held a different one.");
            }
            else
            {
                notices.Add($"{lead} No default key was found in ~/.ssh, so set a private key in the host editor before it can connect.");
            }
        }

        return notices;
    }

    private static string? Text(JsonObject node, string property) =>
        node[property] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static string? DefaultKey(string homeDirectory) => DefaultKeyNames
        .Where(name => File.Exists(Path.Combine(homeDirectory, ".ssh", name)))
        .Select(name => "~/.ssh/" + name)
        .FirstOrDefault();
}
