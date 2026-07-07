using CarePath.Application.Abstractions.Auth;
using CarePath.Contracts.Auth;
using CarePath.Contracts.Common;
using CarePath.WebApi.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ContractUserRole = CarePath.Contracts.Enumerations.UserRole;

namespace CarePath.WebApi.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private const string InvalidCredentialsCode = "auth.invalid_credentials";
    private const string InvalidCredentialsMessage = "Invalid credentials.";

    private readonly IIdentityService identityService;
    private readonly IJwtTokenService jwtTokenService;

    public AuthController(IIdentityService identityService, IJwtTokenService jwtTokenService)
    {
        this.identityService = identityService;
        this.jwtTokenService = jwtTokenService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthTokenResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var user = await identityService.ValidateCredentialsAsync(
            request.Email,
            request.Password,
            cancellationToken);

        if (!user.Succeeded)
        {
            return InvalidCredentials();
        }

        if (!TryGetRole(user, out var role))
        {
            return InvalidCredentials();
        }

        var refreshToken = await identityService.IssueRefreshTokenAsync(user.UserId!.Value, cancellationToken);
        return Ok(await CreateTokenResponseAsync(user, refreshToken, role, cancellationToken));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthTokenResponse>> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var rotation = await identityService.RotateRefreshTokenAsync(request.RefreshToken, cancellationToken);

        if (!rotation.Succeeded || rotation.User is null || rotation.RefreshToken is null)
        {
            return InvalidCredentials();
        }

        if (!TryGetRole(rotation.User, out var role))
        {
            return InvalidCredentials();
        }

        return Ok(await CreateTokenResponseAsync(rotation.User, rotation.RefreshToken, role, cancellationToken));
    }

    private async Task<AuthTokenResponse> CreateTokenResponseAsync(
        IdentityUserResult user,
        string refreshToken,
        ContractUserRole role,
        CancellationToken cancellationToken)
    {
        var jwt = await jwtTokenService.CreateTokenAsync(
            new JwtTokenRequest(
                user.UserId!.Value,
                user.Email ?? string.Empty,
                user.Roles,
                HttpContext.TraceIdentifier),
            cancellationToken);

        return new AuthTokenResponse
        {
            AccessToken = jwt.AccessToken,
            ExpiresAtUtc = jwt.ExpiresAtUtc,
            RefreshToken = refreshToken,
            Role = role,
            DisplayName = user.DisplayName ?? string.Empty,
        };
    }

    private static bool TryGetRole(IdentityUserResult user, out ContractUserRole role)
    {
        role = default;

        if (user.UserId is null || string.IsNullOrWhiteSpace(user.Email) || user.Roles.Count != 1)
        {
            return false;
        }

        return Enum.TryParse(user.Roles.Single(), ignoreCase: false, out role);
    }

    private static UnauthorizedObjectResult InvalidCredentials()
    {
        return new UnauthorizedObjectResult(new ProblemDetailsResponse
        {
            Type = "about:blank",
            Title = "Unauthorized.",
            Status = StatusCodes.Status401Unauthorized,
            Detail = null,
            Instance = null,
            TraceId = null,
            ValidationErrors = [],
            Errors = [new ApiError(InvalidCredentialsCode, InvalidCredentialsMessage)],
        });
    }
}
