using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using OneNest.Infrastructure.Data;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Application.Interfaces.Security;
using OneNest.Application.Interfaces.Services;
using OneNest.Application.Services;
using OneNest.Domain.Entities;
using OneNest.Infrastructure.Repositories;
using OneNest.Infrastructure.Security;

namespace OneNest.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<OneNestDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection")));
        services.AddScoped<INoteRepository, NoteRepository>();
        services.AddScoped<INoteService, NoteService>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<IExpenseService, ExpenseService>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddHttpContextAccessor();

        return services;
    }
}