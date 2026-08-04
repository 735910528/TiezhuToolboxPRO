using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TiezhuToolbox.Modules.GearExport;

/// <summary>
/// 调用百里机器人（e7bot.top）公开页面同款接口：上传 gear.txt 并获取战力分析图。
/// </summary>
public sealed class BailiGearStatClient
{
    public const string DefaultBaseUrl = "https://e7bot.top/gs/";
    public const string SiteUrl = "https://e7bot.top/gs/";

    private readonly HttpClient _http;
    private readonly Uri _baseUri;

    public BailiGearStatClient(HttpClient? httpClient = null, string? baseUrl = null)
    {
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        var root = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.Trim();
        if (!root.EndsWith('/'))
            root += "/";
        _baseUri = new Uri(root, UriKind.Absolute);
    }

    public async Task<BailiGearStatResult> AnalyzeAsync(
        GearTxtDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Items.Count == 0)
            throw new InvalidOperationException("没有可分析的装备数据，请先完成解包。");

        var json = GearTxtExporter.Serialize(document);
        var bytes = Encoding.UTF8.GetBytes(json);
        return await AnalyzeBytesAsync(bytes, "gear.txt", cancellationToken);
    }

    public async Task<BailiGearStatResult> AnalyzeFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new FileNotFoundException("找不到 gear.txt 文件。", path);

        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        var name = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(name))
            name = "gear.txt";
        return await AnalyzeBytesAsync(bytes, name, cancellationToken);
    }

    private async Task<BailiGearStatResult> AnalyzeBytesAsync(
        byte[] fileBytes,
        string fileName,
        CancellationToken cancellationToken)
    {
        var fileId = await UploadAsync(fileBytes, fileName, cancellationToken);
        var imageBytes = await FetchStatImageAsync(fileId, cancellationToken);
        return new BailiGearStatResult
        {
            FileId = fileId,
            ImageBytes = imageBytes,
        };
    }

    private async Task<string> UploadAsync(
        byte[] fileBytes,
        string fileName,
        CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "file", fileName);
        form.Add(new StringContent("gs-"), "perfix");

        using var response = await _http.PostAsync(
            new Uri(_baseUri, "api/uploadFile"),
            form,
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"百里上传接口返回 {(int)response.StatusCode}。可能已变更或不可用。响应：{Truncate(body, 300)}");
        }

        BailiUploadResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<BailiUploadResponse>(body);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "百里上传接口返回了无法解析的内容：" + Truncate(body, 300), ex);
        }

        if (parsed == null || string.IsNullOrWhiteSpace(parsed.FileId))
        {
            throw new InvalidOperationException(
                "百里上传接口未返回 fileId：" + Truncate(body, 300));
        }

        return parsed.FileId.Trim();
    }

    private async Task<byte[]> FetchStatImageAsync(
        string fileId,
        CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync(
            new Uri(_baseUri, "api/gearStat"),
            new { fileId },
            cancellationToken);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var text = Encoding.UTF8.GetString(bytes);
            throw new InvalidOperationException(
                $"百里战力分析接口返回 {(int)response.StatusCode}。响应：{Truncate(text, 300)}");
        }

        if (bytes.Length < 8)
            throw new InvalidOperationException("百里战力分析接口返回空图片。");

        return bytes;
    }

    private static string Truncate(string text, int max)
        => text.Length <= max ? text : text[..max] + "…";

    private sealed class BailiUploadResponse
    {
        [JsonPropertyName("fileId")]
        public string? FileId { get; set; }

        [JsonPropertyName("code")]
        public int Code { get; set; }
    }
}

public sealed class BailiGearStatResult
{
    public required string FileId { get; init; }
    public required byte[] ImageBytes { get; init; }
}
