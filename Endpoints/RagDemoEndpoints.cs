using System.Text;
using Microsoft.Extensions.Options;
using RagDemo.Models;
using RagDemo.Services;
using Scalar.AspNetCore;

namespace RagDemo.Endpoints;

public static class RagDemoEndpoints
{
    public static void MapRagDemoEndpoints(this WebApplication app)
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options => options.WithTitle("RAG Demo API"));

        app.MapGet("/", () => Results.Ok(new
        {
            name = "RAG Demo API",
            framework = ".NET 10 Minimal API",
            endpoints = new[] { "GET /api/health", "GET /api/documents", "POST /api/documents", "POST /api/documents/upload", "DELETE /api/documents/{id}", "POST /api/chat" },
            documentation = new[] { "GET /openapi/v1.json", "GET /scalar" }
        }))
            .WithSummary("Informazioni sull'API")
            .WithDescription("Restituisce il nome del servizio, gli endpoint disponibili e i link alla documentazione.");

        app.MapGet("/api/health", (IOptions<OllamaOptions> options, DocumentStore store) => Results.Ok(new
        {
            status = "ok",
            provider = "ollama",
            ollamaBaseUrl = options.Value.BaseUrl,
            embeddingModel = options.Value.EmbeddingModel,
            chatModel = options.Value.ChatModel,
            indexedDocuments = store.Count
        }))
            .WithSummary("Verifica lo stato dell'API")
            .WithDescription("Restituisce lo stato del servizio Ollama, i modelli configurati e il numero di documenti indicizzati.");

        app.MapGet("/api/documents", (DocumentStore store) => Results.Ok(store.List()))
            .WithSummary("Elenca i documenti")
            .WithDescription("Restituisce i documenti attualmente presenti nell'indice in memoria.");

        app.MapPost("/api/documents", AddDocument)
            .WithSummary("Aggiunge un documento")
            .WithDescription("Suddivide il testo in segmenti, calcola gli embeddings tramite Ollama e aggiunge il documento all'indice.");

        app.MapPost("/api/documents/upload", UploadDocument)
            .DisableAntiforgery()
            .WithSummary("Carica un documento")
            .WithDescription("Carica e indicizza un file UTF-8 .txt o .md tramite multipart/form-data.");

        app.MapDelete("/api/documents/{id:guid}", (Guid id, DocumentStore store, ILogger<Program> logger) =>
        {
            if (!store.Remove(id))
                return Results.NotFound(new { error = "Document not found." });

            logger.LogInformation("Documento {DocumentId} rimosso dall'indice in memoria. Documenti rimasti: {DocumentCount}.", id, store.Count);
            return Results.NoContent();
        })
            .WithSummary("Rimuove un documento")
            .WithDescription("Rimuove dall'indice in memoria il documento identificato dal suo ID.");

        app.MapPost("/api/chat", Chat)
            .WithSummary("Invia una domanda")
            .WithDescription("Cerca nei documenti indicizzati e genera una risposta basata sui contenuti rilevanti tramite Ollama.");
    }

    private static async Task<IResult> AddDocument(
        AddDocumentRequest request,
        OllamaClient ollama,
        DocumentStore store,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200)
            return Results.BadRequest(new { error = "Title is required and must be at most 200 characters." });
        if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Length > 100_000)
            return Results.BadRequest(new { error = "Content is required and must be at most 100,000 characters." });

        var chunks = TextChunker.Split(request.Content);
        logger.LogInformation(
            "Indicizzazione documento '{DocumentTitle}': {CharacterCount} caratteri suddivisi in {ChunkCount} segmenti. Calcolo embeddings con Ollama...",
            request.Title.Trim(),
            request.Content.Length,
            chunks.Count);
        try
        {
            var embeddings = await ollama.EmbedAsync(chunks, cancellationToken);
            if (embeddings.Length != chunks.Count)
                return Results.Problem("Ollama returned an unexpected number of embeddings.", statusCode: 502);

            var document = store.Add(request.Title.Trim(), chunks, embeddings);
            logger.LogInformation(
                "Documento '{DocumentTitle}' indicizzato: {ChunkCount} segmenti aggiunti all'indice in memoria (totale: {DocumentCount} documenti). Nessun dato salvato su database.",
                document.Title,
                document.ChunkCount,
                store.Count);
            return Results.Created($"/api/documents/{document.Id}", document);
        }
        catch (OllamaServiceException exception)
        {
            logger.LogWarning(exception, "Could not index document with Ollama.");
            return Results.Problem(exception.Message, statusCode: 503, title: "Ollama is unavailable");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not connect to Ollama while indexing a document.");
            return Results.Problem("Could not connect to Ollama. Check that the service is running and the configured model is available.", statusCode: 503);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Ollama timed out while indexing a document.");
            return Results.Problem("Ollama did not respond before the request timed out.", statusCode: 503);
        }
    }

    private static async Task<IResult> UploadDocument(
        IFormFile file,
        OllamaClient ollama,
        DocumentStore store,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
            return Results.BadRequest(new { error = "The uploaded file is empty." });
        if (file.Length > 400_000)
            return Results.BadRequest(new { error = "The uploaded file must be at most 400,000 bytes." });

        var extension = Path.GetExtension(file.FileName);
        if (!string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest(new { error = "Only .txt and .md files are supported." });

        var title = Path.GetFileNameWithoutExtension(Path.GetFileName(file.FileName)).Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
            return Results.BadRequest(new { error = "The file name must provide a title of at most 200 characters." });

        string content;
        try
        {
            using var reader = new StreamReader(file.OpenReadStream(), new UTF8Encoding(false, true));
            content = await reader.ReadToEndAsync(cancellationToken);
        }
        catch (DecoderFallbackException)
        {
            return Results.BadRequest(new { error = "The uploaded file must contain valid UTF-8 text." });
        }

        if (string.IsNullOrWhiteSpace(content) || content.Length > 100_000)
            return Results.BadRequest(new { error = "File content is required and must be at most 100,000 characters." });

        var chunks = TextChunker.Split(content);
        logger.LogInformation(
            "Indicizzazione file '{DocumentTitle}': {CharacterCount} caratteri suddivisi in {ChunkCount} segmenti. Calcolo embeddings con Ollama...",
            title,
            content.Length,
            chunks.Count);
        try
        {
            var embeddings = await ollama.EmbedAsync(chunks, cancellationToken);
            if (embeddings.Length != chunks.Count)
                return Results.Problem("Ollama returned an unexpected number of embeddings.", statusCode: 502);

            var document = store.Add(title, chunks, embeddings);
            logger.LogInformation(
                "File '{DocumentTitle}' indicizzato: {ChunkCount} segmenti aggiunti all'indice in memoria (totale: {DocumentCount} documenti).",
                document.Title,
                document.ChunkCount,
                store.Count);
            return Results.Created($"/api/documents/{document.Id}", document);
        }
        catch (OllamaServiceException exception)
        {
            logger.LogWarning(exception, "Could not index uploaded file with Ollama.");
            return Results.Problem(exception.Message, statusCode: 503, title: "Ollama is unavailable");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not connect to Ollama while indexing an uploaded file.");
            return Results.Problem("Could not connect to Ollama. Check that the service is running and the configured model is available.", statusCode: 503);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Ollama timed out while indexing an uploaded file.");
            return Results.Problem("Ollama did not respond before the request timed out.", statusCode: 503);
        }
    }

    private static async Task<IResult> Chat(
        ChatRequest request,
        OllamaClient ollama,
        DocumentStore store,
        IOptions<OllamaOptions> options,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question) || request.Question.Length > 4_000)
            return Results.BadRequest(new { error = "Question is required and must be at most 4,000 characters." });
        if (store.Count == 0)
        {
            logger.LogWarning("Domanda non elaborata: l'indice in memoria è vuoto. Indicizzare prima un documento.");
            return Results.Conflict(new { error = "No documents have been indexed. Add a document before asking a question." });
        }

        logger.LogInformation("Nuova domanda RAG ricevuta. Documenti nell'indice in memoria: {DocumentCount}.", store.Count);
        try
        {
            var queryEmbedding = await ollama.EmbedAsync([request.Question], cancellationToken);
            if (queryEmbedding.Length != 1)
                return Results.Problem("Ollama returned an unexpected number of query embeddings.", statusCode: 502);

            var matches = store.Search(queryEmbedding[0], options.Value.TopK);
            logger.LogInformation(
                "Ricerca completata: {MatchCount} segmenti recuperati. Fonti: {Sources}",
                matches.Count,
                string.Join("; ", matches.Select((match, index) => $"[{index + 1}] {match.Title} (similarità: {match.Score:F3})")));
            var context = string.Join(
                "\n\n",
                matches.Select((match, index) => $"[Source {index + 1}: {match.Title}]\n{match.Content}"));
            var answer = await ollama.ChatAsync(request.Question, context, cancellationToken);
            logger.LogInformation("Risposta generata con Ollama ({AnswerLength} caratteri).", answer.Length);

            return Results.Ok(new ChatResponse(
                answer,
                matches.Select((match, index) => new SourceResponse(index + 1, match.DocumentId, match.Title, match.Content, match.Score)).ToArray()));
        }
        catch (OllamaServiceException exception)
        {
            logger.LogWarning(exception, "Could not answer chat request with Ollama.");
            return Results.Problem(exception.Message, statusCode: 503, title: "Ollama is unavailable");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not connect to Ollama while answering a chat request.");
            return Results.Problem("Could not connect to Ollama. Check that the service is running and the configured models are available.", statusCode: 503);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Ollama timed out while answering a chat request.");
            return Results.Problem("Ollama did not respond before the request timed out.", statusCode: 503);
        }
    }
}
