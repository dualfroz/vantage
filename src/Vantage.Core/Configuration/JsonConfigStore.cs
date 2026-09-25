using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Vantage.Core.Models;

namespace Vantage.Core.Configuration;

public sealed class JsonConfigStore : IConfigStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string homeDirectory;

    public JsonConfigStore(string path, string? homeDirectory = null)
    {
        Path = path;
        this.homeDirectory = homeDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    public string Path { get; }

    public static string DefaultPath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return System.IO.Path.Combine(root, "vantage", "config.json");
    }

    public async Task<VantageConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(Path))
            return new VantageConfig();

        var text = await File.ReadAllTextAsync(Path, cancellationToken).ConfigureAwait(false);
        if (JsonNode.Parse(text) is not { } root)
            return new VantageConfig();

        var notices = LegacyConfigMigration.Apply(root, homeDirectory);
        var config = root.Deserialize<VantageConfig>(SerializerOptions) ?? new VantageConfig();
        config.LoadNotices.AddRange(notices);
        config.MigratedOnLoad = notices.Count > 0;
        return config;
    }

    public async Task SaveAsync(VantageConfig config, CancellationToken cancellationToken = default)
    {
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await using var stream = File.Create(Path);
        await JsonSerializer.SerializeAsync(stream, config, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }
}
