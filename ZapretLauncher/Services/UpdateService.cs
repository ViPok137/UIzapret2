using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ZapretLauncher.Services;

public class ReleaseInfo
{
    public required string TagName { get; init; }
    public string? ZipAssetUrl { get; init; }
    public string? ZipAssetName { get; init; }
    public long ZipAssetSize { get; init; }
}

/// <summary>
/// Модуль автообновления (п. 3.3 ТЗ). Обращается к GitHub REST API
/// (GET /repos/{owner}/{repo}/releases/latest) собственного репозитория
/// my-zapret-core, скачивает zip-ассет последнего релиза и раскладывает
/// его в рабочую папку /bin/.
///
/// GitHub API требует заголовок User-Agent на любой запрос — без него
/// отдаёт 403. Аутентификация не нужна: и чтение публичного релиза,
/// и скачивание ассета по browser_download_url работают анонимно
/// (см. docs.github.com/rest/releases).
/// </summary>
public class UpdateService
{
    public string RepoOwner { get; set; } = "YOUR_GITHUB_USERNAME";
    public string RepoName { get; set; } = "my-zapret-core";

    private readonly HttpClient _http;

    public UpdateService()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ZapretLauncher", "1.0"));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>Запрашивает информацию о последнем релизе. Возвращает null, если репозиторий/релиз недоступен.</summary>
    public async Task<ReleaseInfo?> GetLatestReleaseAsync(CancellationToken ct = default)
    {
        var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

        using var response = await _http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;

        var tagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";

        string? zipUrl = null;
        string? zipName = null;
        long zipSize = 0;

        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    continue;

                zipUrl = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                zipName = name;
                zipSize = asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
                break; // берём первый .zip-ассет релиза
            }
        }

        if (string.IsNullOrEmpty(tagName))
            return null;

        return new ReleaseInfo
        {
            TagName = tagName,
            ZipAssetUrl = zipUrl,
            ZipAssetName = zipName,
            ZipAssetSize = zipSize,
        };
    }

    /// <summary>
    /// Грубое сравнение версий: снимает необязательный префикс 'v', пытается
    /// сравнить как System.Version (major.minor.build.revision); если формат
    /// тегов не числовой (например, с суффиксом -beta) — считает версии разными,
    /// если строки не совпадают буквально.
    /// </summary>
    public static bool IsNewer(string remoteTag, string localVersion)
    {
        static string Clean(string s) => s.Trim().TrimStart('v', 'V');

        var remote = Clean(remoteTag);
        var local = Clean(localVersion);

        if (Version.TryParse(remote, out var remoteVersion) && Version.TryParse(local, out var localVersion2))
            return remoteVersion > localVersion2;

        return !string.Equals(remote, local, StringComparison.OrdinalIgnoreCase);
    }

    public async Task DownloadToFileAsync(string url, string destinationPath, IProgress<int>? progress, CancellationToken ct = default)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;

        await using var httpStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[81920];
        long readSoFar = 0;
        int bytesRead;
        while ((bytesRead = await httpStream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            readSoFar += bytesRead;
            if (totalBytes > 0)
                progress?.Report((int)(readSoFar * 100 / totalBytes));
        }
    }

    /// <summary>
    /// Распаковывает скачанный zip во временную папку и копирует содержимое в binDir,
    /// перезаписывая существующие файлы. Работает и если в архиве всё лежит плоско,
    /// и если есть один общий подкаталог — в обоих случаях итог одинаковый:
    /// файлы оказываются прямо в binDir.
    /// </summary>
    public void ExtractAndReplace(string zipPath, string binDir)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ZapretLauncherUpdate_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            ZipFile.ExtractToDirectory(zipPath, tempDir, overwriteFiles: true);

            var sourceRoot = tempDir;
            var entries = Directory.GetFileSystemEntries(tempDir);
            // Если архив содержит ровно один каталог верхнего уровня — считаем его "оболочкой"
            // и берём файлы из него, а не создаём лишний уровень вложенности в bin/.
            if (entries.Length == 1 && Directory.Exists(entries[0]))
                sourceRoot = entries[0];

            Directory.CreateDirectory(binDir);
            foreach (var file in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceRoot, file);
                var destinationPath = Path.Combine(binDir, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                File.Copy(file, destinationPath, overwrite: true);
            }
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
