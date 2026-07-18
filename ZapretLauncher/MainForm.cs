using System.Drawing;
using System.Windows.Forms;
using ZapretLauncher.Models;
using ZapretLauncher.Services;

namespace ZapretLauncher;

public class MainForm : Form
{
    private readonly bool _elevationDeclined;

    private readonly ConfigManager _configManager = new();
    private readonly ProcessManager _processManager = new();
    private readonly UpdateService _updateService = new();
    private AppConfig _config = ConfigManager.CreateDefault();

    // true во время программного заполнения контролов при загрузке — чтобы
    // обработчики "изменилось значение" не сохраняли конфиг и не дёргали
    // перезапуск процесса на пустом месте при первой отрисовке формы.
    private bool _suppressChangeEvents;

    // true только когда закрытие инициировано пунктом "Выход" из трея (или ОС).
    // Во всех остальных случаях закрытие окна прячет его в трей (см. п.3.5 ТЗ).
    private bool _reallyClose;

    private Label _statusDot = null!;
    private Label _statusLabel = null!;
    private Button _toggleButton = null!;
    private ComboBox _presetCombo = null!;
    private CheckBox _autostartCheck = null!;
    private CheckBox _autoUpdateCheck = null!;
    private Button _editListButton = null!;
    private Button _checkUpdateButton = null!;
    private Label _versionLabel = null!;
    private Label _adminLabel = null!;

    private NotifyIcon _trayIcon = null!;
    private ContextMenuStrip _trayMenu = null!;
    private ToolStripMenuItem _trayToggleItem = null!;
    private ToolStripMenuItem _trayPresetsItem = null!;

    private static string BinDir => Path.Combine(AppContext.BaseDirectory, "bin");
    private static string EnginePath => Path.Combine(BinDir, "winws.exe");
    private static string ListPath => Path.Combine(AppContext.BaseDirectory, "list.txt");

    public MainForm(bool elevationDeclined)
    {
        _elevationDeclined = elevationDeclined;

        BuildUi();

        _processManager.StatusChanged += (_, status) =>
        {
            if (InvokeRequired) BeginInvoke(new Action(() => ApplyStatus(status)));
            else ApplyStatus(status);
        };
        _processManager.LogMessage += (_, line) => AppendLog(line);

        Load += MainForm_Load;
        FormClosing += MainForm_FormClosing;
    }

    // ---------------------------------------------------------------- UI --

