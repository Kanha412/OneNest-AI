using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using OneNest.Application.Interfaces.Security;

namespace OneNest.Infrastructure.Security;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId
    {
        get
        {
            var userId = _httpContextAccessor.HttpContext?.User
                .FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(userId, out var id)
                ? id
                : throw new UnauthorizedAccessException("User is not authenticated.");
        }
    }
}
