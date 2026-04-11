using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SyllabusAI.Services;

/// <summary>
/// OpenAI uyumlu API (varsayılan https://api.openai.com/v1). Anahtar boşsa tüm çağrılar no-op.
/// Anahtar sırası: <c>OpenAI:ApiKey</c> (appsettings / user-secrets / ortamda <c>OpenAI__ApiKey</c>),
/// ardından ortam değişkeni <c>OPENAI_API_KEY</c> (SDK ve OpenAI arayüzünde yaygın).
/// </summary>
public class OpenAiSyllabusClient : IOpenAiSyllabusClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<OpenAiSyllabusClient> _logger;

    public OpenAiSyllabusClient(IHttpClientFactory httpFactory, IConfiguration config, ILogger<OpenAiSyllabusClient> logger)
    {
        _httpFactory = httpFactory;
        _config = config;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ResolveApiKey());

    public async Task<float[]?> EmbedOneAsync(string text, CancellationToken ct = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(text)) return null;
        var list = await EmbedManyAsync(new[] { text }, ct);
        return list is { Count: > 0 } ? list[0] : null;
    }

    public async Task<IReadOnlyList<float[]?>?> EmbedManyAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        if (!IsConfigured || texts.Count == 0) return null;

        var client = CreateClient();
        var model = _config["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";
        const int batch = 16;
        var all = new List<float[]?>(texts.Count);

        for (var offset = 0; offset < texts.Count; offset += batch)
        {
            var slice = texts.Skip(offset).Take(batch).ToArray();
            var payload = new { model, input = slice };
            using var response = await client.PostAsJsonAsync("embeddings", payload, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenAI embeddings HTTP {Status}", response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("data", out var dataEl))
                return null;

            var batchEmb = new float[slice.Length][];
            foreach (var item in dataEl.EnumerateArray())
            {
                if (!item.TryGetProperty("index", out var idxEl) || !item.TryGetProperty("embedding", out var embEl))
                    continue;
                var idx = idxEl.GetInt32();
                if (idx < 0 || idx >= batchEmb.Length) continue;
                batchEmb[idx] = embEl.EnumerateArray().Select(x => (float)x.GetDouble()).ToArray();
            }

            foreach (var e in batchEmb)
                all.Add(e);
        }

        return all;
    }

    public async Task<string?> ChatAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
    {
        if (!IsConfigured) return null;

        var client = CreateClient();
        var model = _config["OpenAI:ChatModel"] ?? "gpt-4o-mini";
        var payload = new
        {
            model,
            temperature = 0.2,
            max_tokens = 600,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userMessage }
            }
        };

        using var response = await client.PostAsJsonAsync("chat/completions", payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("OpenAI chat HTTP {Status}", response.StatusCode);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            return null;
        var first = choices[0];
        if (!first.TryGetProperty("message", out var msg)) return null;
        if (!msg.TryGetProperty("content", out var content)) return null;
        return content.GetString()?.Trim();
    }

    private HttpClient CreateClient()
    {
        var client = _httpFactory.CreateClient(nameof(OpenAiSyllabusClient));
        var key = ResolveApiKey()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return client;
    }

    /// <summary>
    /// Yapılandırma yalnızca <c>OpenAI:ApiKey</c> okuduğunda, anahtar yalnızca <c>OPENAI_API_KEY</c>
    /// ile tanımlı kaldığı için uygulamanın "yapılandırılmadı" demesi yaygın bir hata; bu yüzden ortam değişkeni yedeklenir.
    /// </summary>
    private string? ResolveApiKey()
    {
        var k = _config["OpenAI:ApiKey"];
        if (!string.IsNullOrWhiteSpace(k))
            return k.Trim();
        k = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(k))
            return k.Trim();
        return null;
    }
}
