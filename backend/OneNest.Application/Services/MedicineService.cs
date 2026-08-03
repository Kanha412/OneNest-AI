using OneNest.Application.DTOs.Health;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Application.Interfaces.Security;
using OneNest.Application.Interfaces.Services;
using OneNest.Domain.Entities;

namespace OneNest.Application.Services;

public class MedicineService : IMedicineService
{
    private readonly IMedicineRepository _medicineRepository;
    private readonly ICurrentUserService _currentUserService;

    public MedicineService(
        IMedicineRepository medicineRepository,
        ICurrentUserService currentUserService)
    {
        _medicineRepository = medicineRepository;
        _currentUserService = currentUserService;
    }

    public async Task<List<MedicineResponse>> GetAllAsync(string? search, bool? isActive)
    {
        var medicines = await _medicineRepository.GetAllAsync(_currentUserService.UserId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            medicines = medicines
                .Where(x => x.Name.ToLower().Contains(term) ||
                            x.Frequency.ToLower().Contains(term) ||
                            x.Instructions.ToLower().Contains(term))
                .ToList();
        }

        if (isActive.HasValue)
        {
            medicines = medicines.Where(x => x.IsActive == isActive.Value).ToList();
        }

        return medicines.Select(MapToResponse).ToList();
    }

    public async Task<MedicineResponse?> GetByIdAsync(Guid id)
    {
        var medicine = await _medicineRepository.GetByIdAsync(id, _currentUserService.UserId);
        return medicine is null ? null : MapToResponse(medicine);
    }

    public async Task<MedicineResponse> CreateAsync(CreateMedicineRequest request)
    {
        Validate(request.StartDate, request.EndDate);

        var medicine = new Medicine
        {
            Id = Guid.NewGuid(),
            UserId = _currentUserService.UserId,
            Name = request.Name.Trim(),
            Dosage = request.Dosage,
            Frequency = request.Frequency,
            Morning = request.Morning,
            Afternoon = request.Afternoon,
            Night = request.Night,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Instructions = request.Instructions,
            FoodTiming = request.FoodTiming,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        await _medicineRepository.AddAsync(medicine);

        return MapToResponse(medicine);
    }

    public async Task<MedicineResponse?> UpdateAsync(Guid id, UpdateMedicineRequest request)
    {
        var medicine = await _medicineRepository.GetByIdAsync(id, _currentUserService.UserId);

        if (medicine is null)
            return null;

        Validate(request.StartDate, request.EndDate);

        medicine.Name = request.Name.Trim();
        medicine.Dosage = request.Dosage;
        medicine.Frequency = request.Frequency;
        medicine.Morning = request.Morning;
        medicine.Afternoon = request.Afternoon;
        medicine.Night = request.Night;
        medicine.StartDate = request.StartDate;
        medicine.EndDate = request.EndDate;
        medicine.Instructions = request.Instructions;
        medicine.FoodTiming = request.FoodTiming;
        medicine.IsActive = request.IsActive;
        medicine.UpdatedAt = DateTime.UtcNow;

        await _medicineRepository.UpdateAsync(medicine);

        return MapToResponse(medicine);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var medicine = await _medicineRepository.GetByIdAsync(id, _currentUserService.UserId);

        if (medicine is null)
            return false;

        await _medicineRepository.DeleteAsync(medicine);
        return true;
    }

    private static void Validate(DateOnly startDate, DateOnly? endDate)
    {
        if (endDate.HasValue && endDate.Value < startDate)
        {
            throw new InvalidOperationException("End date cannot be earlier than start date.");
        }
    }

    private static MedicineResponse MapToResponse(Medicine medicine)
    {
        return new MedicineResponse
        {
            Id = medicine.Id,
            Name = medicine.Name,
            Dosage = medicine.Dosage,
            Frequency = medicine.Frequency,
            Morning = medicine.Morning,
            Afternoon = medicine.Afternoon,
            Night = medicine.Night,
            StartDate = medicine.StartDate,
            EndDate = medicine.EndDate,
            Instructions = medicine.Instructions,
            FoodTiming = medicine.FoodTiming,
            IsActive = medicine.IsActive,
            CreatedAt = medicine.CreatedAt,
            UpdatedAt = medicine.UpdatedAt
        };
    }
}
