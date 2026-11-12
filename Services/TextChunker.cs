namespace RagDemo.Services;

public static class TextChunker
{
    private const int ChunkSize = 1_200;
    private const int Overlap = 200;

    public static IReadOnlyList<string> Split(string text)
    {
        var normalized = text.Trim();
        var chunks = new List<string>();
        var start = 0;

        while (start < normalized.Length)
        {
            var end = Math.Min(start + ChunkSize, normalized.Length);
            if (end < normalized.Length)
            {
                var paragraphBreak = normalized.LastIndexOf("\n\n", end - 1, end - start, StringComparison.Ordinal);
                var sentenceBreak = normalized.LastIndexOfAny(['.', '!', '?', '\n'], end - 1, end - start);
                var breakAt = paragraphBreak >= start + ChunkSize / 2 ? paragraphBreak + 2
                    : sentenceBreak >= start + ChunkSize / 2 ? sentenceBreak + 1
                    : end;
                end = breakAt;
            }

            var chunk = normalized[start..end].Trim();
            if (chunk.Length > 0)
                chunks.Add(chunk);
            if (end >= normalized.Length)
                break;

            start = Math.Max(end - Overlap, start + 1);
        }

        return chunks;
    }
}
