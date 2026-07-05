namespace CarePath.Application.Abstractions.Auth;

public sealed record IdentityProvisioningResult(
    bool Succeeded,
    string? ErrorCode = null)
{
    public static IdentityProvisioningResult Success() => new(true);

    public static IdentityProvisioningResult Failed(string errorCode) => new(false, errorCode);
}
