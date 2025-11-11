using FinalProjectRina.Server.BL;
using Microsoft.AspNetCore.Mvc;

namespace FinalProjectRina.Server.Controllers;

[ApiController]
[Route("api/tts")]
public class TtsController : ControllerBase
{
    private readonly ISpeechService _speechService;

    public TtsController(ISpeechService speechService)
    {
        _speechService = speechService;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] TtsRequest? request)
    {
        try
        {
            var result = await _speechService.SynthesizeAsync(request?.Text);
            return File(result.Buffer, result.MimeType);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public record TtsRequest(string? Text);
