using FinalProjectRina.Server.BL;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FinalProjectRina.Server.Controllers;

[ApiController]
[Route("api/stt")]
public class SttController : ControllerBase
{
    private readonly ISpeechService _speechService;

    public SttController(ISpeechService speechService)
    {
        _speechService = speechService;
    }

    [HttpPost]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> Post([FromForm(Name = "audio")] IFormFile? audio, [FromForm(Name = "language")] string language = "he")
    {
        try
        {
            var transcript = await _speechService.TranscribeAsync(audio, language);
            return Ok(new { transcript });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
