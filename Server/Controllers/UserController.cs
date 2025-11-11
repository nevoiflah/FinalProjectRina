using FinalProjectRina.Server.BL;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace FinalProjectRina.Server.Controllers;

[ApiController]
[Route("api/user")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public IActionResult Get([FromQuery] string? email)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            var found = _userService.FindByEmail(email);
            if (found is null)
            {
                return NotFound(new { error = "User not found" });
            }

            return Ok(found);
        }

        return Ok(_userService.GetUsers());
    }

    [HttpPost("register")]
    public IActionResult Register([FromBody] RegisterUserRequest? request)
    {
        try
        {
            var user = _userService.Register(
                request?.Name,
                request?.Email,
                request?.Organization,
                request?.Password);
            return Ok(user);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest? request)
    {
        if (request is null)
        {
            return BadRequest(new { error = "Request body is required" });
        }

        var user = _userService.Authenticate(request.Email, request.Password);
        if (user is null)
        {
            return Unauthorized(new { error = "Invalid email or password" });
        }

        return Ok(user);
    }
}

public record RegisterUserRequest(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("org")] string? Organization,
    [property: JsonPropertyName("password")] string? Password);

public record LoginRequest(
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("password")] string? Password);
