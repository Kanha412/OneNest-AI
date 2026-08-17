using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneNest.Application.DTOs.Contact;
using OneNest.Application.Interfaces.Services;

namespace OneNest.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ContactController : ControllerBase
{
    private readonly IContactService _contactService;

    public ContactController(IContactService contactService)
    {
        _contactService = contactService;
    }

    [HttpPost]
    public async Task<ActionResult<ContactMessageResponse>> Create(CreateContactRequest request)
    {
        var message = await _contactService.CreateAsync(request);
        return Ok(message);
    }

    [HttpGet("my")]
    public async Task<ActionResult<List<ContactMessageResponse>>> GetMyMessages()
    {
        var messages = await _contactService.GetMyMessagesAsync();
        return Ok(messages);
    }
}
