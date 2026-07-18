using System.Text.Json;
using ZapretLauncher.Models;

namespace ZapretLauncher.Services;

/// <summary>
/// Отвечает за чтение и запись config.json (п. 3.4 ТЗ). Файл лежит рядом с exe.
/// </summary>
public class ConfigManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public string ConfigPath { get; }

    public ConfigManager(string? configPath = null)
    {
        ConfigPath = configPath ?? Path.Combine(AppContext.BaseDirectory, "config.json");
    }

    /// <summary>Загружает config.json; если файла нет или он повреждён — создаёт конфиг по умолчанию и сохраняет его.</summary>
    public AppConfig LoadOrCreateDefault()
    {
        if (!File.Exists(ConfigPath))
        {
            var fresh = CreateDefault();
            Save(fresh);
            return fresh;
        }

        try
        {
            var json = File.ReadAllText(ConfigPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            if (config is null || config.Presets.Count == 0)
                return CreateDefault();
            return config;
        }
        catch (Exception)
        {
            // Повреждённый config.json — не роняем приложение, откатываемся на дефолт.
            // Файл при этом не перезаписываем молча, чтобы не потерять пользовательские правки —
            // это решает MainForm (может предложить пользователю восстановить дефолт).
            return CreateDefault();
        }
    }

    public void Save(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        var tempPath = ConfigPath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, ConfigPath, overwrite: true);
    }

    /// <summary>Дефолтный конфиг — пресеты списаны буквально из примера в разделе 5 ТЗ.</summary>
    public static AppConfig CreateDefault() => new()
    {
        Version = "0.0.0",
        AutoUpdate = true,
        SelectedPreset = "youtube_discord",
        Autostart = false,
        UpdateRepoOwner = "YOUR_GITHUB_USERNAME",
        UpdateRepoName = "my-zapret-core",
        Presets = new List<Preset>
        {
            new Preset
            {
                Id = "youtube_discord",
                Name = "YouTube + Discord",
                Args = "--wf-l3=ipv4 --wf-tcp=80,443 --wf-udp=443 --dpi-desync=split2 " +
                       "--dpi-desync-split-pos=2 --dpi-desync-any-protocol=1",
            },
            new Preset
            {
                Id = "hostlist_only",
                Name = "Только сайты из списка",
                Args = "--wf-l3=ipv4 --wf-tcp=80,443 --hostlist=list.txt --dpi-desync=split2",
            },
        },
    };
}
