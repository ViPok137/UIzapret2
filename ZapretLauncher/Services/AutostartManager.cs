using Microsoft.Win32;

namespace ZapretLauncher.Services;

/// <summary>
/// Переключатель автозапуска лаунчера вместе с Windows (п. 3.2 ТЗ) через
/// стандартный Run-ключ текущего пользователя. Права администратора для
/// записи в HKCU не требуются, поэтому переключать автозапуск можно даже
/// в неповышенном режиме (сам обход при этом всё равно останется недоступен
/// без UAC — см. ElevationHelper и MainForm).
/// </summary>
public static class AutostartManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ZapretLauncher";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string;
    }

    public static void SetEnabled(bool enabled, string executablePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (key is null)
            return;

        if (enabled)
        {
            key.SetValue(ValueName, $"\"{executablePath}\"", RegistryValueKind.String);
        }
        else if (key.GetValue(ValueName) != null)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
