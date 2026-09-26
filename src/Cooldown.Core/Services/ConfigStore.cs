using System.Text.Json;
using System.Text.Json.Serialization;
using Cooldown.Core.Models;

namespace Cooldown.Core.Services;

public static class ConfigStore
{
    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Loads config.json, creating it from <paramref name="createDefault"/> on first run.</summary>
    public static CooldownConfig LoadOrCreate(string path, Func<CooldownConfig> createDefault)
    {
        if (File.Exists(path))
        {
            var loaded = JsonSerializer.Deserialize<CooldownConfig>(File.ReadAllText(path), Json);
            if (loaded is not null)
                return loaded;
        }

        var config = createDefault();
        Save(path, config);
        return config;
    }

    public static void Save(string path, CooldownConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(config, Json));
    }
}
