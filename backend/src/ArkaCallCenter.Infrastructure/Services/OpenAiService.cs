using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Core.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ArkaCallCenter.Infrastructure.Services;

public class OpenAiService : IOpenAiService
{
    private readonly HttpClient _http;
    private readonly ISettingsService _settings;
    private readonly IConfiguration _config;
    private readonly ILogger<OpenAiService> _logger;

    public OpenAiService(HttpClient http, ISettingsService settings, IConfiguration config, ILogger<OpenAiService> logger)
    {
        _http = http;
        _settings = settings;
        _config = config;
        _logger = logger;
    }

    private async Task<(string baseUrl, string apiKey)> CredsAsync(CancellationToken ct)
    {
        var baseUrl = await _settings.GetAsync(SettingKeys.OpenAiBaseUrl, null, ct)
            ?? _config["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1";
        var apiKey = await _settings.GetAsync(SettingKeys.OpenAiApiKey, null, ct)
            ?? _config["OpenAI:ApiKey"] ?? "";
        return (baseUrl.TrimEnd('/'), apiKey);
    }

    private async Task<HttpRequestMessage> BuildAsync(HttpMethod method, string path, object body, CancellationToken ct)
    {
        var (baseUrl, apiKey) = await CredsAsync(ct);
        var req = new HttpRequestMessage(method, $"{baseUrl}{path}")
        {
            Content = JsonContent.Create(body),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return req;
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        => (await EmbedBatchAsync(new[] { text }, ct))[0];

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        var model = await _settings.GetAsync(SettingKeys.OpenAiEmbeddingModel, "text-embedding-3-small", ct);
        var req = await BuildAsync(HttpMethod.Post, "/embeddings", new { model, input = texts }, ct);
        using var res = await _http.SendAsync(req, ct);
        await EnsureOkAsync(res, ct);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));

        var result = new List<float[]>(texts.Count);
        foreach (var item in doc.RootElement.GetProperty("data").EnumerateArray())
        {
            var vec = item.GetProperty("embedding").EnumerateArray().Select(x => x.GetSingle()).ToArray();
            result.Add(vec);
        }
        return result;
    }

    public async Task<string> ChatAsync(string systemPrompt, string userPrompt, bool jsonMode = false, CancellationToken ct = default)
    {
        var model = await _settings.GetAsync(SettingKeys.OpenAiChatModel, "gpt-4o-mini", ct);
        object body = jsonMode
            ? new
            {
                model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt },
                },
                response_format = new { type = "json_object" },
                temperature = 0,
            }
            : new
            {
                model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt },
                },
                temperature = 0.2,
            };

        var req = await BuildAsync(HttpMethod.Post, "/chat/completions", body, ct);
        using var res = await _http.SendAsync(req, ct);
        await EnsureOkAsync(res, ct);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }

    public async Task<byte[]> TextToSpeechAsync(string text, string voice, CancellationToken ct = default)
    {
        var model = await _settings.GetAsync(SettingKeys.OpenAiTtsModel, "gpt-4o-mini-tts", ct);
        var req = await BuildAsync(HttpMethod.Post, "/audio/speech",
            new { model, voice, input = text, response_format = "mp3" }, ct);
        using var res = await _http.SendAsync(req, ct);
        await EnsureOkAsync(res, ct);
        return await res.Content.ReadAsByteArrayAsync(ct);
    }

    private async Task EnsureOkAsync(HttpResponseMessage res, CancellationToken ct)
    {
        if (res.IsSuccessStatusCode) return;
        var body = await res.Content.ReadAsStringAsync(ct);
        _logger.LogError("OpenAI error {Status}: {Body}", res.StatusCode, body);
        throw new HttpRequestException($"OpenAI API error {(int)res.StatusCode}: {body}");
    }
}
