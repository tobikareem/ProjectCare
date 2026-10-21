namespace CarePath.Application.Abstractions.Auth;

/// <summary>
/// Request to create a platform identity for a tenant user and its membership in the current
/// organization. <paramref name="UserId"/> becomes the platform user id (D-CP04-02).
/// </summary>
/// <param name="UserId">Tenant <c>User.Id</c>, reused as the platform user id.</param>
/// <param name="Email">Login email.</param>
/// <param name="PhoneNumber">Contact phone.</param>
/// <param name="DisplayName">Display name; staff or client name only, never clinical data.</param>
/// <param name="TemporaryPassword">Initial password; never logged.</param>
/// <param name="Role">Tenant role name for the membership.</param>
public sealed record IdentityProvisioningRequest(
    Guid UserId,
    string Email,
    string PhoneNumber,
    string DisplayName,
    string TemporaryPassword,
    string Role);
