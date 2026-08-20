using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using OneNest.Application.DTOs.SemanticSearch;
using OneNest.Application.Interfaces.AI;
using OneNest.Application.Interfaces.Repositories;
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
    private readonly ISemanticSearchService          _semanticSearchService;
    private readonly ICurrentUserService             _currentUserService;
    private readonly IServiceScopeFactory            _scopeFactory;
    private readonly IEmbeddingProvider              _embeddingProvider;
    private readonly IEmbeddingRepository            _embeddingRepository;
    private readonly ILogger<SemanticSearchController> _logger;

    public SemanticSearchController(
        ISemanticSearchService          semanticSearchService,
        ICurrentUserService             currentUserService,
        IServiceScopeFactory            scopeFactory,
        IEmbeddingProvider              embeddingProvider,
        IEmbeddingRepository            embeddingRepository,
        ILogger<SemanticSearchController> logger)
    {
        _semanticSearchService = semanticSearchService;
        _currentUserService    = currentUserService;
        _scopeFactory          = scopeFactory;
        _embeddingProvider     = embeddingProvider;
        _embeddingRepository   = embeddingRepository;
        _logger                = logger;
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
    /// Re-indexes all notes and documents owned by the authenticated user
    /// in a background thread and returns 202 Accepted immediately.
    ///
    /// WHY background: ONNX CPU inference over many chunks can take minutes.
    /// Awaiting it in the HTTP handler would leave Swagger hanging forever.
    /// The result is logged to the server console when the run completes.
    ///
    /// Safe to call multiple times — indexing is idempotent.
    /// </summary>
    [HttpPost("backfill")]
    public IActionResult Backfill()
    {
        var capturedUserId = _currentUserService.UserId;

        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope      = _scopeFactory.CreateAsyncScope();
                var             backfillSvc = scope.ServiceProvider
                                                   .GetRequiredService<IBackfillService>();

                // CancellationToken.None — backfill must not be aborted when the
                // HTTP response is sent; it should always run to completion.
                await backfillSvc.BackfillUserAsync(capturedUserId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                // An unexpected exception escaped BackfillService (e.g. scope resolution
                // failure or unhandled DI error).  Individual item failures are already
                // logged inside BackfillService.
                _logger.LogError(ex,
                    "SemanticSearchController: backfill background task threw unexpectedly for user {UserId}. " +
                    "This is likely a DI or infrastructure failure — check the exception details.",
                    capturedUserId);
            }
        });

        return Accepted(new
        {
            message = "Backfill started in the background. Check server logs for progress and completion."
        });
    }

    /// <summary>
    /// Diagnostic endpoint — call this to check:
    /// <list type="bullet">
    ///   <item>Whether the local ONNX embedding provider is alive.</item>
    ///   <item>How many embedding rows exist for the authenticated user
    ///         (use this to know when backfill has finished).</item>
    /// </list>
    ///
    /// Expected healthy response:
    /// <code>
    /// { "providerReady": true, "embeddingDimension": 384,
    ///   "embeddingRecordCount": 6, "hint": null }
    /// </code>
    /// If <c>providerReady</c> is false the model failed to initialise —
    /// restart the backend; it will re-initialise on the next request.
    /// If <c>embeddingRecordCount</c> is 0 the backfill has not finished yet —
    /// wait ~1–2 minutes and call this endpoint again.
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;

        // Test the embedding provider with a trivial query
        var testVector = await _embeddingProvider.EmbedAsync("health check", cancellationToken);
        bool providerReady = testVector is { Length: > 0 };

        // Count how many rows this user currently has in the index
        int count = 0;
        try { count = await _embeddingRepository.CountByUserAsync(userId, cancellationToken); }
        catch { /* DB error: leave count at 0 */ }

        string? hint = null;
        if (!providerReady)
            hint = "Embedding provider is unavailable. Restart the backend and try again.";
        else if (count == 0)
            hint = "No embedding records found. Call POST /api/semantic-search/backfill and wait ~1-2 minutes.";

        return Ok(new
        {
            providerReady,
            embeddingDimension  = testVector?.Length ?? 0,
            embeddingRecordCount = count,
            hint
        });
    }
}
