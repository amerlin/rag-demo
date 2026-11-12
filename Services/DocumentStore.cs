using System.Collections.Concurrent;
using RagDemo.Models;

namespace RagDemo.Services;

public sealed class DocumentStore
{
    private readonly ConcurrentDictionary<Guid, DocumentEntry> _documents = new();

    public int Count => _documents.Count;

    public DocumentResponse Add(string title, IReadOnlyList<string> chunks, IReadOnlyList<float[]> embeddings)
    {
        var id = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        var storedChunks = chunks.Select((content, index) =>
            new StoredChunk(id, title, content, embeddings[index])).ToArray();
        _documents[id] = new DocumentEntry(new DocumentResponse(id, title, chunks.Count, createdAt), storedChunks);
        return _documents[id].Response;
    }

    public IReadOnlyList<DocumentResponse> List() =>
        _documents.Values.Select(document => document.Response).OrderByDescending(document => document.CreatedAt).ToArray();

    public bool Remove(Guid id) => _documents.TryRemove(id, out _);

    public IReadOnlyList<SearchMatch> Search(float[] query, int topK) =>
        _documents.Values
            .SelectMany(document => document.Chunks)
            .Select(chunk => new SearchMatch(
                chunk.DocumentId,
                chunk.Title,
                chunk.Content,
                chunk.Embedding,
                CosineSimilarity(query, chunk.Embedding)))
            .OrderByDescending(match => match.Score)
            .Take(Math.Clamp(topK, 1, 20))
            .ToArray();

    private static double CosineSimilarity(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        if (left.Count == 0 || left.Count != right.Count)
            return 0;

        double dot = 0;
        double leftMagnitude = 0;
        double rightMagnitude = 0;
        for (var index = 0; index < left.Count; index++)
        {
            dot += left[index] * right[index];
            leftMagnitude += left[index] * left[index];
            rightMagnitude += right[index] * right[index];
        }

        return leftMagnitude == 0 || rightMagnitude == 0
            ? 0
            : dot / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
    }

    private sealed record DocumentEntry(DocumentResponse Response, StoredChunk[] Chunks);
}
