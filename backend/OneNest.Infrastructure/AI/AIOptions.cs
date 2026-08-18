namespace OneNest.Infrastructure.AI;

public class AIOptions
{
    public string Provider { get; set; } = "Gemini";
    public string Model { get; set; } = "gemini-2.5-flash";
    public string ApiKey { get; set; } = string.Empty;

    // EmbeddingModel removed — embeddings are now produced locally by
    // LocalEmbeddingProvider (all-MiniLM-L6-v2 ONNX, 384 dims, zero cost).
}
