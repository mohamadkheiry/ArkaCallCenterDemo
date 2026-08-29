using System.Net;
using System.Text;
using System.Text.Json;
using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Core.Constants;
using ArkaCallCenter.Infrastructure.Audio;
using ArkaCallCenter.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ArkaCallCenter.Tests;

public sealed class GapAiServiceTests
{
    [Fact]
    public async Task Primary_speech_route_uses_requested_gemini_model_voice_and_wav()
    {
        var handler = new QueueHttpHandler(request =>
        {
            Assert.EndsWith("/audio/speech", request.Uri.AbsoluteUri, StringComparison.Ordinal);
            using var body = JsonDocument.Parse(request.Body);
            Assert.Equal("gemini-2.5-pro-preview-tts", body.RootElement.GetProperty("model").GetString());
            Assert.Equal("Kore", body.RootElement.GetProperty("voice").GetString());
            Assert.Equal("wav", body.RootElement.GetProperty("response_format").GetString());
            return Bytes(HttpStatusCode.OK, CreateWav(24_000), "audio/wav");
        });
        var service = CreateService(handler);

        var result = await service.GenerateSpeechWav8kAsync("سلام");

        AssertWav8k(result);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Native_inline_pcm_is_used_when_primary_speech_route_fails()
    {
        var pcm = new byte[2_400];
        for (var index = 0; index < pcm.Length; index += 2)
        {
            var sample = (short)(Math.Sin(index / 30d) * short.MaxValue / 4);
            BitConverter.TryWriteBytes(pcm.AsSpan(index, 2), sample);
        }
        var encoded = Convert.ToBase64String(pcm);
        var response = JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[] { new { inlineData = new { mimeType = "audio/L16;rate=24000", data = encoded } } },
                    },
                },
            },
        });
        var handler = new QueueHttpHandler(
            _ => Json(HttpStatusCode.BadGateway, "{\"error\":\"temporary\"}"),
            request =>
            {
                Assert.EndsWith("/models/gemini-2.5-pro-preview-tts:generateContent",
                    request.Uri.AbsoluteUri, StringComparison.Ordinal);
                return Json(HttpStatusCode.OK, response);
            });
        var service = CreateService(handler);

        var result = await service.GenerateSpeechWav8kAsync("سلام");

        AssertWav8k(result);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Cleaner_uses_configured_model_and_returns_only_cleaned_text()
    {
        var handler = new QueueHttpHandler(request =>
        {
            Assert.EndsWith("/chat/completions", request.Uri.AbsoluteUri, StringComparison.Ordinal);
            using var body = JsonDocument.Parse(request.Body);
            Assert.Equal("gemini-3.6-flash", body.RootElement.GetProperty("model").GetString());
            var prompt = body.RootElement.GetProperty("messages")[1]
                .GetProperty("content").GetString()!;
            var dataStart = prompt.LastIndexOf("data: ", StringComparison.Ordinal);
            Assert.True(dataStart >= 0);
            using var transcriptData = JsonDocument.Parse(prompt[(dataStart + 6)..]);
            Assert.Equal("کلاس ها چه ساعتی بر گزار میشه",
                transcriptData.RootElement.GetProperty("transcript").GetString());
            return Json(HttpStatusCode.OK,
                "{\"choices\":[{\"message\":{\"content\":\"کلاس‌ها چه ساعتی برگزار می‌شوند؟\"}}]}");
        });
        var service = CreateService(handler);

        var cleaned = await service.CleanTranscriptAsync("کلاس ها چه ساعتی بر گزار میشه");

        Assert.Equal("کلاس‌ها چه ساعتی برگزار می‌شوند؟", cleaned);
    }

    [Fact]
    public async Task Whisper_request_matches_the_supplied_postman_contract()
    {
        var handler = new QueueHttpHandler(request =>
        {
            Assert.Equal("http://192.168.20.189:8101/v1/audio/transcriptions", request.Uri.AbsoluteUri);
            Assert.Equal("multipart/form-data", request.ContentType);
            Assert.Contains("name=file", request.Body.Replace("\"", "", StringComparison.Ordinal),
                StringComparison.Ordinal);
            Assert.Contains("name=model", request.Body.Replace("\"", "", StringComparison.Ordinal),
                StringComparison.Ordinal);
            Assert.Contains("whisper-1", request.Body, StringComparison.Ordinal);
            Assert.Contains("name=language", request.Body.Replace("\"", "", StringComparison.Ordinal),
                StringComparison.Ordinal);
            Assert.Contains("false", request.Body, StringComparison.Ordinal);
            return Json(HttpStatusCode.OK, "{\"text\":\"ساعت کلاس چیست؟\"}");
        });
        var service = CreateService(handler);

        var transcript = await service.TranscribeAsync(CreateWav(8_000));

        Assert.Equal("ساعت کلاس چیست؟", transcript);
    }

    [Fact]
    public async Task Semantic_matcher_rejects_an_id_not_present_in_candidates()
    {
        var handler = new QueueHttpHandler(_ => Json(HttpStatusCode.OK,
            "{\"choices\":[{\"message\":{\"content\":\"{\\\"matchedId\\\":999}\"}}]}"));
        var service = CreateService(handler);

        var selected = await service.SelectMatchingQuestionAsync("ساعت کلاس چیست؟",
            new[] { new GapQuestionCandidate(12, "کلاس چه ساعتی برگزار می‌شود؟") });

        Assert.Null(selected);
    }

    private static GapAiService CreateService(HttpMessageHandler handler)
    {
        var settings = new DictionarySettings(new Dictionary<string, string?>
        {
            [SettingKeys.GapGptBaseUrl] = "https://api.gapgpt.app/v1",
            [SettingKeys.GapGptApiKey] = "test-secret",
            [SettingKeys.GapGptCleanerModel] = "gemini-3.6-flash",
            [SettingKeys.GapGptTtsModel] = "gemini-2.5-pro-preview-tts",
            [SettingKeys.GapGptTtsVoice] = "Kore",
            [SettingKeys.GapGptFallbackTtsModel] = "gpt-4o-mini-tts",
            [SettingKeys.GapGptFallbackTtsVoice] = "alloy",
            [SettingKeys.WhisperBaseUrl] = "http://192.168.20.189:8101",
            [SettingKeys.WhisperModel] = "whisper-1",
            [SettingKeys.WhisperLanguage] = "fa",
        });
        return new GapAiService(new HttpClient(handler), settings, NullLogger<GapAiService>.Instance);
    }

    private static byte[] CreateWav(int rate)
        => AudioConvert.WriteWav(Enumerable.Range(0, rate / 20)
            .Select(index => (short)(Math.Sin(index / 15d) * short.MaxValue / 5)).ToArray(), rate);

    private static void AssertWav8k(byte[] wav)
    {
        Assert.True(wav.Length > 44);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(wav, 8, 4));
        Assert.Equal(8_000, BitConverter.ToInt32(wav, 24));
        Assert.Equal(1, BitConverter.ToInt16(wav, 22));
        Assert.Equal(16, BitConverter.ToInt16(wav, 34));
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Bytes(HttpStatusCode status, byte[] body, string mediaType)
    {
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
        return new HttpResponseMessage(status) { Content = content };
    }

    private sealed class QueueHttpHandler(params Func<CapturedRequest, HttpResponseMessage>[] responders)
        : HttpMessageHandler
    {
        private readonly Queue<Func<CapturedRequest, HttpResponseMessage>> _responders = new(responders);
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.NotEmpty(_responders);
            var bytes = request.Content is null
                ? Array.Empty<byte>()
                : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            var captured = new CapturedRequest(
                request.Method,
                request.RequestUri!,
                request.Content?.Headers.ContentType?.MediaType,
                Encoding.UTF8.GetString(bytes));
            Requests.Add(captured);
            return _responders.Dequeue()(captured);
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri, string? ContentType, string Body);

    private sealed class DictionarySettings(IReadOnlyDictionary<string, string?> values) : ISettingsService
    {
        public Task<string?> GetAsync(string key, string? fallback = null, CancellationToken ct = default)
            => Task.FromResult(values.TryGetValue(key, out var value) ? value : fallback);

        public async Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default)
            => int.TryParse(await GetAsync(key, null, ct), out var value) ? value : fallback;

        public async Task<double> GetDoubleAsync(string key, double fallback, CancellationToken ct = default)
            => double.TryParse(await GetAsync(key, null, ct), out var value) ? value : fallback;

        public Task SetAsync(string key, string? value, bool isSecret = false, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyDictionary<string, string?>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult(values);
    }
}
