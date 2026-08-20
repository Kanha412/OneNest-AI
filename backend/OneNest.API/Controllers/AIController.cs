using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneNest.Application.DTOs.AI;
using OneNest.Application.DTOs.Rag;
using OneNest.Application.Interfaces.Security;
using OneNest.Application.Interfaces.Services;

namespace OneNest.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class AIController : ControllerBase
{
    private readonly IAIService _aiService;
    private readonly IAIConversationService _conversationService;
    private readonly IRagService _ragService;
    private readonly ICurrentUserService _currentUserService;

    public AIController(
        IAIService aiService,
        IAIConversationService conversationService,
        IRagService ragService,
        ICurrentUserService currentUserService)
    {
        _aiService = aiService;
        _conversationService = conversationService;
        _ragService = ragService;
        _currentUserService = currentUserService;
    }

    [HttpGet("conversations")]
    public async Task<ActionResult<List<ConversationListResponse>>> GetConversations(
        [FromQuery] bool includeArchived = false,
        [FromQuery] string? search = null)
    {
        var items = await _conversationService.GetConversationsAsync(includeArchived, search);
        return Ok(items);
    }

    [HttpGet("conversations/{id:guid}")]
    public async Task<ActionResult<ConversationResponse>> GetConversation(Guid id)
    {
        var conversation = await _conversationService.GetConversationAsync(id);
        if (conversation is null)
            return NotFound();

        return Ok(conversation);
    }

    [HttpPost("conversations")]
    public async Task<ActionResult<ConversationResponse>> CreateConversation(CreateConversationRequest request)
    {
        var conversation = await _conversationService.CreateConversationAsync(request);
        return Ok(conversation);
    }

    [HttpPut("conversations/{id:guid}/rename")]
    public async Task<ActionResult<ConversationResponse>> RenameConversation(Guid id, RenameConversationRequest request)
    {
        try
        {
            var conversation = await _conversationService.RenameConversationAsync(id, request);
            if (conversation is null)
                return NotFound();

            return Ok(conversation);
        }
        catch (InvalidOperationException ex)
        {
            return MapInvalidOperation(ex);
        }
    }

    [HttpDelete("conversations/{id:guid}")]
    public async Task<IActionResult> DeleteConversation(Guid id)
    {
        var deleted = await _conversationService.DeleteConversationAsync(id);
        if (!deleted)
            return NotFound();

        return NoContent();
    }

    [HttpPost("conversations/{id:guid}/archive")]
    public async Task<IActionResult> ArchiveConversation(Guid id)
    {
        var archived = await _conversationService.ArchiveConversationAsync(id);
        if (!archived)
            return NotFound();

        return NoContent();
    }

    [HttpPost("conversations/{id:guid}/unarchive")]
    public async Task<IActionResult> UnarchiveConversation(Guid id)
    {
        var restored = await _conversationService.UnarchiveConversationAsync(id);
        if (!restored)
            return NotFound();

        return NoContent();
    }

    [HttpPost("conversations/{id:guid}/messages")]
    public async Task<ActionResult<ChatResponse>> SendMessage(Guid id, SendMessageRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _conversationService.SendMessageAsync(id, request, cancellationToken);
            if (response is null)
                return NotFound();

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return MapInvalidOperation(ex);
        }
    }

    [HttpPost("chat")]
    public async Task<ActionResult<ChatResponse>> Chat(ChatRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _aiService.ChatAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return MapInvalidOperation(ex);
        }
    }

    // ── Phase 9 — RAG ─────────────────────────────────────────────────────────

    /// <summary>
    /// Answers a natural-language question by retrieving the most relevant
    /// notes and documents from the user's personal content and grounding a
    /// Gemini response in them.
    ///
    /// This is an explicit opt-in endpoint — standard conversation and semantic
    /// search are unaffected.  The UserId is always resolved from the JWT;
    /// it cannot be supplied by the client.
    /// </summary>
    [HttpPost("rag")]
    public async Task<ActionResult<RagResponse>> Ask(
        [FromBody] RagRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var response = await _ragService.AskAsync(userId, request, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return MapInvalidOperation(ex);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private ActionResult MapInvalidOperation(InvalidOperationException ex)
    {
        if (ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, ex.Message);
        }

        return BadRequest(ex.Message);
    }
}
