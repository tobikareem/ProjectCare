namespace CarePath.Application.Abstractions.Auth;

public interface ICurrentUserContext
{
    Guid? UserId { get; }

    string? UserName { get; }

    bool IsAuthenticated { get; }

    IReadOnlySet<string> Roles { get; }

    string? CorrelationId { get; }
}