using OneNest.Application.Interfaces.Storage;

namespace OneNest.Infrastructure.Storage;

public class FileStorageService : IFileStorageService
{
    private readonly string _rootPath;

    public FileStorageService()
    {
        _rootPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "documents");
    }

    public async Task<string> SaveAsync(Guid userId, string storedFileName, Stream content)
    {
        var userDirectory = GetUserDirectory(userId);
        Directory.CreateDirectory(userDirectory);

        var fullPath = Path.Combine(userDirectory, storedFileName);

        await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
        await content.CopyToAsync(fileStream);

        return Path.Combine("uploads", "documents", userId.ToString(), storedFileName);
    }

    public Task<Stream?> OpenReadAsync(Guid userId, string storedFileName)
    {
        var fullPath = Path.Combine(GetUserDirectory(userId), storedFileName);

        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(Guid userId, string storedFileName)
    {
        var fullPath = Path.Combine(GetUserDirectory(userId), storedFileName);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public Task DeleteUserDirectoryAsync(Guid userId)
    {
        var userDirectory = GetUserDirectory(userId);
        if (Directory.Exists(userDirectory))
        {
            Directory.Delete(userDirectory, recursive: true);
        }

        return Task.CompletedTask;
    }

    private string GetUserDirectory(Guid userId)
    {
        return Path.Combine(_rootPath, userId.ToString());
    }
}
