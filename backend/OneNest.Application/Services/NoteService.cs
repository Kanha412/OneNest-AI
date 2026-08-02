using OneNest.Application.DTOs.Notes;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Application.Interfaces.Services;
using OneNest.Domain.Entities;

namespace OneNest.Application.Services;

public class NoteService : INoteService
{
    private readonly INoteRepository _noteRepository;

    public NoteService(INoteRepository noteRepository)
    {
        _noteRepository = noteRepository;
    }

    public async Task<List<NoteResponse>> GetAllAsync()
{
    var notes = await _noteRepository.GetAllAsync();

    return notes.Select(note => new NoteResponse
    {
        Id = note.Id,
        Title = note.Title,
        Content = note.Content,
        CreatedAt = note.CreatedAt,
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
        CreatedAt = DateTime.UtcNow
    };

    await _noteRepository.AddAsync(note);

    return new NoteResponse
    {
        Id = note.Id,
        Title = note.Title,
        Content = note.Content,
        CreatedAt = note.CreatedAt,
        IsPinned = note.IsPinned,
        IsArchived = note.IsArchived
    };
}

    public async Task DeleteAsync(Guid id)
{
    var note = await _noteRepository.GetByIdAsync(id);

    if (note is null)
        return;

    await _noteRepository.DeleteAsync(note);
}
}