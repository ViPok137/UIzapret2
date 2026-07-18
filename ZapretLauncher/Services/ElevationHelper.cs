using System.Security.Principal;

namespace ZapretLauncher.Services;

public static class ElevationHelper
{
    /// <summary>Запущен ли текущий процесс с правами локального администратора.</summary>
    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
