using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneNest.Application.DTOs.Notes;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Application.Interfaces.Security;
using OneNest.Application.Interfaces.Services;
using OneNest.Domain.Entities;
using OneNest.Domain.Enums;

namespace OneNest.Application.Services;

public class NoteService : INoteService
{
    private readonly INoteRepository _noteRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISemanticIndexService _semanticIndexService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NoteService> _logger;

    public NoteService(
        INoteRepository noteRepository,
        ICurrentUserService currentUserService,
        ISemanticIndexService semanticIndexService,
        IServiceScopeFactory scopeFactory,
        ILogger<NoteService> logger)
    {
        _noteRepository = noteRepository;
        _currentUserService = currentUserService;
        _semanticIndexService = semanticIndexService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<List<NoteResponse>> GetAllAsync()
{
    var notes = await _noteRepository.GetAllAsync(_currentUserService.UserId);

    return notes.Select(note => new NoteResponse
{
    Id = note.Id,
    Title = note.Title,
    Content = note.Content,
    CreatedAt = note.CreatedAt,
    UpdatedAt = note.UpdatedAt,
    IsPinned = note.IsPinned,
    IsArchived = note.IsArchived
}).ToList();
}

    public async Task<NoteResponse> CreateAsync(CreateNoteRequest request)
{
    var note = new Note
    {
        Id = Guid.NewGuid(),
        Title = request.Title,
        Content = request.Content,
        UserId = _currentUserService.UserId,
        CreatedAt = DateTime.UtcNow
    };

    await _noteRepository.AddAsync(note);

    // Phase 8 — index in background; same rationale as DocumentService
    EnqueueIndex(note.UserId, note.Id, note.Title, note.Content ?? string.Empty);

    return new NoteResponse
{
    Id = note.Id,
    Title = note.Title,
    Content = note.Content,
    CreatedAt = note.CreatedAt,
    UpdatedAt = note.UpdatedAt,
    IsPinned = note.IsPinned,
    IsArchived = note.IsArchived
};
}

    public async Task DeleteAsync(Guid id)
{
    var userId = _currentUserService.UserId;
    var note = await _noteRepository.GetByIdAsync(id, userId);

    if (note is null)
        return;

    await _noteRepository.DeleteAsync(note);

    // Phase 8 — remove embedding entry
    await _semanticIndexService.DeleteIndexAsync(userId, EmbeddingSourceType.Note, id);
}

public async Task<NoteResponse?> UpdateAsync(Guid id, UpdateNoteRequest request)
{
    var note = await _noteRepository.GetByIdAsync(id, _currentUserService.UserId);

    if (note is null)
        return null;

    note.Title = request.Title;
    note.Content = request.Content;
    note.UpdatedAt = DateTime.UtcNow;

    await _noteRepository.UpdateAsync(note);

    // Phase 8 — re-index with updated content in background
    EnqueueIndex(note.UserId, note.Id, note.Title, note.Content ?? string.Empty);

    return new NoteResponse
    {
        Id = note.Id,
        Title = note.Title,
        Content = note.Content,
        CreatedAt = note.CreatedAt,
        UpdatedAt = note.UpdatedAt,
        IsPinned = note.IsPinned,
        IsArchived = note.IsArchived
    };
}

public async Task TogglePinAsync(Guid id)
{
    var note = await _noteRepository.GetByIdAsync(id, _currentUserService.UserId);

    if (note is null)
        return;

    note.IsPinned = !note.IsPinned;

    await _noteRepository.UpdateAsync(note);
}

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Schedules semantic indexing for a note on a background thread using its own
    /// DI scope.  This keeps note create/update HTTP responses instant and prevents
    /// client-disconnect from aborting a partially-written EmbeddingRecord.
    /// </summary>
    private void EnqueueIndex(Guid userId, Guid noteId, string title, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var capturedUserId = userId;
        var capturedId     = noteId;
        var capturedTitle  = title;
        var capturedText   = text;

        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope        = _scopeFactory.CreateAsyncScope();
                var             indexService = scope.ServiceProvider
                                                   .GetRequiredService<ISemanticIndexService>();
                await indexService.IndexAsync(
                    capturedUserId, EmbeddingSourceType.Note,
                    capturedId, capturedTitle, capturedText);
            }
            catch (Exception ex)
            {
                // Best-effort; the Note row is already committed.
                // Run POST /api/semantic-search/backfill to retry.
                _logger.LogError(ex,
                    "NoteService: background semantic indexing FAILED for note {NoteId} ('{Title}'). " +
                    "Run POST /api/semantic-search/backfill to retry.",
                    capturedId, capturedTitle);
            }
        });
    }
}