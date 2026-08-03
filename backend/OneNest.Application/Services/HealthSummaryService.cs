using OneNest.Application.DTOs.Health;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Application.Interfaces.Security;
using OneNest.Application.Interfaces.Services;
using OneNest.Domain.Enums;

namespace OneNest.Application.Services;

public class HealthSummaryService : IHealthSummaryService
{
    private const int RecentReportsCount = 3;
    private const int UpcomingAppointmentsCount = 3;
    private const int ExpiringSoonDays = 7;

    private readonly IMedicineRepository _medicineRepository;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IMedicalReportRepository _medicalReportRepository;
    private readonly IMedicalRecordRepository _medicalRecordRepository;
    private readonly ICurrentUserService _currentUserService;

    public HealthSummaryService(
        IMedicineRepository medicineRepository,
        IAppointmentRepository appointmentRepository,
        IMedicalReportRepository medicalReportRepository,
        IMedicalRecordRepository medicalRecordRepository,
        ICurrentUserService currentUserService)
    {
        _medicineRepository = medicineRepository;
        _appointmentRepository = appointmentRepository;
        _medicalReportRepository = medicalReportRepository;
        _medicalRecordRepository = medicalRecordRepository;
        _currentUserService = currentUserService;
    }

    public async Task<HealthSummaryResponse> GetSummaryAsync()
    {
        var userId = _currentUserService.UserId;

        var medicines = await _medicineRepository.GetAllAsync(userId);
        var appointments = await _appointmentRepository.GetAllAsync(userId);
        var reports = await _medicalReportRepository.GetAllAsync(userId);
        var record = await _medicalRecordRepository.GetByUserAsync(userId);

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var expiringThreshold = today.AddDays(ExpiringSoonDays);

        bool IsUpcoming(Domain.Entities.Appointment appointment) =>
            appointment.Status == AppointmentStatus.Scheduled &&
            appointment.Date.ToDateTime(appointment.Time) >= now;

        var activeMedicines = medicines.Where(x => x.IsActive).ToList();

        return new HealthSummaryResponse
        {
            ActiveMedicines = activeMedicines.Count,
            TodayMedicines = activeMedicines.Count(x =>
                x.StartDate <= today && (x.EndDate == null || x.EndDate >= today)),
            ExpiringSoonMedicines = activeMedicines.Count(x =>
                x.EndDate != null && x.EndDate >= today && x.EndDate <= expiringThreshold),
            UpcomingAppointments = appointments.Count(IsUpcoming),
            PastAppointments = appointments.Count(x =>
                x.Status == AppointmentStatus.Completed || !IsUpcoming(x)),
            TotalReports = reports.Count,
            LastRecordUpdate = record?.UpdatedAt ?? record?.CreatedAt,
            MedicineDistribution = new List<MedicineTimingDistribution>
            {
                new() { Timing = "Morning", Count = activeMedicines.Count(x => x.Morning) },
                new() { Timing = "Afternoon", Count = activeMedicines.Count(x => x.Afternoon) },
                new() { Timing = "Night", Count = activeMedicines.Count(x => x.Night) }
            },
            AppointmentTimeline = appointments
                .GroupBy(x => new { x.Date.Year, x.Date.Month })
                .Select(g => new AppointmentTimelinePoint
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Count = g.Count()
                })
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ToList(),
            RecentReports = reports
                .OrderByDescending(x => x.CreatedAt)
                .Take(RecentReportsCount)
                .Select(x => new MedicalReportResponse
                {
                    Id = x.Id,
                    Title = x.Title,
                    Category = x.Category,
                    DoctorName = x.DoctorName,
                    Hospital = x.Hospital,
                    ReportDate = x.ReportDate,
                    Description = x.Description,
                    OriginalFileName = x.OriginalFileName,
                    ContentType = x.ContentType,
                    FileSize = x.FileSize,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .ToList(),
            UpcomingAppointmentsList = appointments
                .Where(IsUpcoming)
                .OrderBy(x => x.Date)
                .ThenBy(x => x.Time)
                .Take(UpcomingAppointmentsCount)
                .Select(x => new AppointmentResponse
                {
                    Id = x.Id,
                    DoctorName = x.DoctorName,
                    Hospital = x.Hospital,
                    Specialty = x.Specialty,
                    Date = x.Date,
                    Time = x.Time,
                    Notes = x.Notes,
                    Status = x.Status,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .ToList()
        };
    }
}
