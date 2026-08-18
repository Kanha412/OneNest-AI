using Microsoft.Extensions.Logging;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Application.Interfaces.Services;
using OneNest.Domain.Enums;

namespace OneNest.Application.Services;

/// <summary>
/// Backfills the semantic index for all workspace items owned by a user.
///
/// <b>Notes</b>: indexed using <c>Title + " " + Content</c>.
///
/// <b>Documents</b>: indexed using <c>Title + " " + ExtractedText</c>.
/// Documents whose <c>ExtractedText</c> is null or empty (e.g. image-only
/// uploads or items uploaded before text extraction was added) are skipped
/// and counted in <see cref="BackfillResult.Skipped"/>.  Re-upload the
/// document to trigger fresh text extraction and full indexing.
///
/// All per-item failures are swallowed so the run always completes.
/// </summary>
public sealed class BackfillService : IBackfillService
{
    private readonly INoteRepository         _noteRepository;
    private readonly IDocumentRepository     _documentRepository;
    private readonly ISemanticIndexService   _semanticIndexService;
    private readonly ILogger<BackfillService> _logger;

    public BackfillService(
        INoteRepository         noteRepository,
        IDocumentRepository     documentRepository,
        ISemanticIndexService   semanticIndexService,
        ILogger<BackfillService> logger)
    {
        _noteRepository       = noteRepository;
        _documentRepository   = documentRepository;
        _semanticIndexService = semanticIndexService;
        _logger               = logger;
    }

    /// <inheritdoc/>
    public async Task<BackfillResult> BackfillUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        int notesIndexed = 0, docsIndexed = 0, skipped = 0, errors = 0;

        // ── Notes ─────────────────────────────────────────────────────────────

        IReadOnlyList<Domain.Entities.Note> notes;
        try
        {
            notes = await _noteRepository.GetAllAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BackfillService: failed to fetch notes for user {UserId}.", userId);
            notes = [];
        }

        foreach (var note in notes)
        {
            if (cancellationToken.IsCancellationRequested) break;
            try
            {
                var text = BuildNoteText(note);
                if (string.IsNullOrWhiteSpace(text)) { skipped++; continue; }

                await _semanticIndexService.IndexAsync(
                    userId, EmbeddingSourceType.Note, note.Id, note.Title, text, cancellationToken);

                notesIndexed++;
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogWarning(ex, "BackfillService: failed to index note {NoteId}.", note.Id);
            }
        }

        // ── Documents ─────────────────────────────────────────────────────────

        IReadOnlyList<Domain.Entities.Document> documents;
        try
        {
            documents = await _documentRepository.GetAllAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BackfillService: failed to fetch documents for user {UserId}.", userId);
            documents = [];
        }

        foreach (var doc in documents)
        {
            if (cancellationToken.IsCancellationRequested) break;
            try
            {
                if (string.IsNullOrWhiteSpace(doc.ExtractedText))
                {
                    // Image-only or not yet extracted — skip without error
                    skipped++;
                    continue;
                }

                var text = BuildDocumentText(doc);
                await _semanticIndexService.IndexAsync(
                    userId, EmbeddingSourceType.Document, doc.Id, doc.Title, text, cancellationToken);

                docsIndexed++;
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogWarning(ex, "BackfillService: failed to index document {DocumentId}.", doc.Id);
            }
        }

        _logger.LogInformation(
            "BackfillService: user {UserId} — notes={Notes}, docs={Docs}, skipped={Skipped}, errors={Errors}.",
            userId, notesIndexed, docsIndexed, skipped, errors);

        return new BackfillResult(notesIndexed, docsIndexed, skipped, errors);
    }

    // ── Text helpers ──────────────────────────────────────────────────────────

    private static string BuildNoteText(Domain.Entities.Note note)
    {
        var parts = new[] { note.Title, note.Content };
        return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static string BuildDocumentText(Domain.Entities.Document doc)
    {
        var parts = new[] { doc.Title, doc.ExtractedText };
        return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }
}
