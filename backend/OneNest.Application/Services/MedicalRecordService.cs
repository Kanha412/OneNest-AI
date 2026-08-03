using OneNest.Application.DTOs.Health;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Application.Interfaces.Security;
using OneNest.Application.Interfaces.Services;
using OneNest.Domain.Entities;

namespace OneNest.Application.Services;

public class MedicalRecordService : IMedicalRecordService
{
    private readonly IMedicalRecordRepository _medicalRecordRepository;
    private readonly ICurrentUserService _currentUserService;

    public MedicalRecordService(
        IMedicalRecordRepository medicalRecordRepository,
        ICurrentUserService currentUserService)
    {
        _medicalRecordRepository = medicalRecordRepository;
        _currentUserService = currentUserService;
    }

    public async Task<MedicalRecordResponse?> GetAsync()
    {
        var record = await _medicalRecordRepository.GetByUserAsync(_currentUserService.UserId);
        return record is null ? null : MapToResponse(record);
    }

    public async Task<MedicalRecordResponse> SaveAsync(SaveMedicalRecordRequest request)
    {
        var userId = _currentUserService.UserId;
        var record = await _medicalRecordRepository.GetByUserAsync(userId);

        if (record is null)
        {
            record = new MedicalRecord
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            Apply(record, request);
            await _medicalRecordRepository.AddAsync(record);
        }
        else
        {
            Apply(record, request);
            record.UpdatedAt = DateTime.UtcNow;
            await _medicalRecordRepository.UpdateAsync(record);
        }

        return MapToResponse(record);
    }

    private static void Apply(MedicalRecord record, SaveMedicalRecordRequest request)
    {
        record.BloodGroup = request.BloodGroup;
        record.HeightCm = request.HeightCm;
        record.WeightKg = request.WeightKg;
        record.Allergies = request.Allergies;
        record.ExistingConditions = request.ExistingConditions;
        record.EmergencyContactName = request.EmergencyContactName;
        record.EmergencyContactPhone = request.EmergencyContactPhone;
    }

    private static MedicalRecordResponse MapToResponse(MedicalRecord record)
    {
        return new MedicalRecordResponse
        {
            Id = record.Id,
            BloodGroup = record.BloodGroup,
            HeightCm = record.HeightCm,
            WeightKg = record.WeightKg,
            Allergies = record.Allergies,
            ExistingConditions = record.ExistingConditions,
            EmergencyContactName = record.EmergencyContactName,
            EmergencyContactPhone = record.EmergencyContactPhone,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt
        };
    }
}
