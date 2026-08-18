using OneNest.Application.DTOs.SemanticSearch;

namespace OneNest.Application.Interfaces.Services;

/// <summary>Runs a semantic similarity search over a user's indexed workspace items.</summary>
public interface ISemanticSearchService
{
    /// <summary>
    /// Embeds <paramref name="request"/>.<c>Query</c> and returns the
    /// <c>TopK</c> most relevant workspace items as ranked results.
    /// Returns an empty list when embedding is unavailable.
    /// </summary>
    Task<List<SemanticSearchResult>> SearchAsync(
        Guid userId,
        SemanticSearchRequest request,
        CancellationToken cancellationToken = default);
}
