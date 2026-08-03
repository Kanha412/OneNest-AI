using OneNest.Domain.Enums;

namespace OneNest.Application.DTOs.Health;

public class MedicineTimingDistribution
{
    public string Timing { get; set; } = string.Empty;

    public int Count { get; set; }
}

public class AppointmentTimelinePoint
{
    public int Year { get; set; }

    public int Month { get; set; }

    public int Count { get; set; }
}

public class HealthSummaryResponse
{
    public int ActiveMedicines { get; set; }

    public int TodayMedicines { get; set; }

    public int ExpiringSoonMedicines { get; set; }

    public int UpcomingAppointments { get; set; }

    public int PastAppointments { get; set; }

    public int TotalReports { get; set; }

    public DateTime? LastRecordUpdate { get; set; }

    public List<MedicineTimingDistribution> MedicineDistribution { get; set; } = new();

    public List<AppointmentTimelinePoint> AppointmentTimeline { get; set; } = new();

    public List<MedicalReportResponse> RecentReports { get; set; } = new();

    public List<AppointmentResponse> UpcomingAppointmentsList { get; set; } = new();
}
