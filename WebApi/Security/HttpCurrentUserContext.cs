using System.Security.Claims;
using CarePath.Application.Abstractions.Auth;

namespace CarePath.WebApi.Security;

public sealed class HttpCurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor httpContextAccessor;

    public HttpCurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var value = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }

    public string? UserName => User?.Identity?.Name;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public IReadOnlySet<string> Roles => User?.FindAll(ClaimTypes.Role)
        .Select(claim => claim.Value)
        .Where(role => !string.IsNullOrWhiteSpace(role))
        .ToHashSet(StringComparer.Ordinal)
        ?? new HashSet<string>(StringComparer.Ordinal);

    public string? CorrelationId => httpContextAccessor.HttpContext?.TraceIdentifier;

    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;
}
