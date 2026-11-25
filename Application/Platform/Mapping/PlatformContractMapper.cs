using CarePath.Contracts.Platform;
using CarePath.Domain.Entities.Platform;
using OrganizationEntity = CarePath.Domain.Entities.Platform.Organization;
using ContractDomainVerificationStatus = CarePath.Contracts.Enumerations.DomainVerificationStatus;
using ContractOrganizationStatus = CarePath.Contracts.Enumerations.OrganizationStatus;
using ContractProvisioningState = CarePath.Contracts.Enumerations.ProvisioningState;
using ContractColorToken = CarePath.Contracts.Enumerations.BrandingColorToken;

namespace CarePath.Application.Platform.Mapping;

/// <summary>Domain → Contracts mapping for platform-operator views. Never exposes secret references.</summary>
public static class PlatformContractMapper
{
    /// <summary>Maps a list row.</summary>
    public static OrganizationSummaryDto ToSummaryDto(OrganizationEntity organization, string? primaryHostName) => new()
    {
        Id = organization.Id,
        DisplayName = organization.DisplayName,
        Slug = organization.Slug,
        Status = (ContractOrganizationStatus)organization.Status,
        ProvisioningState = (ContractProvisioningState)organization.ProvisioningState,
        PrimaryHostName = primaryHostName,
        CreatedAtUtc = organization.CreatedAt,
    };

    /// <summary>Maps the detail view.</summary>
    public static OrganizationDto ToDto(OrganizationEntity organization, IReadOnlyList<OrganizationDomain> domains, OrganizationDataPlane? dataPlane) => new()
    {
        Id = organization.Id,
        LegalName = organization.LegalName,
        DisplayName = organization.DisplayName,
        Slug = organization.Slug,
        Status = (ContractOrganizationStatus)organization.Status,
        ProvisioningState = (ContractProvisioningState)organization.ProvisioningState,
        DefaultTimeZone = organization.DefaultTimeZone,
        DataRegion = organization.DataRegion,
        SuspensionReason = organization.SuspensionReason,
        SuspendedAtUtc = organization.SuspendedAtUtc,
        Domains = domains.OrderByDescending(domain => domain.IsPrimary).ThenBy(domain => domain.HostName, StringComparer.Ordinal).Select(ToDomainDto).ToArray(),
        DataPlane = dataPlane is null
            ? null
            : new OrganizationDataPlaneDto
            {
                Provider = dataPlane.Provider.ToString(),
                SchemaVersion = dataPlane.SchemaVersion,
                RegisteredAtUtc = dataPlane.RegisteredAtUtc,
            },
        CreatedAtUtc = organization.CreatedAt,
    };

    /// <summary>Maps a domain row.</summary>
    public static OrganizationDomainDto ToDomainDto(OrganizationDomain domain) => new()
    {
        Id = domain.Id,
        HostName = domain.HostName,
        IsPrimary = domain.IsPrimary,
        VerificationStatus = (ContractDomainVerificationStatus)domain.VerificationStatus,
    };

    /// <summary>Maps branding for the operator view.</summary>
    public static OrganizationBrandingDetailDto ToBrandingDto(OrganizationBranding branding) => new()
    {
        LogoStorageKey = branding.LogoStorageKey,
        PrimaryColorToken = (ContractColorToken)branding.PrimaryColorToken,
        AccentColorToken = (ContractColorToken)branding.AccentColorToken,
        SupportEmail = branding.SupportEmail,
        SupportPhone = branding.SupportPhone,
    };
}
