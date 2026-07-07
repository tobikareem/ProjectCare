namespace CarePath.Application.Abstractions.Auth;

public sealed record RefreshTokenRotationResult(
    bool Succeeded,
    IdentityUserResult? User,
    string? RefreshToken,
    string? FailureCode)
{
    public static RefreshTokenRotationResult Failed(string failureCode) =>
        new(false, null, null, failureCode);
}
