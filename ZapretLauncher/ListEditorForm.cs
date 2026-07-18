using System.Drawing;
using System.Windows.Forms;

namespace ZapretLauncher;

/// <summary>
/// Простой текстовый редактор файла list.txt — список доменов для пресетов
/// с --hostlist (п. 3.4 ТЗ). Один домен на строку.
/// </summary>
public class ListEditorForm : Form
{
    private readonly string _filePath;
    private TextBox _textBox = null!;
    private Label _countLabel = null!;
    private Button _saveButton = null!;

    public ListEditorForm(string filePath)
    {
        _filePath = filePath;
        BuildUi();
        Load += (_, _) => LoadFile();
    }

    private void BuildUi()
    {
        Text = "Список доменов — list.txt";
        ClientSize = new Size(480, 460);
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(360, 300);

        _textBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            AcceptsReturn = true,
            AcceptsTab = false,
            WordWrap = false,
            Left = 12,
            Top = 12,
            Width = ClientSize.Width - 24,
            Height = ClientSize.Height - 90,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            Font = new Font(FontFamily.GenericMonospace, 9.5F),
        };
        _textBox.TextChanged += (_, _) => UpdateCount();
        Controls.Add(_textBox);

        _countLabel = new Label
        {
            Left = 12,
            Top = ClientSize.Height - 70,
            Width = 250,
            Height = 18,
            AutoSize = false,
            Text = "Строк: 0",
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
            ForeColor = Color.Gray,
        };
        Controls.Add(_countLabel);

        var hint = new Label
        {
            Left = 12,
            Top = ClientSize.Height - 50,
            Width = ClientSize.Width - 24,
            Height = 18,
            AutoSize = false,
            Text = "По одному домену на строку, например: youtube.com",
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            ForeColor = Color.Gray,
        };
        Controls.Add(hint);

        var cancelButton = new Button
        {
            Text = "Закрыть",
            Left = ClientSize.Width - 12 - 90,
            Top = ClientSize.Height - 12 - 30,
            Width = 90,
            Height = 30,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            DialogResult = DialogResult.Cancel,
        };
        Controls.Add(cancelButton);

        _saveButton = new Button
        {
            Text = "Сохранить",
            Left = cancelButton.Left - 12 - 90,
            Top = cancelButton.Top,
            Width = 90,
            Height = 30,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
        };
        _saveButton.Click += (_, _) => SaveFile();
        Controls.Add(_saveButton);

        CancelButton = cancelButton;
        AcceptButton = _saveButton;
    }

    private void LoadFile()
    {
        try
        {
            if (File.Exists(_filePath))
                _textBox.Text = File.ReadAllText(_filePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Не удалось загрузить список: {ex.Message}", "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        UpdateCount();
    }

    private void SaveFile()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(_filePath, _textBox.Text);
            _countLabel.Text = $"Строк: {CountNonEmptyLines()} — сохранено";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Не удалось сохранить список: {ex.Message}", "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private int CountNonEmptyLines() => _textBox.Lines.Count(l => !string.IsNullOrWhiteSpace(l));

    private void UpdateCount() => _countLabel.Text = $"Строк: {CountNonEmptyLines()}";
}
