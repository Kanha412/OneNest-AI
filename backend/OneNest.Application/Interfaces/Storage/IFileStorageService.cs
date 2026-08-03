namespace OneNest.Application.Interfaces.Storage;

public interface IFileStorageService
{
    Task<string> SaveAsync(Guid userId, string storedFileName, Stream content);

    Task<Stream?> OpenReadAsync(Guid userId, string storedFileName);

    Task DeleteAsync(Guid userId, string storedFileName);
}
