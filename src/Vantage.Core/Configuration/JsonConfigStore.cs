using System.Text.Json;
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

    public JsonConfigStore(string path)
    {
        Path = path;
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

        await using var stream = File.OpenRead(Path);
        var config = await JsonSerializer
            .DeserializeAsync<VantageConfig>(stream, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);
        return config ?? new VantageConfig();
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
