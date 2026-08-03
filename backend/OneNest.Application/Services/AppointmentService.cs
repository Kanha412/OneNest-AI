using OneNest.Application.DTOs.Health;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Application.Interfaces.Security;
using OneNest.Application.Interfaces.Services;
using OneNest.Domain.Entities;
using OneNest.Domain.Enums;

namespace OneNest.Application.Services;

public class AppointmentService : IAppointmentService
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly ICurrentUserService _currentUserService;

    public AppointmentService(
        IAppointmentRepository appointmentRepository,
        ICurrentUserService currentUserService)
    {
        _appointmentRepository = appointmentRepository;
        _currentUserService = currentUserService;
    }

    public async Task<List<AppointmentResponse>> GetAllAsync(string? search, AppointmentStatus? status)
    {
        var appointments = await _appointmentRepository.GetAllAsync(_currentUserService.UserId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            appointments = appointments
                .Where(x => x.DoctorName.ToLower().Contains(term) ||
                            x.Hospital.ToLower().Contains(term) ||
                            x.Specialty.ToLower().Contains(term))
                .ToList();
        }

        if (status.HasValue)
        {
            appointments = appointments.Where(x => x.Status == status.Value).ToList();
        }

        return appointments.Select(MapToResponse).ToList();
    }

    public async Task<AppointmentResponse?> GetByIdAsync(Guid id)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(id, _currentUserService.UserId);
        return appointment is null ? null : MapToResponse(appointment);
    }

    public async Task<AppointmentResponse> CreateAsync(CreateAppointmentRequest request)
    {
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            UserId = _currentUserService.UserId,
            DoctorName = request.DoctorName.Trim(),
            Hospital = request.Hospital,
            Specialty = request.Specialty,
            Date = request.Date,
            Time = request.Time,
            Notes = request.Notes,
            Status = request.Status,
            CreatedAt = DateTime.UtcNow
        };

        await _appointmentRepository.AddAsync(appointment);

        return MapToResponse(appointment);
    }

    public async Task<AppointmentResponse?> UpdateAsync(Guid id, UpdateAppointmentRequest request)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(id, _currentUserService.UserId);

        if (appointment is null)
            return null;

        appointment.DoctorName = request.DoctorName.Trim();
        appointment.Hospital = request.Hospital;
        appointment.Specialty = request.Specialty;
        appointment.Date = request.Date;
        appointment.Time = request.Time;
        appointment.Notes = request.Notes;
        appointment.Status = request.Status;
        appointment.UpdatedAt = DateTime.UtcNow;

        await _appointmentRepository.UpdateAsync(appointment);

        return MapToResponse(appointment);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(id, _currentUserService.UserId);

        if (appointment is null)
            return false;

        await _appointmentRepository.DeleteAsync(appointment);
        return true;
    }

    private static AppointmentResponse MapToResponse(Appointment appointment)
    {
        return new AppointmentResponse
        {
            Id = appointment.Id,
            DoctorName = appointment.DoctorName,
            Hospital = appointment.Hospital,
            Specialty = appointment.Specialty,
            Date = appointment.Date,
            Time = appointment.Time,
            Notes = appointment.Notes,
            Status = appointment.Status,
            CreatedAt = appointment.CreatedAt,
            UpdatedAt = appointment.UpdatedAt
        };
    }
}
