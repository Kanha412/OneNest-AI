namespace OneNest.Application.DTOs.Rag;

/// <summary>
/// Response from the RAG endpoint.
///
/// When no relevant content is found above the similarity threshold, <see cref="HasSources"/>
/// is <c>false</c>, <see cref="Sources"/> is empty, and <see cref="Answer"/> contains
/// a polite "no relevant content found" message — never an empty string.
/// </summary>
public class RagResponse
{
    /// <summary>The AI-generated answer grounded in the retrieved content.</summary>
    public string Answer { get; set; } = string.Empty;

    /// <summary>
    /// Source items that were retrieved and used to ground the answer.
    /// Empty when no relevant content was found above the threshold.
    /// </summary>
    public List<RagSourceDto> Sources { get; set; } = [];

    /// <summary>
    /// <c>true</c> when at least one source item was retrieved above the
    /// similarity threshold; <c>false</c> for the "no relevant content" case.
    /// </summary>
    public bool HasSources { get; set; }

    /// <summary>Gemini model name used to generate the answer.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the response was generated.</summary>
    public DateTime Timestamp { get; set; }
}
