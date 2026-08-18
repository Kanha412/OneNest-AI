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

    public NoteService(
        INoteRepository noteRepository,
        ICurrentUserService currentUserService,
        ISemanticIndexService semanticIndexService)
    {
        _noteRepository = noteRepository;
        _currentUserService = currentUserService;
        _semanticIndexService = semanticIndexService;
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

    // Phase 8 — best-effort semantic indexing; never blocks note creation
    await _semanticIndexService.IndexAsync(
        note.UserId, EmbeddingSourceType.Note, note.Id,
        note.Title, note.Content ?? string.Empty);

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

    // Phase 8 — re-index with updated content
    await _semanticIndexService.IndexAsync(
        note.UserId, EmbeddingSourceType.Note, note.Id,
        note.Title, note.Content ?? string.Empty);

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
}