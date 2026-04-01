using System.Text;

namespace SyllabusAI.Services;

/// <summary>
/// Müfredat metnini RAG için parçalara böler (paragraf + maksimum uzunluk).
/// </summary>
public static class SyllabusTextChunker
{
    public const int MaxChunkChars = 1200;
    public const int MinChunkChars = 80;

    public static IReadOnlyList<string> Split(string text)
    {
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (string.IsNullOrEmpty(text)) return Array.Empty<string>();

        var paragraphs = text.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var chunks = new List<string>();
        var current = new StringBuilder();

        void Flush()
        {
            var s = current.ToString().Trim();
            if (s.Length >= MinChunkChars)
                chunks.Add(s);
            current.Clear();
        }

        foreach (var p in paragraphs)
        {
            if (current.Length > 0 && current.Length + p.Length + 2 > MaxChunkChars)
                Flush();

            if (p.Length > MaxChunkChars)
            {
                Flush();
                for (var i = 0; i < p.Length; i += MaxChunkChars)
                {
                    var len = Math.Min(MaxChunkChars, p.Length - i);
                    var slice = p.AsSpan(i, len).Trim().ToString();
                    if (slice.Length >= MinChunkChars)
                        chunks.Add(slice);
                }
                continue;
            }

            if (current.Length > 0) current.Append("\n\n");
            current.Append(p);
        }

        Flush();

        if (chunks.Count == 0 && text.Length >= MinChunkChars)
            chunks.Add(text);
        else if (chunks.Count == 0 && text.Length > 0)
            chunks.Add(text);

        return chunks;
    }
}
