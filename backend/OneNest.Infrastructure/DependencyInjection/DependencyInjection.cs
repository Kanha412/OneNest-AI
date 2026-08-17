using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using OneNest.Infrastructure.AI;
using OneNest.Infrastructure.AI.WorkspaceTools;
using OneNest.Infrastructure.Data;
using OneNest.Application.Interfaces.AI;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Application.Interfaces.Security;
using OneNest.Application.Interfaces.Services;
using OneNest.Application.Interfaces.Storage;
using OneNest.Application.Services;
using OneNest.Domain.Entities;
using OneNest.Infrastructure.Repositories;
using OneNest.Infrastructure.Security;
using OneNest.Infrastructure.Storage;

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
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddSingleton<IFileStorageService, FileStorageService>();

        services.AddScoped<IMedicineRepository, MedicineRepository>();
        services.AddScoped<IMedicineService, MedicineService>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        services.AddScoped<IMedicalRecordRepository, MedicalRecordRepository>();
        services.AddScoped<IMedicalRecordService, MedicalRecordService>();
        services.AddScoped<IMedicalReportRepository, MedicalReportRepository>();
        services.AddScoped<IMedicalReportService, MedicalReportService>();
        services.AddScoped<IHealthSummaryService, HealthSummaryService>();

        services.Configure<AIOptions>(configuration.GetSection("AI"));
        services.AddHttpClient<IAIProvider, GeminiProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IAIConversationRepository, AIConversationRepository>();
        services.AddScoped<IAIWorkspacePlanner, AIWorkspacePlanner>();
        services.AddScoped<IAIWorkspaceOrchestrator, AIWorkspaceOrchestrator>();
        services.AddScoped<IAIWorkspaceTool, TasksWorkspaceTool>();
        services.AddScoped<IAIWorkspaceTool, ExpensesWorkspaceTool>();
        services.AddScoped<IAIWorkspaceTool, NotesWorkspaceTool>();
        services.AddScoped<IAIWorkspaceTool, DocumentsWorkspaceTool>();
        services.AddScoped<IAIWorkspaceTool, HealthWorkspaceTool>();
        services.AddScoped<IAIConversationService, AIConversationService>();
        services.AddScoped<IAIService, AIService>();

        services.AddScoped<IContactRepository, ContactRepository>();
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<IAdminService, AdminService>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserSettingsRepository, UserSettingsRepository>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddHttpContextAccessor();

        return services;
    }
}