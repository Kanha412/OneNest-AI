using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneNest.Infrastructure.Data;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Application.Interfaces.Services;
using OneNest.Application.Services;
using OneNest.Infrastructure.Repositories;

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
        return services;
    }
}