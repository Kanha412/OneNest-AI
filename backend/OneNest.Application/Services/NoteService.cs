using OneNest.Application.DTOs.Notes;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Application.Interfaces.Security;
using OneNest.Application.Interfaces.Services;
using OneNest.Domain.Entities;

namespace OneNest.Application.Services;

public class NoteService : INoteService
{
    private readonly INoteRepository _noteRepository;
    private readonly ICurrentUserService _currentUserService;

    public NoteService(INoteRepository noteRepository, ICurrentUserService currentUserService)
    {
        _noteRepository = noteRepository;
        _currentUserService = currentUserService;
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
    var note = await _noteRepository.GetByIdAsync(id, _currentUserService.UserId);

    if (note is null)
        return;

    await _noteRepository.DeleteAsync(note);
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