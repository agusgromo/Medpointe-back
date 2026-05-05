using Medpointe.Models.Auth;
using Medpointe.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Medpointe.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(AuthService authService) : ControllerBase
{
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
            return Unauthorized(new { message = "Invalid username or password." });
        }

        return Ok(response);
    }
}
