using Microsoft.EntityFrameworkCore;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Domain.Entities;
using OneNest.Infrastructure.Data;

namespace OneNest.Infrastructure.Repositories;

public class AppointmentRepository : IAppointmentRepository
{
    private readonly OneNestDbContext _dbContext;

    public AppointmentRepository(OneNestDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Appointment>> GetAllAsync(Guid userId)
    {
        return await _dbContext.Appointments
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.Time)
            .ToListAsync();
    }

    public async Task<Appointment?> GetByIdAsync(Guid id, Guid userId)
    {
        return await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
    }

    public async Task AddAsync(Appointment appointment)
    {
        _dbContext.Appointments.Add(appointment);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(Appointment appointment)
    {
        _dbContext.Appointments.Update(appointment);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(Appointment appointment)
    {
        _dbContext.Appointments.Remove(appointment);
        await _dbContext.SaveChangesAsync();
    }
}
