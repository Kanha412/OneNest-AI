using OneNest.Application.DTOs.Notes;

namespace OneNest.Application.Interfaces.Services;

public interface INoteService
{
    Task<List<NoteResponse>> GetAllAsync();

    Task<NoteResponse> CreateAsync(CreateNoteRequest request);

    Task DeleteAsync(Guid id);
}