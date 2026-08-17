using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OneNest.Application.DTOs.AI;
using OneNest.Application.Interfaces.AI;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Application.Interfaces.Security;
using OneNest.Application.Interfaces.Services;
using OneNest.Domain.Entities;
using OneNest.Domain.Enums;

namespace OneNest.Infrastructure.AI;

public class AIConversationService : IAIConversationService
{
    private const int HistoryLimit = 20;
    private const string MetadataMarkerStart = "<!--ONENEST_AI_META:";
    private const string MetadataMarkerLegacyStart = "<!-ONENEST_AI_META:";
    private const string MetadataMarkerEnd = "-->";

    private readonly IAIConversationRepository _conversationRepository;
    private readonly IAIProvider _provider;
    private readonly IAIWorkspaceOrchestrator _workspaceOrchestrator;
    private readonly IUserRepository _userRepository;
    private readonly IUserSettingsRepository _userSettingsRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly AIOptions _options;

    public AIConversationService(
        IAIConversationRepository conversationRepository,
        IAIProvider provider,
        IAIWorkspaceOrchestrator workspaceOrchestrator,
        IUserRepository userRepository,
        IUserSettingsRepository userSettingsRepository,
        ICurrentUserService currentUserService,
        IOptions<AIOptions> options)
    {
        _conversationRepository = conversationRepository;
        _provider = provider;
        _workspaceOrchestrator = workspaceOrchestrator;
        _userRepository = userRepository;
        _userSettingsRepository = userSettingsRepository;
        _currentUserService = currentUserService;
        _options = options.Value;
    }

    public async Task<List<ConversationListResponse>> GetConversationsAsync(bool includeArchived = false, string? search = null)
    {
        var userId = _currentUserService.UserId;

        var conversations = string.IsNullOrWhiteSpace(search)
            ? await _conversationRepository.GetAllForUserAsync(userId, includeArchived)
            : await _conversationRepository.SearchConversationsAsync(userId, search, includeArchived);

        return conversations.Select(MapList).ToList();
    }

    public async Task<ConversationResponse?> GetConversationAsync(Guid conversationId)
    {
        var userId = _currentUserService.UserId;
        var conversation = await _conversationRepository.GetConversationAsync(conversationId, userId);
        if (conversation is null)
        {
            return null;
        }

        var messages = await _conversationRepository.GetMessagesAsync(conversationId, userId);
        return MapConversation(conversation, messages);
    }

    public async Task<ConversationResponse> CreateConversationAsync(CreateConversationRequest request)
    {
        var userId = _currentUserService.UserId;
        var now = DateTime.UtcNow;

        var title = NormalizeTitle(request?.Title);

        var conversation = new AIConversation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            CreatedAt = now,
            UpdatedAt = now,
            LastMessageAt = now,
            IsArchived = false,
            IsDeleted = false
        };

        await _conversationRepository.AddConversationAsync(conversation);

