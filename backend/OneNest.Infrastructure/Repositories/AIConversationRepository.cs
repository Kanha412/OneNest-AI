using Microsoft.EntityFrameworkCore;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Domain.Entities;
using OneNest.Infrastructure.Data;

namespace OneNest.Infrastructure.Repositories;

public class AIConversationRepository : IAIConversationRepository
{
    private readonly OneNestDbContext _dbContext;

    public AIConversationRepository(OneNestDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<AIConversation>> GetAllForUserAsync(Guid userId, bool includeArchived = false)
    {
        var query = _dbContext.AIConversations
            .Where(x => x.UserId == userId && !x.IsDeleted);

        if (!includeArchived)
        {
            query = query.Where(x => !x.IsArchived);
        }

        return await query
            .OrderByDescending(x => x.LastMessageAt)
            .ToListAsync();
    }

    public async Task<List<AIConversation>> SearchConversationsAsync(Guid userId, string query, bool includeArchived = false)
    {
        var term = query.Trim().ToLower();

        var dbQuery = _dbContext.AIConversations
            .Where(x => x.UserId == userId && !x.IsDeleted && x.Title.ToLower().Contains(term));

        if (!includeArchived)
        {
            dbQuery = dbQuery.Where(x => !x.IsArchived);
        }

        return await dbQuery
            .OrderByDescending(x => x.LastMessageAt)
            .ToListAsync();
    }

    public async Task<AIConversation?> GetConversationAsync(Guid id, Guid userId)
    {
        return await _dbContext.AIConversations
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId && !x.IsDeleted);
    }

    public async Task<List<AIMessage>> GetMessagesAsync(Guid conversationId, Guid userId)
    {
        return await _dbContext.AIMessages
            .Where(x => x.ConversationId == conversationId && x.Conversation != null && x.Conversation.UserId == userId && !x.Conversation.IsDeleted)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<AIMessage>> GetLastMessagesAsync(Guid conversationId, Guid userId, int limit)
    {
        return await _dbContext.AIMessages
            .Where(x => x.ConversationId == conversationId && x.Conversation != null && x.Conversation.UserId == userId && !x.Conversation.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task AddConversationAsync(AIConversation conversation)
    {
        _dbContext.AIConversations.Add(conversation);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateConversationAsync(AIConversation conversation)
    {
        _dbContext.AIConversations.Update(conversation);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteConversationAsync(AIConversation conversation)
    {
        // Hard delete — CASCADE on AIMessages FK removes all messages automatically.
        _dbContext.AIConversations.Remove(conversation);
        await _dbContext.SaveChangesAsync();
    }

    public async Task AddMessageAsync(AIMessage message)
    {
        _dbContext.AIMessages.Add(message);
        await _dbContext.SaveChangesAsync();
    }
}
