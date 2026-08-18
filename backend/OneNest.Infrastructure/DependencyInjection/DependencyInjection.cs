using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using OneNest.Infrastructure.AI;
using OneNest.Infrastructure.AI.WorkspaceTools;
using OneNest.Infrastructure.Data;
using OneNest.Infrastructure.Documents;
using OneNest.Infrastructure.Repositories;
using OneNest.Application.Interfaces.AI;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Application.Interfaces.Security;
using OneNest.Application.Interfaces.Services;
using OneNest.Application.Interfaces.Storage;
using OneNest.Application.Services;
using OneNest.Domain.Entities;
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
        services.AddScoped<IDocumentTextExtractor, DocumentTextExtractor>();
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
        // Use HttpClientHandler (WinHTTP on Windows) so corporate proxies / Zscaler
        // that perform SSL inspection are handled by the native Windows TLS stack,
        // the same path curl.exe and the browser use.
        services.AddHttpClient<IAIProvider, GeminiProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            UseProxy = true
        });

        // Phase 8 — configurable embedding provider
        // Default: Gemini text-embedding-004 (hosted, $0 free tier, 768 dims)
        // Optional: Local all-MiniLM-L6-v2 ONNX (offline, no API key, 384 dims)
        // Switch via  Embeddings:Provider = "Gemini" | "Local"  in appsettings.json.
        services.Configure<EmbeddingOptions>(configuration.GetSection("Embeddings"));

        var embeddingProvider = configuration["Embeddings:Provider"] ?? "Gemini";
        if (embeddingProvider.Equals("Local", StringComparison.OrdinalIgnoreCase))
        {
            services.Configure<LocalEmbeddingOptions>(configuration.GetSection("LocalEmbedding"));
            services.AddHttpClient<LocalEmbeddingProvider>(client =>
            {
                // Large timeout: ONNX model download (~22 MB) happens once on first use.
                client.Timeout = TimeSpan.FromMinutes(5);
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                UseProxy = true
            });
            services.AddSingleton<IEmbeddingProvider, LocalEmbeddingProvider>();
        }
        else
        {
            // Default: Gemini text-embedding-004.
            // GeminiEmbeddingProvider owns its HttpClient directly (HttpClientHandler)
            // so the Windows native TLS stack is used on all environments.
            services.AddSingleton<IEmbeddingProvider, GeminiEmbeddingProvider>();
        }
        services.AddScoped<IAIConversationRepository, AIConversationRepository>();
        services.AddScoped<IEmbeddingRepository, EmbeddingRepository>();
        services.AddScoped<IAIWorkspacePlanner, AIWorkspacePlanner>();
        services.AddScoped<IAIWorkspaceOrchestrator, AIWorkspaceOrchestrator>();
        services.AddScoped<IAIWorkspaceTool, TasksWorkspaceTool>();
        services.AddScoped<IAIWorkspaceTool, ExpensesWorkspaceTool>();
        services.AddScoped<IAIWorkspaceTool, NotesWorkspaceTool>();
        services.AddScoped<IAIWorkspaceTool, DocumentsWorkspaceTool>();
        services.AddScoped<IAIWorkspaceTool, HealthWorkspaceTool>();
        services.AddScoped<IAIConversationService, AIConversationService>();
        services.AddScoped<IAIService, AIService>();
        // Phase 8 — text chunking (singleton: stateless, cheap to share)
        services.Configure<TextChunkerOptions>(configuration.GetSection("TextChunker"));
        services.AddSingleton<ITextChunker, TextChunker>();

        services.AddScoped<ISemanticIndexService, SemanticIndexService>();
        services.AddScoped<ISemanticSearchService, SemanticSearchService>();
        services.AddScoped<IBackfillService, BackfillService>();

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
