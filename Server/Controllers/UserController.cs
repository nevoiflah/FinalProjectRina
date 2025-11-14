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

    [HttpGet("{userId}")]
    public IActionResult GetById(string userId)
    {
        var user = _userService.FindById(userId);
        if (user is null)
        {
            return NotFound(new { error = "User not found" });
        }

        return Ok(user);
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

    [HttpPut("{userId}")]
    public IActionResult Update(string userId, [FromBody] UpdateUserRequest? request)
    {
        if (request is null)
        {
            return BadRequest(new { error = "Request body is required" });
        }

        try
        {
            var user = _userService.UpdateProfile(
                userId,
                request.Name,
                request.Organization,
                request.Password);

            if (user is null)
            {
                return NotFound(new { error = "User not found" });
            }

            return Ok(user);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{userId}/admin")]
    public IActionResult ToggleAdmin(string userId, [FromBody] ToggleAdminRequest? request)
    {
        if (request is null)
        {
            return BadRequest(new { error = "Request body is required" });
        }

        try
        {
            var result = _userService.ToggleAdmin(userId, request.IsAdmin);
            if (!result)
            {
                return NotFound(new { error = "User not found" });
            }

            return Ok(new
            {
                message = $"User admin status updated to {request.IsAdmin}",
                isAdmin = request.IsAdmin
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{userId}/status")]
    public IActionResult ToggleStatus(string userId, [FromBody] ToggleStatusRequest? request)
    {
        if (request is null)
        {
            return BadRequest(new { error = "Request body is required" });
        }

        try
        {
            var result = _userService.ToggleStatus(userId, request.IsActive);
            if (!result)
            {
                return NotFound(new { error = "User not found" });
            }

            return Ok(new
            {
                message = $"User account {(request.IsActive ? "activated" : "deactivated")}",
                isActive = request.IsActive
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{userId}")]
    public IActionResult Delete(string userId)
    {
        try
        {
            var result = _userService.DeleteUser(userId);
            if (!result)
            {
                return NotFound(new { error = "User not found" });
            }

            return Ok(new { message = "User deleted successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

// Request/Response Classes
public class RegisterUserRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("org")]
    public string? Organization { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }
}

public class LoginRequest
{
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }
}

public class UpdateUserRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("organization")]
    public string? Organization { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }
}

public class ToggleAdminRequest
{
    [JsonPropertyName("isAdmin")]
    public bool IsAdmin { get; set; }
}

public class ToggleStatusRequest
{
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }
}