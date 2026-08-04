using System.Net.Http.Json;
using System.Text.Json;

namespace TiezhuToolbox.Modules.GearExport;

/// <summary>调用第三方解包服务，将抓包 hex 转为原始装备 JSON。</summary>
public sealed class GearUnpackClient
{
    public const string DefaultEndpoint =
        "https://krivpfvxi0.execute-api.us-west-2.amazonaws.com/dev/getItems";

    private readonly HttpClient _http;
    private readonly string _endpoint;

    public GearUnpackClient(HttpClient? httpClient = null, string? endpoint = null)
    {
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _endpoint = string.IsNullOrWhiteSpace(endpoint) ? DefaultEndpoint : endpoint.Trim();
    }

    public async Task<JsonDocument> UnpackAsync(
        IReadOnlyList<string> hexChunks,
        CancellationToken cancellationToken = default)
    {
        if (hexChunks.Count == 0)
            throw new InvalidOperationException("扫描未捕获到任何数据包，请确认已安装 Npcap、按步骤进游戏大厅后再停止。");

        using var response = await _http.PostAsJsonAsync(
            _endpoint,
            new { data = hexChunks },
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"第三方解包服务返回 {(int)response.StatusCode}。可能已变更或不可用。响应：{Truncate(body, 300)}");
        }

        try
        {
            return JsonDocument.Parse(body);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "第三方解包服务返回了无法解析的内容：" + Truncate(body, 300), ex);
        }
    }

    private static string Truncate(string text, int max)
        => text.Length <= max ? text : text[..max] + "…";
}