        return MapConversation(conversation, new List<AIMessage>());
    }

    public async Task<ConversationResponse?> RenameConversationAsync(Guid conversationId, RenameConversationRequest request)
    {
        var userId = _currentUserService.UserId;
        var conversation = await _conversationRepository.GetConversationAsync(conversationId, userId);
        if (conversation is null)
        {
            return null;
        }

        var title = request?.Title?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException("Title cannot be empty.");
        }

        conversation.Title = title.Length > 120 ? title[..120] : title;
        conversation.UpdatedAt = DateTime.UtcNow;

        await _conversationRepository.UpdateConversationAsync(conversation);

        var messages = await _conversationRepository.GetMessagesAsync(conversationId, userId);
        return MapConversation(conversation, messages);
    }

    public async Task<bool> ArchiveConversationAsync(Guid conversationId)
    {
        var userId = _currentUserService.UserId;
        var conversation = await _conversationRepository.GetConversationAsync(conversationId, userId);
        if (conversation is null)
        {
            return false;
        }

        conversation.IsArchived = true;
        conversation.UpdatedAt = DateTime.UtcNow;
        await _conversationRepository.UpdateConversationAsync(conversation);
        return true;
    }

    public async Task<bool> UnarchiveConversationAsync(Guid conversationId)
    {
        var userId = _currentUserService.UserId;
        var conversation = await _conversationRepository.GetConversationAsync(conversationId, userId);
        if (conversation is null)
        {
            return false;
        }

        conversation.IsArchived = false;
        conversation.UpdatedAt = DateTime.UtcNow;
        await _conversationRepository.UpdateConversationAsync(conversation);
        return true;
    }

    public async Task<bool> DeleteConversationAsync(Guid conversationId)
    {
        var userId = _currentUserService.UserId;
        var conversation = await _conversationRepository.GetConversationAsync(conversationId, userId);
        if (conversation is null)
        {
            return false;
        }

        conversation.IsDeleted = true;
        conversation.UpdatedAt = DateTime.UtcNow;
        await _conversationRepository.UpdateConversationAsync(conversation);
        return true;
    }

    public async Task<ChatResponse?> SendMessageAsync(Guid conversationId, SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        var conversation = await _conversationRepository.GetConversationAsync(conversationId, userId);
        if (conversation is null)
        {
            return null;
        }

        var messageText = request?.Message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(messageText))
        {
            throw new InvalidOperationException("Message cannot be empty.");
        }

        if (messageText.Length > 8000)
        {
            throw new InvalidOperationException("Message is too long. Please keep it within 8000 characters.");
        }

        var userMessage = new AIMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            Role = MessageRole.User,
            Content = messageText,
            CreatedAt = DateTime.UtcNow,
            IsError = false
        };

        await _conversationRepository.AddMessageAsync(userMessage);

        if (string.IsNullOrWhiteSpace(conversation.Title))
        {
            conversation.Title = GenerateTitleFromPrompt(messageText);
        }

        var now = DateTime.UtcNow;
        conversation.LastMessageAt = now;
        conversation.UpdatedAt = now;
        await _conversationRepository.UpdateConversationAsync(conversation);

        var recentMessages = await _conversationRepository.GetLastMessagesAsync(conversation.Id, userId, HistoryLimit);
        var history = recentMessages.Select(MapConversationMessage).ToList();

        var user = await _userRepository.GetByIdAsync(userId);
        var userSettings = await _userSettingsRepository.GetByUserIdAsync(userId);

        var contextDepth = NormalizeAiPreference(userSettings?.ContextDepth, "medium");
        var responseStyle = NormalizeAiPreference(userSettings?.ResponseStyle, "balanced");

        var workspaceContext = await _workspaceOrchestrator.BuildContextAsync(
            new WorkspaceToolExecutionContext
            {
                UserPrompt = messageText,
                History = history,
                UtcNow = now,
                ContextDepth = contextDepth
            },
            cancellationToken);

        var scopedWorkspaceContext = ApplyContextDepth(workspaceContext.ContextBlock, contextDepth);
        var systemPrompt = BuildSystemPrompt(user?.FullName, now, scopedWorkspaceContext, responseStyle);

        try
        {
            var rawAiText = await _provider.GenerateResponseAsync(systemPrompt, history, cancellationToken);
            var parsedProviderPayload = ParsePersistedMessage(rawAiText);
            var aiText = parsedProviderPayload.Text;

            var assistantMetadata = new
            {
                usedWorkspaceData = workspaceContext.UsedWorkspaceData,
                responseMode = workspaceContext.ResponseMode,
                workspaceToolsUsed = workspaceContext.ToolResults.Select(x => x.ToolName).Distinct().ToList()
            };

            var assistantMessage = new AIMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                Role = MessageRole.Assistant,
                Content = AppendMetadataToMessage(aiText, assistantMetadata),
                CreatedAt = DateTime.UtcNow,
                IsError = false
            };

            await _conversationRepository.AddMessageAsync(assistantMessage);

            var responseTime = DateTime.UtcNow;
            conversation.LastMessageAt = responseTime;
            conversation.UpdatedAt = responseTime;
            await _conversationRepository.UpdateConversationAsync(conversation);

            return new ChatResponse
            {
                Response = aiText,
                Model = string.IsNullOrWhiteSpace(_options.Model) ? "gemini-3.5-flash" : _options.Model,
                Timestamp = assistantMessage.CreatedAt,
                UsedWorkspaceData = workspaceContext.UsedWorkspaceData,
                ResponseMode = workspaceContext.ResponseMode,
                WorkspaceToolsUsed = workspaceContext.ToolResults.Select(x => x.ToolName).Distinct().ToList()
            };
        }
        catch (InvalidOperationException ex)
        {
            var errorMetadata = new
            {
                usedWorkspaceData = workspaceContext.UsedWorkspaceData,
                responseMode = workspaceContext.ResponseMode,
                workspaceToolsUsed = workspaceContext.ToolResults.Select(x => x.ToolName).Distinct().ToList()
            };

            var errorMessage = new AIMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                Role = MessageRole.Assistant,
                Content = AppendMetadataToMessage(ex.Message, errorMetadata),
                CreatedAt = DateTime.UtcNow,
                IsError = true
            };

            await _conversationRepository.AddMessageAsync(errorMessage);

            conversation.LastMessageAt = errorMessage.CreatedAt;
            conversation.UpdatedAt = errorMessage.CreatedAt;
            await _conversationRepository.UpdateConversationAsync(conversation);

            throw;
        }
    }

    private static ConversationMessage MapConversationMessage(AIMessage message)
    {
        var payload = ParsePersistedMessage(message.Content);

        return new ConversationMessage
        {
            Role = message.Role == MessageRole.User ? "user" : "assistant",
            Content = payload.Text,
            Timestamp = message.CreatedAt
        };
    }

    private static ConversationListResponse MapList(AIConversation conversation)
    {
        return new ConversationListResponse
        {
            Id = conversation.Id,
            Title = conversation.Title,
            CreatedAt = conversation.CreatedAt,
            LastMessageAt = conversation.LastMessageAt,
            IsArchived = conversation.IsArchived
        };
    }

    private static ConversationResponse MapConversation(AIConversation conversation, List<AIMessage> messages)
    {
        return new ConversationResponse
        {
            Id = conversation.Id,
            Title = conversation.Title,
            CreatedAt = conversation.CreatedAt,
            UpdatedAt = conversation.UpdatedAt,
            LastMessageAt = conversation.LastMessageAt,
            IsArchived = conversation.IsArchived,
            Messages = messages
                .OrderBy(x => x.CreatedAt)
                .Select(x =>
                {
                    var payload = ParsePersistedMessage(x.Content);
                    return new ChatMessageResponse
                    {
                        Id = x.Id,
                        Role = x.Role == MessageRole.User ? "user" : "assistant",
                        Content = payload.Text,
                        Timestamp = x.CreatedAt,
                        IsError = x.IsError,
                        UsedWorkspaceData = payload.UsedWorkspaceData,
                        ResponseMode = payload.ResponseMode,
                        WorkspaceToolsUsed = payload.WorkspaceToolsUsed
                    };
                })
                .ToList()
        };
    }

    private static string NormalizeTitle(string? title)
    {
        var value = title?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length > 120 ? value[..120] : value;
    }

    private static string GenerateTitleFromPrompt(string prompt)
    {
        var cleaned = prompt.Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return "New Chat";
        }

        var title = cleaned.Length > 60 ? cleaned[..60] : cleaned;
        return title;
    }

    private static string BuildSystemPrompt(string? fullName, DateTime nowUtc, string? workspaceContextBlock, string responseStyle)
    {
        var safeName = string.IsNullOrWhiteSpace(fullName) ? "User" : fullName.Trim();
        var style = NormalizeAiPreference(responseStyle, "balanced");

        var builder = new StringBuilder();
        builder.AppendLine("You are OneNest AI Assistant.");
        builder.AppendLine();
        builder.AppendLine("Context:");
        builder.AppendLine($"- User name: {safeName}");
        builder.AppendLine($"- Current UTC datetime: {nowUtc:O}");
        builder.AppendLine("- Application: OneNest AI (personal productivity and life management app)");
        builder.AppendLine("- Modules currently in OneNest: Notes, Tasks, Expenses, Documents, Health");
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(workspaceContextBlock))
        {
            builder.AppendLine(workspaceContextBlock.Trim());
            builder.AppendLine();
        }

        builder.AppendLine("Instructions:");
        builder.AppendLine("- Be concise, clear, and helpful.");
        builder.AppendLine("- Use markdown when useful for readability.");
        builder.AppendLine("- When workspace context is available, prioritize it for personalized answers.");
        builder.AppendLine("- For money amounts from workspace context, preserve source currency exactly (INR/₹). Do not convert to USD unless user explicitly asks.");
        builder.AppendLine("- When workspace context is not available, continue as a general assistant.");

        if (style == "short")
        {
            builder.AppendLine("- Response style preference: SHORT. Keep replies brief and to the point (about 2-4 bullets or short paragraph unless user asks for detail).");
        }
        else if (style == "detailed")
        {
            builder.AppendLine("- Response style preference: DETAILED. Provide structured, thorough responses with clear sections and actionable guidance.");
        }
        else
        {
            builder.AppendLine("- Response style preference: BALANCED. Keep responses practical with moderate detail.");
        }

        return builder.ToString().Trim();
    }

    private static string NormalizeAiPreference(string? value, string fallback)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static string ApplyContextDepth(string? workspaceContextBlock, string contextDepth)
    {
        if (string.IsNullOrWhiteSpace(workspaceContextBlock))
        {
            return string.Empty;
        }

        var depth = NormalizeAiPreference(contextDepth, "medium");
        if (depth != "low")
        {
            return workspaceContextBlock.Trim();
        }

        var lines = workspaceContextBlock
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (!lines.Any())
        {
            return workspaceContextBlock.Trim();
        }

        var compact = new StringBuilder();
        compact.AppendLine("Workspace data retrieved for this user (low context depth):");

        for (var i = 0; i < lines.Count; i++)
        {
            if (!lines[i].StartsWith("[", StringComparison.Ordinal) || !lines[i].EndsWith("]", StringComparison.Ordinal))
            {
                continue;
            }

            compact.AppendLine(lines[i]);

            for (var j = i + 1; j < lines.Count; j++)
            {
                var next = lines[j];
                if (next.StartsWith("[", StringComparison.Ordinal) && next.EndsWith("]", StringComparison.Ordinal))
                {
                    break;
                }

                if (next.StartsWith("Use this workspace data", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                compact.AppendLine(next);
                break;
            }

            compact.AppendLine();
        }

        compact.AppendLine("Use this workspace data as the highest priority for user-specific answers.");
        return compact.ToString().Trim();
    }

    private static string AppendMetadataToMessage(string text, object metadata)
    {
        var metadataJson = JsonSerializer.Serialize(metadata);
        return $"{text}\n\n{MetadataMarkerStart}{metadataJson}{MetadataMarkerEnd}";
    }

    private static ParsedMessage ParsePersistedMessage(string content)
    {
        var value = content ?? string.Empty;

        var standardIdx = value.LastIndexOf(MetadataMarkerStart, StringComparison.Ordinal);
        var legacyIdx = value.LastIndexOf(MetadataMarkerLegacyStart, StringComparison.Ordinal);

        var idx = Math.Max(standardIdx, legacyIdx);
        if (idx < 0)
        {
            return new ParsedMessage
            {
                Text = value,
                UsedWorkspaceData = false,
                ResponseMode = "general",
                WorkspaceToolsUsed = new List<string>()
            };
        }

        var markerLength = idx == standardIdx ? MetadataMarkerStart.Length : MetadataMarkerLegacyStart.Length;

        var endIdx = value.IndexOf(MetadataMarkerEnd, idx, StringComparison.Ordinal);
        if (endIdx < 0)
        {
            return new ParsedMessage
            {
                Text = value,
                UsedWorkspaceData = false,
                ResponseMode = "general",
                WorkspaceToolsUsed = new List<string>()
            };
        }

        var text = value[..idx].TrimEnd();
        var jsonStart = idx + markerLength;
        var json = value.Substring(jsonStart, endIdx - jsonStart);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var usedWorkspaceData = root.TryGetProperty("usedWorkspaceData", out var usedEl) && usedEl.ValueKind == JsonValueKind.True;
            var responseMode = root.TryGetProperty("responseMode", out var modeEl) && modeEl.ValueKind == JsonValueKind.String
                ? (modeEl.GetString() ?? "general")
                : "general";

            var tools = new List<string>();
            if (root.TryGetProperty("workspaceToolsUsed", out var toolsEl) && toolsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in toolsEl.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var name = item.GetString();
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            tools.Add(name);
                        }
                    }
                }
            }

            return new ParsedMessage
            {
                Text = text,
                UsedWorkspaceData = usedWorkspaceData,
                ResponseMode = string.IsNullOrWhiteSpace(responseMode) ? "general" : responseMode,
                WorkspaceToolsUsed = tools
            };
        }
        catch
        {
            return new ParsedMessage
            {
                Text = text,
                UsedWorkspaceData = false,
                ResponseMode = "general",
                WorkspaceToolsUsed = new List<string>()
            };
        }
    }

    private sealed class ParsedMessage
    {
        public string Text { get; set; } = string.Empty;
        public bool UsedWorkspaceData { get; set; }
        public string ResponseMode { get; set; } = "general";
        public List<string> WorkspaceToolsUsed { get; set; } = new();
    }
}
