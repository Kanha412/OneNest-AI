using OneNest.Application.DTOs.Rag;

namespace OneNest.Application.Interfaces.Services;

/// <summary>
/// Retrieval-Augmented Generation (RAG) service for OneNest.
///
/// Orchestrates:
///   1. Semantic retrieval of the most relevant personal content chunks via pgvector
///   2. Context construction with prompt-injection protection
///   3. Grounded answer generation via Gemini
///
/// This service is explicitly opt-in; calling it is separate from the normal
/// AI conversation flow.  Standard <see cref="IAIConversationService"/> and
/// <see cref="ISemanticSearchService"/> are unaffected.
/// </summary>
public interface IRagService
{
    /// <summary>
    /// Answers <paramref name="request"/>.<c>Query</c> by retrieving the user's
    /// most relevant notes and documents and grounding a Gemini response in them.
    /// </summary>
    /// <param name="userId">
    /// The authenticated user's ID resolved from the JWT.
    /// Never supplied by the client — always passed by the controller.
    /// </param>
    /// <param name="request">The RAG query and optional overrides.</param>
    /// <param name="cancellationToken">Propagated to all async I/O calls.</param>
    /// <returns>
    /// A <see cref="RagResponse"/> that is never null.
    /// When no relevant content is found, <see cref="RagResponse.HasSources"/>
    /// is <c>false</c> and the answer is a polite fallback message.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="request"/>.<c>Query</c> is empty or whitespace.
    /// </exception>
    Task<RagResponse> AskAsync(
        Guid userId,
        RagRequest request,
        CancellationToken cancellationToken = default);
}
