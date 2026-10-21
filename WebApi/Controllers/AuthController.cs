using CarePath.Application.Auth.Services;
using CarePath.Contracts.Auth;
using CarePath.WebApi.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarePath.WebApi.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ILoginService loginService;

    public AuthController(ILoginService loginService)
    {
        this.loginService = loginService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthTokenResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await loginService.LoginAsync(request.Email, request.Password, HttpContext.TraceIdentifier, cancellationToken);
        return result.Succeeded ? Ok(result.Token) : InvalidCredentials();
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthTokenResponse>> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var result = await loginService.RefreshAsync(request.RefreshToken, HttpContext.TraceIdentifier, cancellationToken);
        return result.Succeeded ? Ok(result.Token) : InvalidCredentials();
    }

    // One body for every failure mode, shared with host resolution and the bearer challenge.
    private static ActionResult InvalidCredentials() => UnauthorizedResponseWriter.Result();
}
