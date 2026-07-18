using System.Text.Json.Serialization;

namespace ZapretLauncher.Models;

/// <summary>
/// Корневая структура config.json — соответствует схеме из раздела 5 ТЗ.
/// Поля UpdateRepoOwner/UpdateRepoName — расширение сверх исходной схемы:
/// без них некуда было бы положить адрес вашего форка my-zapret-core
/// для автообновителя. Остальные поля — один в один как в ТЗ.
/// </summary>
public class AppConfig
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "0.0.0";

    [JsonPropertyName("auto_update")]
    public bool AutoUpdate { get; set; } = true;

    [JsonPropertyName("selected_preset")]
    public string SelectedPreset { get; set; } = "";

    [JsonPropertyName("presets")]
    public List<Preset> Presets { get; set; } = new();

    /// <summary>Владелец (пользователь/организация) GitHub-репозитория my-zapret-core.</summary>
    [JsonPropertyName("update_repo_owner")]
    public string UpdateRepoOwner { get; set; } = "YOUR_GITHUB_USERNAME";

    /// <summary>Имя GitHub-репозитория my-zapret-core.</summary>
    [JsonPropertyName("update_repo_name")]
    public string UpdateRepoName { get; set; } = "my-zapret-core";

    /// <summary>Автозапуск лаунчера вместе с Windows. Источник истины — реестр (HKCU\...\Run,
    /// см. AutostartManager); при загрузке формы состояние чекбокса читается именно оттуда.
    /// Это поле в config.json — просто отражение последнего известного состояния для
    /// удобства чтения файла человеком, самим приложением на старте не используется.</summary>
    [JsonPropertyName("autostart")]
    public bool Autostart { get; set; } = false;

    public Preset? GetSelectedPreset() => Presets.FirstOrDefault(p => p.Id == SelectedPreset);
}

public class Preset
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("args")]
    public string Args { get; set; } = "";

    public override string ToString() => Name;
}
