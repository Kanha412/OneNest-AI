namespace OneNest.Application.Interfaces.Security;

public interface ICurrentUserService
{
    Guid UserId { get; }
}
