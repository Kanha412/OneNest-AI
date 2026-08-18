namespace OneNest.Application.Interfaces.Services;

/// <summary>
/// Result of a per-user backfill run.
/// </summary>
/// <param name="NotesIndexed">Number of notes for which embedding was attempted and succeeded.</param>
/// <param name="DocumentsIndexed">Number of documents for which embedding was attempted and succeeded.</param>
/// <param name="Skipped">Items with no indexable text (e.g. image-only documents).</param>
/// <param name="Errors">Items that failed unexpectedly during indexing.</param>
public record BackfillResult(
    int NotesIndexed,
    int DocumentsIndexed,
    int Skipped,
    int Errors);

/// <summary>
/// Re-indexes all workspace items owned by a user.
///
/// Designed to be run once after Phase 8 is deployed so that notes and
/// documents created before semantic search existed become discoverable.
/// Safe to run multiple times — indexing is idempotent (delete + re-insert).
/// </summary>
public interface IBackfillService
{
    /// <summary>
    /// Indexes all notes and documents belonging to <paramref name="userId"/>.
    /// Individual item failures are swallowed and counted in
    /// <see cref="BackfillResult.Errors"/>; the run always completes.
    /// </summary>
    Task<BackfillResult> BackfillUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
