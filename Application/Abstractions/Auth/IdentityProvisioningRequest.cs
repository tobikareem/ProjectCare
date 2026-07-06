namespace CarePath.Application.Abstractions.Auth;

public sealed record IdentityProvisioningRequest(
    Guid DomainUserId,
    string Email,
    string PhoneNumber,
    string TemporaryPassword,
    string Role);
