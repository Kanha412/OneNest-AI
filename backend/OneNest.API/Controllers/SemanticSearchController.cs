using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneNest.Application.DTOs.SemanticSearch;
using OneNest.Application.Interfaces.Security;
using OneNest.Application.Interfaces.Services;

namespace OneNest.API.Controllers;

/// <summary>
/// Phase 8 — Semantic Search endpoints.
///
/// POST /api/semantic-search          — run a similarity search
/// POST /api/semantic-search/backfill — re-index all of the caller's workspace items
/// </summary>
[ApiController]
[Authorize]
[Route("api/semantic-search")]
public class SemanticSearchController : ControllerBase
{
    private readonly ISemanticSearchService _semanticSearchService;
    private readonly IBackfillService       _backfillService;
    private readonly ICurrentUserService    _currentUserService;

    public SemanticSearchController(
        ISemanticSearchService semanticSearchService,
        IBackfillService       backfillService,
        ICurrentUserService    currentUserService)
    {
        _semanticSearchService = semanticSearchService;
        _backfillService       = backfillService;
        _currentUserService    = currentUserService;
    }

    /// <summary>
    /// Run a semantic similarity search against the authenticated user's
    /// indexed notes and documents.
    /// </summary>
    /// <remarks>
    /// Example request body:
    /// <code>{ "query": "employment history", "topK": 5 }</code>
    /// </remarks>
    [HttpPost]
    public async Task<ActionResult<List<SemanticSearchResult>>> Search(
        [FromBody] SemanticSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest("Query must not be empty.");

        var userId  = _currentUserService.UserId;
        var results = await _semanticSearchService.SearchAsync(userId, request, cancellationToken);

        return Ok(results);
    }

    /// <summary>
    /// Re-indexes all notes and documents owned by the authenticated user.
    ///
    /// Use this endpoint once after Phase 8 is deployed so that items created
    /// before semantic search existed become discoverable.  Safe to call
    /// multiple times — indexing is idempotent.
    /// </summary>
    [HttpPost("backfill")]
    public async Task<ActionResult<BackfillResult>> Backfill(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        var result = await _backfillService.BackfillUserAsync(userId, cancellationToken);
        return Ok(result);
    }
}
