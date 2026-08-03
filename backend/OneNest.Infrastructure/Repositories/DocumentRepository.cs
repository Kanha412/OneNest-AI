using Microsoft.EntityFrameworkCore;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Domain.Entities;
using OneNest.Domain.Enums;
using OneNest.Infrastructure.Data;

namespace OneNest.Infrastructure.Repositories;

public class DocumentRepository : IDocumentRepository
{
    private readonly OneNestDbContext _dbContext;

    public DocumentRepository(OneNestDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Document>> GetAllAsync(Guid userId)
    {
        return await _dbContext.Documents
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<Document?> GetByIdAsync(Guid id, Guid userId)
    {
        return await _dbContext.Documents
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
    }

    public async Task AddAsync(Document document)
    {
        _dbContext.Documents.Add(document);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(Document document)
    {
        _dbContext.Documents.Update(document);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(Document document)
    {
        _dbContext.Documents.Remove(document);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<List<Document>> SearchAsync(Guid userId, string searchTerm)
    {
        var term = searchTerm.Trim().ToLower();

        return await _dbContext.Documents
            .Where(x => x.UserId == userId &&
                (x.Title.ToLower().Contains(term) ||
                 x.OriginalFileName.ToLower().Contains(term) ||
                 x.Description.ToLower().Contains(term)))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Document>> GetByCategoryAsync(Guid userId, DocumentCategory category)
    {
        return await _dbContext.Documents
            .Where(x => x.UserId == userId && x.Category == category)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Document>> GetRecentAsync(Guid userId, int count)
    {
        return await _dbContext.Documents
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<bool> ExistsByOriginalFileNameAsync(Guid userId, string originalFileName)
    {
        var name = originalFileName.Trim().ToLower();

        return await _dbContext.Documents
            .AnyAsync(x => x.UserId == userId && x.OriginalFileName.ToLower() == name);
    }
}
