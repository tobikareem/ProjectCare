namespace CarePath.Application.Abstractions.Auth;

public interface IIdentityProvisioningService
{
    Task<IdentityProvisioningResult> ProvisionUserAsync(
        IdentityProvisioningRequest request,
        CancellationToken cancellationToken = default);
}
