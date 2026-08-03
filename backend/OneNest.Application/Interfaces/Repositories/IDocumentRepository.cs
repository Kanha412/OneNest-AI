using OneNest.Domain.Entities;
using OneNest.Domain.Enums;

namespace OneNest.Application.Interfaces.Repositories;

public interface IDocumentRepository
{
    Task<List<Document>> GetAllAsync(Guid userId);

    Task<Document?> GetByIdAsync(Guid id, Guid userId);

    Task AddAsync(Document document);

    Task UpdateAsync(Document document);

    Task DeleteAsync(Document document);

    Task<List<Document>> SearchAsync(Guid userId, string searchTerm);

    Task<List<Document>> GetByCategoryAsync(Guid userId, DocumentCategory category);

    Task<List<Document>> GetRecentAsync(Guid userId, int count);

    Task<bool> ExistsByOriginalFileNameAsync(Guid userId, string originalFileName);
}
