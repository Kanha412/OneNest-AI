using OneNest.Application.DTOs.Health;
using OneNest.Domain.Enums;

namespace OneNest.Application.Interfaces.Services;

public interface IAppointmentService
{
    Task<List<AppointmentResponse>> GetAllAsync(string? search, AppointmentStatus? status);

    Task<AppointmentResponse?> GetByIdAsync(Guid id);

    Task<AppointmentResponse> CreateAsync(CreateAppointmentRequest request);

    Task<AppointmentResponse?> UpdateAsync(Guid id, UpdateAppointmentRequest request);

    Task<bool> DeleteAsync(Guid id);
}
