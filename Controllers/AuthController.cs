using System.Security.Claims;
using Medpointe.Models.Api;
using Medpointe.Models.Auth;
using Medpointe.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Medpointe.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(AuthService authService) : ControllerBase
{
    [Authorize]
    [HttpGet("dashboard-context")]
    public async Task<IActionResult> GetDashboardContext(CancellationToken cancellationToken)
    {
        string? username = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username)) return Unauthorized();

        DashboardContextResponse? context = await authService.GetDashboardContextAsync(username, cancellationToken);
        return context is null ? Unauthorized() : Ok(context);
    }

    // [AllowAnonymous]
    // [HttpPost("register")]
    // public async Task<ActionResult<LoginResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    // {
    //     var response = await authService.RegisterAsync(request, cancellationToken);

    //     if (response is null)
    //     {
    //         return Conflict(new { message = "Username already exists." });
    //     }

    //     return Created(string.Empty, response);
    // }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        LoginResponse? response = await authService.LoginAsync(request, cancellationToken);

        if (response is null)
        {
            return Unauthorized(new ApiError
            {
                Title = "Invalid credentials",
                Message = "The username or password is incorrect.",
                Code = "invalid_credentials"
            });
        }

        return Ok(response);
    }
}