    private void BuildUi()
    {
        Text = "Zapret Launcher";
        ClientSize = new Size(420, 440);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Font = new Font("Segoe UI", 9F);

        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
        catch { /* иконка не обязательна для работы: встраивается при сборке через ApplicationIcon в .csproj */ }

        var title = new Label
        {
            Text = "Zapret Launcher",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            AutoSize = true,
            Left = 20,
            Top = 15,
        };
        Controls.Add(title);

        _statusDot = new Label { Left = 22, Top = 57, Width = 12, Height = 12, BackColor = Color.Gray, AutoSize = false };
        Controls.Add(_statusDot);

        _statusLabel = new Label { Left = 42, Top = 53, Width = 250, Height = 20, Text = "Остановлен", AutoSize = false };
        Controls.Add(_statusLabel);

        _toggleButton = new Button
        {
            Left = 20,
            Top = 82,
            Width = 380,
            Height = 42,
            Text = "Включить обход",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
        };
        _toggleButton.Click += ToggleButton_Click;
        Controls.Add(_toggleButton);

        var presetLabel = new Label { Left = 20, Top = 138, Width = 200, Text = "Пресет:" };
        Controls.Add(presetLabel);

        _presetCombo = new ComboBox
        {
            Left = 20,
            Top = 158,
            Width = 380,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _presetCombo.SelectedIndexChanged += PresetCombo_SelectedIndexChanged;
        Controls.Add(_presetCombo);

        _autostartCheck = new CheckBox { Left = 20, Top = 196, Width = 380, Height = 24, AutoSize = false, Text = "Запускать вместе с Windows" };
        _autostartCheck.CheckedChanged += AutostartCheck_CheckedChanged;
        Controls.Add(_autostartCheck);

        _autoUpdateCheck = new CheckBox { Left = 20, Top = 222, Width = 380, Height = 24, AutoSize = false, Text = "Проверять обновления при запуске" };
        _autoUpdateCheck.CheckedChanged += AutoUpdateCheck_CheckedChanged;
        Controls.Add(_autoUpdateCheck);

        _editListButton = new Button { Left = 20, Top = 260, Width = 380, Height = 32, Text = "Редактировать список доменов..." };
        _editListButton.Click += EditListButton_Click;
        Controls.Add(_editListButton);

        _checkUpdateButton = new Button { Left = 20, Top = 298, Width = 380, Height = 32, Text = "Проверить обновления" };
        _checkUpdateButton.Click += CheckUpdateButton_Click;
        Controls.Add(_checkUpdateButton);

        _versionLabel = new Label { Left = 20, Top = 344, Width = 380, Height = 18, Text = "Версия ядра: —", ForeColor = Color.Gray, AutoSize = false };
        Controls.Add(_versionLabel);

        _adminLabel = new Label
        {
            Left = 20,
            Top = 366,
            Width = 380,
            Height = 18,
            AutoSize = false,
            Text = _elevationDeclined ? "Запущено БЕЗ прав администратора" : "Запущено с правами администратора",
            ForeColor = _elevationDeclined ? Color.DarkOrange : Color.Gray,
        };
        Controls.Add(_adminLabel);

        BuildTrayIcon();
    }

    private void BuildTrayIcon()
    {
        _trayMenu = new ContextMenuStrip();

        var trayOpen = new ToolStripMenuItem("Открыть", null, (_, _) => ShowFromTray());
        _trayToggleItem = new ToolStripMenuItem("Включить обход", null, ToggleButton_Click);
        _trayPresetsItem = new ToolStripMenuItem("Пресеты");
        var traySeparator = new ToolStripSeparator();
        var trayExit = new ToolStripMenuItem("Выход", null, (_, _) =>
        {
            _reallyClose = true;
            Close();
        });

        _trayMenu.Items.Add(trayOpen);
        _trayMenu.Items.Add(_trayToggleItem);
        _trayMenu.Items.Add(_trayPresetsItem);
        _trayMenu.Items.Add(traySeparator);
        _trayMenu.Items.Add(trayExit);

        _trayIcon = new NotifyIcon
        {
            Icon = Icon ?? SystemIcons.Shield,
            Text = "Zapret Launcher",
            ContextMenuStrip = _trayMenu,
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => ShowFromTray();
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    // ------------------------------------------------------------- Load --

    private async void MainForm_Load(object? sender, EventArgs e)
    {
        _suppressChangeEvents = true;

        _config = _configManager.LoadOrCreateDefault();
        _updateService.RepoOwner = _config.UpdateRepoOwner;
        _updateService.RepoName = _config.UpdateRepoName;

        PopulatePresets();
        _autostartCheck.Checked = AutostartManager.IsEnabled();
        _autoUpdateCheck.Checked = _config.AutoUpdate;
        _versionLabel.Text = $"Версия ядра: {_config.Version}";
        ApplyStatus(_processManager.Status);

        _suppressChangeEvents = false;

        if (_elevationDeclined)
        {
            MessageBox.Show(
                this,
                "Права администратора не были предоставлены. Драйверу WinDivert для работы требуются права " +
                "администратора, поэтому запуск обхода блокировок недоступен. Перезапустите приложение и " +
                "подтвердите запрос UAC, чтобы включить эту возможность.",
                "Недостаточно прав",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            _toggleButton.Enabled = false;
            _trayToggleItem.Enabled = false;
        }

        if (_config.AutoUpdate)
            await CheckForUpdatesAsync(silent: true);
    }

    private void PopulatePresets()
    {
        _presetCombo.Items.Clear();
        foreach (var preset in _config.Presets)
            _presetCombo.Items.Add(preset);

        var selected = _config.GetSelectedPreset();
        if (selected != null)
            _presetCombo.SelectedItem = selected;
        else if (_presetCombo.Items.Count > 0)
            _presetCombo.SelectedIndex = 0;

        _trayPresetsItem.DropDownItems.Clear();
        foreach (var preset in _config.Presets)
        {
            var item = new ToolStripMenuItem(preset.Name) { Checked = preset.Id == _config.SelectedPreset };
            item.Click += (_, _) => _presetCombo.SelectedItem = preset;
            _trayPresetsItem.DropDownItems.Add(item);
        }
    }

    // --------------------------------------------------------- Handlers --

    private void ToggleButton_Click(object? sender, EventArgs e)
    {
        if (_elevationDeclined)
        {
            MessageBox.Show(this, "Для запуска обхода нужны права администратора. Перезапустите приложение и подтвердите запрос UAC.",
                "Недостаточно прав", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_processManager.Status == EngineStatus.Running)
        {
            _processManager.Stop();
            return;
        }

        var preset = _config.GetSelectedPreset();
        if (preset is null)
        {
            MessageBox.Show(this, "Не выбран пресет. Выберите пресет из списка.", "Нет пресета",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            _processManager.Start(EnginePath, preset.Args, BinDir);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Не удалось запустить обход", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void PresetCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suppressChangeEvents) return;
        if (_presetCombo.SelectedItem is not Preset preset) return;

        _config.SelectedPreset = preset.Id;
        _configManager.Save(_config);

        foreach (ToolStripMenuItem item in _trayPresetsItem.DropDownItems)
            item.Checked = item.Text == preset.Name;

        if (_processManager.Status == EngineStatus.Running)
        {
            try
            {
                _processManager.Restart(EnginePath, preset.Args, BinDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Не удалось перезапустить с новым пресетом",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void AutostartCheck_CheckedChanged(object? sender, EventArgs e)
    {
        if (_suppressChangeEvents) return;

        AutostartManager.SetEnabled(_autostartCheck.Checked, Application.ExecutablePath);
        _config.Autostart = _autostartCheck.Checked;
        _configManager.Save(_config);
    }

    private void AutoUpdateCheck_CheckedChanged(object? sender, EventArgs e)
    {
        if (_suppressChangeEvents) return;

        _config.AutoUpdate = _autoUpdateCheck.Checked;
        _configManager.Save(_config);
    }

    private void EditListButton_Click(object? sender, EventArgs e)
    {
        using var editor = new ListEditorForm(ListPath);
        editor.ShowDialog(this);
    }

    private async void CheckUpdateButton_Click(object? sender, EventArgs e) => await CheckForUpdatesAsync(silent: false);

    // ------------------------------------------------------------ Update --

    private async Task CheckForUpdatesAsync(bool silent)
    {
        _checkUpdateButton.Enabled = false;
        var originalText = _checkUpdateButton.Text;

        try
        {
            ReleaseInfo? release;
            try
            {
                release = await _updateService.GetLatestReleaseAsync();
            }
            catch (Exception ex)
            {
                if (!silent)
                    MessageBox.Show(this, $"Не удалось проверить обновления: {ex.Message}", "Обновление",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (release is null)
            {
                if (!silent)
                    MessageBox.Show(this,
                        "Не удалось получить сведения о релизе. Проверьте адрес репозитория в config.json " +
                        "(update_repo_owner / update_repo_name).",
                        "Обновление", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!UpdateService.IsNewer(release.TagName, _config.Version))
            {
                if (!silent)
                    MessageBox.Show(this, $"Установлена последняя версия ({_config.Version}).", "Обновление",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (release.ZipAssetUrl is null)
            {
                if (!silent)
                    MessageBox.Show(this,
                        $"Найдена новая версия {release.TagName}, но в релизе нет zip-архива с бинарниками.",
                        "Обновление", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                this,
                $"Доступна новая версия: {release.TagName} (сейчас: {_config.Version}).\n" +
                "Обход будет остановлен на время установки. Установить сейчас?",
                "Доступно обновление",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm == DialogResult.Yes)
                await InstallUpdateAsync(release);
        }
        finally
        {
            _checkUpdateButton.Enabled = true;
            _checkUpdateButton.Text = originalText;
        }
    }

    private async Task InstallUpdateAsync(ReleaseInfo release)
    {
        var wasRunning = _processManager.Status == EngineStatus.Running;
        _processManager.Stop(); // п.3.3, шаг 1 — остановить активный процесс

        try
        {
            var tempZip = Path.Combine(Path.GetTempPath(), $"zapret_update_{Guid.NewGuid():N}.zip");
            var progress = new Progress<int>(p => _checkUpdateButton.Text = $"Загрузка... {p}%");
            await _updateService.DownloadToFileAsync(release.ZipAssetUrl!, tempZip, progress); // шаг 2

            _checkUpdateButton.Text = "Установка...";
            _updateService.ExtractAndReplace(tempZip, BinDir); // шаг 3 — распаковка и замена файлов в /bin/
            File.Delete(tempZip);

            _config.Version = release.TagName;
            _configManager.Save(_config);
            _versionLabel.Text = $"Версия ядра: {_config.Version}";

            if (wasRunning) // шаг 4 — перезапуск с сохранением выбранных настроек
            {
                var preset = _config.GetSelectedPreset();
                if (preset != null)
                    _processManager.Start(EnginePath, preset.Args, BinDir);
            }

            MessageBox.Show(this, $"Обновление до версии {release.TagName} установлено.", "Готово",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Не удалось установить обновление: {ex.Message}", "Ошибка обновления",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ------------------------------------------------------------ Status --

    private void ApplyStatus(EngineStatus status)
    {
        var (color, text) = status switch
        {
            EngineStatus.Running => (Color.FromArgb(46, 160, 67), "Активен"),
            EngineStatus.Error => (Color.FromArgb(218, 54, 51), "Ошибка"),
            _ => (Color.Gray, "Остановлен"),
        };

        _statusDot.BackColor = color;
        _statusLabel.Text = text;

        var toggleText = status == EngineStatus.Running ? "Выключить обход" : "Включить обход";
        _toggleButton.Text = toggleText;
        _trayToggleItem.Text = toggleText;
        _trayIcon.Text = $"Zapret Launcher — {text}";
    }

    private void AppendLog(string line)
    {
        try
        {
            var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logsDir);
            File.AppendAllText(
                Path.Combine(logsDir, "winws.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {line}{Environment.NewLine}");
        }
        catch
        {
            // Логирование не должно ронять приложение.
        }
    }

    // ------------------------------------------------------------- Close --

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_reallyClose || e.CloseReason == CloseReason.WindowsShutDown)
            return;

        // п.3.5 ТЗ: закрытие главного окна сворачивает приложение в трей, а не завершает его.
        e.Cancel = true;
        Hide();
        _trayIcon.ShowBalloonTip(1500, "Zapret Launcher",
            "Приложение свёрнуто в трей. Обход продолжает работать в фоне.", ToolTipIcon.Info);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _processManager.Stop();
        _processManager.Dispose();
        base.OnFormClosed(e);
    }
}
