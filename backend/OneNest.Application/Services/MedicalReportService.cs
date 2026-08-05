using OneNest.Application.DTOs.Health;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Application.Interfaces.Security;
using OneNest.Application.Interfaces.Services;
using OneNest.Application.Interfaces.Storage;
using OneNest.Domain.Entities;
using OneNest.Domain.Enums;

namespace OneNest.Application.Services;

public class MedicalReportService : IMedicalReportService
{
    private const long MaxFileSizeBytes = 25 * 1024 * 1024; // 25 MB
    private const long MaxTotalStorageBytes = 150L * 1024 * 1024; // 150 MB

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
        ".doc", ".docx", ".txt", ".csv"
    };

    private readonly IMedicalReportRepository _medicalReportRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICurrentUserService _currentUserService;

    public MedicalReportService(
        IMedicalReportRepository medicalReportRepository,
        IDocumentRepository documentRepository,
        IFileStorageService fileStorageService,
        ICurrentUserService currentUserService)
    {
        _medicalReportRepository = medicalReportRepository;
        _documentRepository = documentRepository;
        _fileStorageService = fileStorageService;
        _currentUserService = currentUserService;
    }

    public async Task<List<MedicalReportResponse>> GetAllAsync(string? search, MedicalReportCategory? category)
    {
        var userId = _currentUserService.UserId;

        List<MedicalReport> reports;

        if (!string.IsNullOrWhiteSpace(search))
        {
            reports = await _medicalReportRepository.SearchAsync(userId, search);
        }
        else if (category.HasValue)
        {
            reports = await _medicalReportRepository.GetByCategoryAsync(userId, category.Value);
        }
        else
        {
            reports = await _medicalReportRepository.GetAllAsync(userId);
        }

        if (!string.IsNullOrWhiteSpace(search) && category.HasValue)
        {
            reports = reports.Where(x => x.Category == category.Value).ToList();
        }

        return reports.Select(MapToResponse).ToList();
    }

    public async Task<MedicalReportResponse?> GetByIdAsync(Guid id)
    {
        var report = await _medicalReportRepository.GetByIdAsync(id, _currentUserService.UserId);
        return report is null ? null : MapToResponse(report);
    }

    public async Task<MedicalReportResponse> UploadAsync(UploadMedicalReportInput input)
    {
        var userId = _currentUserService.UserId;

        if (input.Content is null || input.FileSize <= 0)
        {
            throw new InvalidOperationException("Uploaded file is empty.");
        }

        if (input.FileSize > MaxFileSizeBytes)
        {
            throw new InvalidOperationException("File size exceeds the 25 MB limit.");
        }

        var reports = await _medicalReportRepository.GetAllAsync(userId);
        var documents = await _documentRepository.GetAllAsync(userId);
        var currentUsageBytes = reports.Sum(x => x.FileSize) + documents.Sum(x => x.FileSize);
        var projectedUsageBytes = currentUsageBytes + input.FileSize;

        if (projectedUsageBytes > MaxTotalStorageBytes)
        {
            throw new InvalidOperationException($"Upload exceeds your 150 MB storage limit. Current usage: {FormatSize(currentUsageBytes)} / 150 MB.");
        }

        var extension = Path.GetExtension(input.OriginalFileName);

        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException($"File type '{extension}' is not allowed.");
        }

        var storedFileName = $"report_{Guid.NewGuid()}{extension}";

        await _fileStorageService.SaveAsync(userId, storedFileName, input.Content);

        var report = new MedicalReport
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = string.IsNullOrWhiteSpace(input.Metadata.Title)
                ? Path.GetFileNameWithoutExtension(input.OriginalFileName)
                : input.Metadata.Title,
            Category = input.Metadata.Category,
            DoctorName = input.Metadata.DoctorName,
            Hospital = input.Metadata.Hospital,
            ReportDate = input.Metadata.ReportDate == default
                ? DateOnly.FromDateTime(DateTime.UtcNow)
                : input.Metadata.ReportDate,
            Description = input.Metadata.Description,
            OriginalFileName = input.OriginalFileName,
            StoredFileName = storedFileName,
            ContentType = input.ContentType,
            FileSize = input.FileSize,
            CreatedAt = DateTime.UtcNow
        };

        await _medicalReportRepository.AddAsync(report);

        return MapToResponse(report);
    }

    public async Task<MedicalReportResponse?> UpdateAsync(Guid id, UpdateMedicalReportRequest request)
    {
        var report = await _medicalReportRepository.GetByIdAsync(id, _currentUserService.UserId);

        if (report is null)
            return null;

        report.Title = request.Title;
        report.Category = request.Category;
        report.DoctorName = request.DoctorName;
        report.Hospital = request.Hospital;
        report.ReportDate = request.ReportDate;
        report.Description = request.Description;
        report.UpdatedAt = DateTime.UtcNow;

        await _medicalReportRepository.UpdateAsync(report);

        return MapToResponse(report);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var userId = _currentUserService.UserId;
        var report = await _medicalReportRepository.GetByIdAsync(id, userId);

        if (report is null)
            return false;

        await _fileStorageService.DeleteAsync(userId, report.StoredFileName);
        await _medicalReportRepository.DeleteAsync(report);

        return true;
    }

    public async Task<MedicalReportFileResult?> DownloadAsync(Guid id)
    {
        var userId = _currentUserService.UserId;
        var report = await _medicalReportRepository.GetByIdAsync(id, userId);

        if (report is null)
            return null;

        var stream = await _fileStorageService.OpenReadAsync(userId, report.StoredFileName);

        if (stream is null)
            return null;

        return new MedicalReportFileResult
        {
            Content = stream,
            ContentType = string.IsNullOrWhiteSpace(report.ContentType)
                ? "application/octet-stream"
                : report.ContentType,
            OriginalFileName = report.OriginalFileName
        };
    }

    private static MedicalReportResponse MapToResponse(MedicalReport report)
    {
        return new MedicalReportResponse
        {
            Id = report.Id,
            Title = report.Title,
            Category = report.Category,
            DoctorName = report.DoctorName,
            Hospital = report.Hospital,
            ReportDate = report.ReportDate,
            Description = report.Description,
            OriginalFileName = report.OriginalFileName,
            ContentType = report.ContentType,
            FileSize = report.FileSize,
            CreatedAt = report.CreatedAt,
            UpdatedAt = report.UpdatedAt
        };
    }

    private static string FormatSize(long bytes)
    {
        var valueInMb = bytes / (1024d * 1024d);
        return $"{valueInMb:0.##} MB";
    }
}
