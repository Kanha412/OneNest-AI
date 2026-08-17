using Microsoft.EntityFrameworkCore;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Domain.Entities;
using OneNest.Infrastructure.Data;

namespace OneNest.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly OneNestDbContext _dbContext;

    public UserRepository(OneNestDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _dbContext.Users
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<List<User>> GetAllAsync()
    {
        return await _dbContext.Users
            .OrderBy(x => x.FullName)
            .ToListAsync();
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _dbContext.Users
            .FirstOrDefaultAsync(x => x.Email == email);
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _dbContext.Users
            .AnyAsync(x => x.Email == email);
    }

    public async Task AddAsync(User user)
    {
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(User user)
    {
        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync();
    }

    public async Task HardDeleteAccountAsync(Guid userId)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return;
        }

        var userSettings = await _dbContext.UserSettings.Where(x => x.UserId == userId).ToListAsync();
        var notes = await _dbContext.Notes.Where(x => x.UserId == userId).ToListAsync();
        var tasks = await _dbContext.Tasks.Where(x => x.UserId == userId).ToListAsync();
        var expenses = await _dbContext.Expenses.Where(x => x.UserId == userId).ToListAsync();
        var medicines = await _dbContext.Medicines.Where(x => x.UserId == userId).ToListAsync();
        var appointments = await _dbContext.Appointments.Where(x => x.UserId == userId).ToListAsync();
        var medicalRecords = await _dbContext.MedicalRecords.Where(x => x.UserId == userId).ToListAsync();
        var documents = await _dbContext.Documents.Where(x => x.UserId == userId).ToListAsync();
        var medicalReports = await _dbContext.MedicalReports.Where(x => x.UserId == userId).ToListAsync();
        var aiConversations = await _dbContext.AIConversations.Where(x => x.UserId == userId).ToListAsync();

        _dbContext.UserSettings.RemoveRange(userSettings);
        _dbContext.Notes.RemoveRange(notes);
        _dbContext.Tasks.RemoveRange(tasks);
        _dbContext.Expenses.RemoveRange(expenses);
        _dbContext.Medicines.RemoveRange(medicines);
        _dbContext.Appointments.RemoveRange(appointments);
        _dbContext.MedicalRecords.RemoveRange(medicalRecords);
        _dbContext.Documents.RemoveRange(documents);
        _dbContext.MedicalReports.RemoveRange(medicalReports);
        _dbContext.AIConversations.RemoveRange(aiConversations);
        _dbContext.Users.Remove(user);

        await _dbContext.SaveChangesAsync();
    }
}
