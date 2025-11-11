using FinalProjectRina.Server.BL;
using Microsoft.AspNetCore.Mvc;

namespace FinalProjectRina.Server.Controllers;

[ApiController]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ChatRequest? request)
    {
        try
        {
            var reply = await _chatService.GenerateReplyAsync(request?.Message);
            return Ok(new { reply });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public record ChatRequest(string? Message);
