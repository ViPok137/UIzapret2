using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;
using ZapretLauncher.Services;

namespace ZapretLauncher;

internal static class Program
{
    /// <summary>Код ошибки Win32, который ShellExecute возвращает, когда пользователь
    /// нажал "Нет" в диалоге UAC (ERROR_CANCELLED).</summary>
    private const int ErrorCancelled = 1223;

    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.ThreadException += (_, e) =>
            MessageBox.Show(e.Exception.ToString(), "Необработанная ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);

        var isAdmin = ElevationHelper.IsAdministrator();
        var elevationDeclined = false;

        // п. 3.1 ТЗ: при старте приложение обязано запросить повышение прав.
        // Делаем это через runas, а не через манифест requireAdministrator — см.
        // комментарий в app.manifest, почему именно так.
        if (!isAdmin)
        {
            if (TryRelaunchElevated())
            {
                // Новый повышенный процесс запущен — этот, неповышенный, экземпляр
                // просто завершает работу, дальше работает только новый процесс.
                return;
            }

            elevationDeclined = true;
        }

        Application.Run(new MainForm(elevationDeclined));
    }

    /// <summary>
    /// Пытается перезапустить текущий exe с запросом прав администратора.
    /// Возвращает true, если новый процесс успешно стартовал (тогда текущий
    /// процесс должен завершиться). Возвращает false, если пользователь отклонил
    /// запрос UAC — тогда приложение продолжает работу без повышения.
    /// </summary>
    private static bool TryRelaunchElevated()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath ?? Application.ExecutablePath,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory,
            };
            Process.Start(psi);
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            // Пользователь нажал "Нет" в UAC — это ожидаемый сценарий, не ошибка.
            return false;
        }
    }
}
