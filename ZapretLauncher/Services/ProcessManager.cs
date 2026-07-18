using System.Diagnostics;

namespace ZapretLauncher.Services;

public enum EngineStatus
{
    Stopped,
    Running,
    Error,
}

/// <summary>
/// Управляет фоновым процессом winws.exe (п. 3.2 ТЗ): запуск полностью скрытым
/// (CreateNoWindow = true, без консольного окна), остановка, отслеживание статуса.
///
/// Это НЕ настоящая служба Windows (SCM) — winws.exe запускается и живёт как дочерний
/// процесс лаунчера. Это сознательное упрощение: полноценная Windows-служба потребовала
/// бы отдельного service-хоста и IPC между службой и GUI, что для v1 избыточно (в
/// zapret-win-bundle уже есть свои service*.cmd на базе штатной регистрации службы
/// WinDivert-драйвером — при необходимости на них можно опереться отдельным этапом).
/// Если процесс убьют/закроют лаунчер — winws.exe завершится вместе с ним.
/// </summary>
public class ProcessManager : IDisposable
{
    private Process? _process;
    private volatile bool _intentionalStop;

    public EngineStatus Status { get; private set; } = EngineStatus.Stopped;
    public event EventHandler<EngineStatus>? StatusChanged;
    public event EventHandler<string>? LogMessage;

    public void Start(string exePath, string arguments, string workingDirectory)
    {
        if (Status == EngineStatus.Running)
            return;

        if (!File.Exists(exePath))
        {
            SetStatus(EngineStatus.Error);
            throw new FileNotFoundException(
                $"Не найден исполняемый файл движка: {exePath}. Поместите winws.exe и файлы WinDivert в папку bin рядом с лаунчером (или выполните обновление).",
                exePath);
        }

        try
        {
            _intentionalStop = false;

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            proc.OutputDataReceived += (_, e) => { if (e.Data != null) LogMessage?.Invoke(this, e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) LogMessage?.Invoke(this, e.Data); };
            proc.Exited += Process_Exited;

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            _process = proc;
            SetStatus(EngineStatus.Running);
        }
        catch
        {
            SetStatus(EngineStatus.Error);
            throw;
        }
    }

    public void Stop()
    {
        var proc = _process;
        if (proc is null || proc.HasExited)
        {
            SetStatus(EngineStatus.Stopped);
            return;
        }

        try
        {
            _intentionalStop = true;
            proc.Kill(entireProcessTree: true);
            proc.WaitForExit(3000);
        }
        catch
        {
            // процесс мог завершиться сам между проверкой HasExited и Kill — это не ошибка
        }
        finally
        {
            SetStatus(EngineStatus.Stopped);
        }
    }

    /// <summary>Перезапуск с новыми аргументами (например, после смены пресета или после обновления).</summary>
    public void Restart(string exePath, string arguments, string workingDirectory)
    {
        var wasRunning = Status == EngineStatus.Running;
        Stop();
        if (wasRunning)
            Start(exePath, arguments, workingDirectory);
    }

    private void Process_Exited(object? sender, EventArgs e)
    {
        SetStatus(_intentionalStop ? EngineStatus.Stopped : EngineStatus.Error);
    }

    private void SetStatus(EngineStatus status)
    {
        Status = status;
        StatusChanged?.Invoke(this, status);
    }

    public void Dispose()
    {
        Stop();
        _process?.Dispose();
    }
}
