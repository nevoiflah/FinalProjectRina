using FinalProjectRina.Server.BL;
using Microsoft.AspNetCore.Mvc;

namespace FinalProjectRina.Server.Controllers;

[ApiController]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly IUserService _userService;

    public ChatController(IChatService chatService, IUserService userService)
    {
        _chatService = chatService;
        _userService = userService;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ChatRequest? request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Message is required" });
        }

        var user = _userService.FindById(request.UserId);
        if (user is null || !user.IsActive)
        {
            return Unauthorized(new { error = "You must be logged in to start a chat." });
        }

        try
        {
            var reply = await _chatService.GenerateReplyAsync(request.Message);
            return Ok(new { reply });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public record ChatRequest(string? Message, string? UserId);
