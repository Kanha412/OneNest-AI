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
        // File storage backed by Supabase Storage (persists across Render restarts).
        // Local FileStorageService wrote to the container filesystem which is
        // ephemeral on Render — all uploaded files would be lost on every deploy/restart.
        services.Configure<SupabaseOptions>(configuration.GetSection("Supabase"));
        services.AddSingleton<IFileStorageService, SupabaseFileStorageService>();

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
        // Default: Local all-MiniLM-L6-v2 ONNX (offline, no API key, 384 dims, zero-cost)
        // Optional: Gemini text-embedding-004 (requires API key, 768 dims)
        //           → also update Embeddings:Dimension and EmbeddingRecords column to 768
        // Switch via  Embeddings:Provider = "Local" | "Gemini"  in appsettings.json.
        services.Configure<EmbeddingOptions>(configuration.GetSection("Embeddings"));

        var embeddingProvider = configuration["Embeddings:Provider"] ?? "Local";
        if (embeddingProvider.Equals("Local", StringComparison.OrdinalIgnoreCase))
        {
            services.Configure<LocalEmbeddingOptions>(configuration.GetSection("LocalEmbedding"));
            services.AddHttpClient<LocalEmbeddingProvider>(client =>
            {
                // Large timeout: ONNX model download (~22 MB, INT8 quantized) happens once on first use.
                // In production (Docker) the model is bundled in the image — no download occurs.
                client.Timeout = TimeSpan.FromMinutes(5);
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                UseProxy = true
            });
            services.AddSingleton<IEmbeddingProvider, LocalEmbeddingProvider>();
        }
        else
        {
            // Opt-in: Gemini text-embedding-004 (requires Embeddings:Provider=Gemini in config).
            // GeminiEmbeddingProvider owns its HttpClient directly (HttpClientHandler)
            // so the Windows native TLS stack is used on all environments.
            // IMPORTANT: also update Embeddings:Dimension=768 and EmbeddingRecords vector(768)
            // when switching to this provider.
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

        // Phase 9 — RAG (Retrieval-Augmented Generation)
        services.Configure<RagOptions>(configuration.GetSection("RAG"));
        services.AddScoped<IRagService, RagService>();

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
