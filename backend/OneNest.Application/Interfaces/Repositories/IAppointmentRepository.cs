using OneNest.Domain.Entities;

namespace OneNest.Application.Interfaces.Repositories;

public interface IAppointmentRepository
{
    Task<List<Appointment>> GetAllAsync(Guid userId);

    Task<Appointment?> GetByIdAsync(Guid id, Guid userId);

    Task AddAsync(Appointment appointment);

    Task UpdateAsync(Appointment appointment);

    Task DeleteAsync(Appointment appointment);
}
