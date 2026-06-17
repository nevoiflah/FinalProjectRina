using FinalProjectRina.Server.BL;
using Microsoft.AspNetCore.Mvc;

namespace FinalProjectRina.Server.Controllers;

[ApiController]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly IUserService _userService;
    private readonly ILearningService _learningService;

    public ChatController(IChatService chatService, IUserService userService, ILearningService learningService)
    {
        _chatService = chatService;
        _userService = userService;
        _learningService = learningService;
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
            var history = (request.History ?? new List<ChatTurnDto>())
                .Where(t => !string.IsNullOrWhiteSpace(t.Role) && !string.IsNullOrWhiteSpace(t.Content))
                .Select(t => new FinalProjectRina.Server.DAL.ChatTurn(t.Role!, t.Content!))
                .ToList();

            var reply = await _chatService.GenerateReplyAsync(
                request.Message, request.UserId, history, request.Language);
            return Ok(new { reply });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
    [HttpPost("end")]
    public async Task<IActionResult> EndSession([FromBody] ChatRequest? request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.UserId))
        {
            return BadRequest(new { error = "UserId is required" });
        }
        
        await _chatService.EndSessionAsync(request.UserId);
        return Ok(new { message = "Session ended" });
    }

    [HttpPost("feedback")]
    public async Task<IActionResult> Feedback([FromBody] FeedbackRequest? request)
    {
        if (request is null
            || string.IsNullOrWhiteSpace(request.UserId)
            || string.IsNullOrWhiteSpace(request.Question)
            || string.IsNullOrWhiteSpace(request.Answer)
            || string.IsNullOrWhiteSpace(request.Rating))
        {
            return BadRequest(new { error = "userId, question, answer and rating are required" });
        }

        var user = _userService.FindById(request.UserId);
        if (user is null || !user.IsActive)
        {
            return Unauthorized(new { error = "You must be logged in to send feedback." });
        }

        await _learningService.HandleFeedbackAsync(
            request.UserId!, request.Question!.Trim(), request.Answer!.Trim(), request.Rating!);
        return Ok(new { message = "Thanks for the feedback" });
    }
}

/// <summary>One prior conversation turn sent by the client. Role is "user" or "assistant".</summary>
public record ChatTurnDto(string? Role, string? Content);

public record ChatRequest(
    string? Message,
    string? UserId,
    List<ChatTurnDto>? History = null,
    string? Language = null);

public record FeedbackRequest(string? UserId, string? Question, string? Answer, string? Rating);
