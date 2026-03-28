namespace SyllabusAI.Services;

public interface IOpenAiSyllabusClient
{
    bool IsConfigured { get; }

    /// <summary>Tek metin için embedding; yapılandırma yoksa null.</summary>
    Task<float[]?> EmbedOneAsync(string text, CancellationToken ct = default);

    /// <summary>Aynı sırada çoklu embedding (indeksleme).</summary>
    Task<IReadOnlyList<float[]?>?> EmbedManyAsync(IReadOnlyList<string> texts, CancellationToken ct = default);

    /// <summary>Sistem + kullanıcı mesajı ile kısa cevap; hata veya yapılandırma yoksa null.</summary>
    Task<string?> ChatAsync(string systemPrompt, string userMessage, CancellationToken ct = default);
}
