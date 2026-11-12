namespace RagDemo.Models;

public sealed record AddDocumentRequest(string Title, string Content);
public sealed record ChatRequest(string Question);
public sealed record DocumentResponse(Guid Id, string Title, int ChunkCount, DateTimeOffset CreatedAt);
public sealed record ChatResponse(string Answer, IReadOnlyList<SourceResponse> Sources);
public sealed record SourceResponse(int Number, Guid DocumentId, string Title, string Content, double Relevance);
public sealed record SearchMatch(Guid DocumentId, string Title, string Content, float[] Embedding, double Score);
internal sealed record StoredChunk(Guid DocumentId, string Title, string Content, float[] Embedding);
