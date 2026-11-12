using Microsoft.Extensions.Options;
using RagDemo.Services;

namespace RagDemo;

public static class RagDemoServiceCollectionExtensions
{
    public static IServiceCollection AddRagDemo(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi();
        services.Configure<OllamaOptions>(configuration.GetSection("Ollama"));
        services.AddHttpClient<OllamaClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<OllamaOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromMinutes(2);
        });
        services.AddSingleton<DocumentStore>();

        return services;
    }

    public static void LogRagDemoStartup(this WebApplication app, IConfiguration configuration)
    {
        app.Logger.LogInformation(
            "RAG demo avviata. Documenti e embeddings sono conservati solo in memoria; nessun database configurato.");
        app.Logger.LogInformation(
            "Ollama: {OllamaBaseUrl} | Modello embeddings: {EmbeddingModel} | Modello chat: {ChatModel}",
            configuration["Ollama:BaseUrl"] ?? "http://localhost:11434",
            configuration["Ollama:EmbeddingModel"] ?? "nomic-embed-text",
            configuration["Ollama:ChatModel"] ?? "llama3.2");
    }
}
