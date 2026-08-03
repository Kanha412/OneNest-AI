using OneNest.Application.DTOs.Health;

namespace OneNest.Application.Interfaces.Services;

public interface IMedicineService
{
    Task<List<MedicineResponse>> GetAllAsync(string? search, bool? isActive);

    Task<MedicineResponse?> GetByIdAsync(Guid id);

    Task<MedicineResponse> CreateAsync(CreateMedicineRequest request);

    Task<MedicineResponse?> UpdateAsync(Guid id, UpdateMedicineRequest request);

    Task<bool> DeleteAsync(Guid id);
}
