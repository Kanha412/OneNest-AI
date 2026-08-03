using OneNest.Domain.Entities;

namespace OneNest.Application.Interfaces.Security;

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAt) GenerateToken(User user);
}
