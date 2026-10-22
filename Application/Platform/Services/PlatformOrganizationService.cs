using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Abstractions.Tenancy;
using CarePath.Application.Common.Exceptions;
using CarePath.Application.Common.Paging;
using CarePath.Application.Platform.Mapping;
using CarePath.Application.Platform.Validators;
using CarePath.Contracts.Common;
using CarePath.Contracts.Platform;
using CarePath.Domain.Entities.Platform;
using OrganizationEntity = CarePath.Domain.Entities.Platform.Organization;
using CarePath.Domain.Enumerations;
using CarePath.Domain.Interfaces.Repositories;
using FluentValidation;

namespace CarePath.Application.Platform.Services;

/// <summary>
/// Control-plane organization lifecycle for platform operators. Every mutation writes a platform
/// audit event and invalidates the public branding cache where presentation data changed.
/// </summary>
public sealed class PlatformOrganizationService : IPlatformOrganizationService
{
    private readonly IControlPlaneUnitOfWork _controlPlane;
    private readonly ICurrentUserContext _currentUser;
    private readonly IPlatformAuditLogger _auditLogger;
    private readonly IBrandingCache _brandingCache;
    private readonly IPlatformDomainSettings _platformSettings;
    private readonly IValidator<RegisterOrganizationRequest> _registerValidator;
    private readonly IValidator<UpdateOrganizationRequest> _updateValidator;
    private readonly IValidator<SuspendOrganizationRequest> _suspendValidator;
    private readonly IValidator<AddOrganizationDomainRequest> _addDomainValidator;
    private readonly IValidator<UpdateOrganizationBrandingRequest> _brandingValidator;

    /// <summary>Initializes the service.</summary>
    public PlatformOrganizationService(
        IControlPlaneUnitOfWork controlPlane,
        ICurrentUserContext currentUser,
        IPlatformAuditLogger auditLogger,
        IBrandingCache brandingCache,
        IPlatformDomainSettings platformSettings,
        IValidator<RegisterOrganizationRequest>? registerValidator = null,
        IValidator<UpdateOrganizationRequest>? updateValidator = null,
        IValidator<SuspendOrganizationRequest>? suspendValidator = null,
        IValidator<AddOrganizationDomainRequest>? addDomainValidator = null,
        IValidator<UpdateOrganizationBrandingRequest>? brandingValidator = null)
    {
        _controlPlane = controlPlane;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
        _brandingCache = brandingCache;
        _platformSettings = platformSettings;
        _registerValidator = registerValidator ?? new RegisterOrganizationRequestValidator();
        _updateValidator = updateValidator ?? new UpdateOrganizationRequestValidator();
        _suspendValidator = suspendValidator ?? new SuspendOrganizationRequestValidator();
        _addDomainValidator = addDomainValidator ?? new AddOrganizationDomainRequestValidator();
        _brandingValidator = brandingValidator ?? new UpdateOrganizationBrandingRequestValidator();
    }

    /// <inheritdoc />
    public async Task<OrganizationDto> RegisterAsync(RegisterOrganizationRequest request, CancellationToken cancellationToken = default)
    {
        await _registerValidator.ValidateAndThrowAsync(request, cancellationToken);

        var slug = request.Slug.Trim();
        if (await _controlPlane.Organizations.ExistsAsync(organization => organization.Slug == slug, cancellationToken))
        {
            throw new ResourceConflictException("organization.slug_taken", "Slug is already registered.");
        }

        var primaryHost = HostNameNormalizer.Normalize($"{slug}.{_platformSettings.PlatformBaseDomain}")
            ?? throw new ValidationException("The request is invalid.");
        if (await _controlPlane.Domains.ExistsAsync(domain => domain.HostName == primaryHost, cancellationToken))
        {
            throw new ResourceConflictException("organization.host_taken", "Host name is already registered.");
        }

        var organization = new OrganizationEntity
        {
            LegalName = request.LegalName.Trim(),
            DisplayName = request.DisplayName.Trim(),
            Slug = slug,
            Status = OrganizationStatus.Pending,
            ProvisioningState = ProvisioningState.Registered,
            DefaultTimeZone = request.DefaultTimeZone,
            DataRegion = request.DataRegion.Trim(),
        };
        var domain = new OrganizationDomain
        {
            OrganizationId = organization.Id,
            HostName = primaryHost,
            IsPrimary = true,
            VerificationStatus = DomainVerificationStatus.Verified,
        };

        await _controlPlane.ExecuteInTransactionAsync(async token =>
        {
            await _controlPlane.Organizations.AddAsync(organization, token);
            await _controlPlane.Domains.AddAsync(domain, token);
            await _controlPlane.Brandings.AddAsync(new OrganizationBranding { OrganizationId = organization.Id }, token);
            await _controlPlane.SaveChangesAsync(token);
        }, cancellationToken);

        await AuditAsync(PlatformAuditAction.OrganizationRegistered, organization.Id, nameof(OrganizationEntity), organization.Id, cancellationToken);
        return PlatformContractMapper.ToDto(organization, [domain], null);
    }

    /// <inheritdoc />
    public async Task<PagedResult<OrganizationSummaryDto>> ListAsync(PagedRequest paging, CancellationToken cancellationToken = default)
    {
        var (organizations, total) = await _controlPlane.Organizations.GetPagedAsync(
            organization => true,
            organization => organization.DisplayName,
            organization => organization.Slug,
            paging.PageNumber,
            paging.PageSize,
            cancellationToken);

        var ids = organizations.Select(organization => organization.Id).ToArray();
        var primaryHosts = (await _controlPlane.Domains.FindAsync(domain => ids.Contains(domain.OrganizationId) && domain.IsPrimary, cancellationToken))
            .GroupBy(domain => domain.OrganizationId)
            .ToDictionary(group => group.Key, group => group.First().HostName);

        var items = organizations
            .Select(organization => PlatformContractMapper.ToSummaryDto(organization, primaryHosts.GetValueOrDefault(organization.Id)))
            .ToArray();
        return PagedResultFactory.Create(items, total, paging.PageNumber, paging.PageSize);
    }

    /// <inheritdoc />
    public async Task<OrganizationDto> GetAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var organization = await RequireOrganizationAsync(organizationId, cancellationToken);
        return await ToDtoAsync(organization, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OrganizationDto> UpdateAsync(Guid organizationId, UpdateOrganizationRequest request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var organization = await RequireOrganizationAsync(organizationId, cancellationToken);

        organization.LegalName = request.LegalName.Trim();
        organization.DisplayName = request.DisplayName.Trim();
        organization.DefaultTimeZone = request.DefaultTimeZone;
        organization.DataRegion = request.DataRegion.Trim();
        await _controlPlane.Organizations.UpdateAsync(organization, cancellationToken);
        await _controlPlane.SaveChangesAsync(cancellationToken);

        _brandingCache.Invalidate(organization.Id);
        await AuditAsync(PlatformAuditAction.OrganizationUpdated, organization.Id, nameof(OrganizationEntity), organization.Id, cancellationToken);
        return await ToDtoAsync(organization, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OrganizationDto> SuspendAsync(Guid organizationId, SuspendOrganizationRequest request, CancellationToken cancellationToken = default)
    {
        await _suspendValidator.ValidateAndThrowAsync(request, cancellationToken);
        var organization = await RequireOrganizationAsync(organizationId, cancellationToken);

        if (organization.Status != OrganizationStatus.Active)
        {
            throw new ResourceConflictException("organization.not_active", "Only an active organization can be suspended.");
        }

        organization.Status = OrganizationStatus.Suspended;
        organization.SuspensionReason = request.Reason.Trim();
        organization.SuspendedAtUtc = DateTime.UtcNow;
        await _controlPlane.Organizations.UpdateAsync(organization, cancellationToken);
        await _controlPlane.SaveChangesAsync(cancellationToken);

        _brandingCache.Invalidate(organization.Id);
        await AuditAsync(PlatformAuditAction.OrganizationSuspended, organization.Id, nameof(OrganizationEntity), organization.Id, cancellationToken);
        return await ToDtoAsync(organization, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OrganizationDto> ReactivateAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var organization = await RequireOrganizationAsync(organizationId, cancellationToken);

        if (organization.Status != OrganizationStatus.Suspended)
        {
            throw new ResourceConflictException("organization.not_suspended", "Only a suspended organization can be reactivated.");
        }

        // FR-015: a Pending organization becomes Active only through CP-06 provisioning.
        if (organization.ProvisioningState != ProvisioningState.Provisioned)
        {
            throw new ResourceConflictException("organization.not_provisioned", "The organization has no provisioned data plane.");
        }

        // One operational database until CP-06 (review finding C1): a second Active organization
        // would share PHI with the first, so reactivation is refused unless this deployment is
        // explicitly synthetic. Startup enforces the same rule as a backstop.
        if (!_platformSettings.AllowMultipleActiveOrganizations
            && await _controlPlane.Organizations.ExistsAsync(candidate => candidate.Status == OrganizationStatus.Active && candidate.Id != organization.Id, cancellationToken))
        {
            throw new ResourceConflictException("organization.single_data_plane", "Only one organization can be active until per-organization data planes are routed (CP-06).");
        }

        organization.Status = OrganizationStatus.Active;
        organization.SuspensionReason = null;
        await _controlPlane.Organizations.UpdateAsync(organization, cancellationToken);
        await _controlPlane.SaveChangesAsync(cancellationToken);

        _brandingCache.Invalidate(organization.Id);
        await AuditAsync(PlatformAuditAction.OrganizationReactivated, organization.Id, nameof(OrganizationEntity), organization.Id, cancellationToken);
        return await ToDtoAsync(organization, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OrganizationDomainDto>> GetDomainsAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        await RequireOrganizationAsync(organizationId, cancellationToken);
        var domains = await _controlPlane.Domains.FindAsync(domain => domain.OrganizationId == organizationId, cancellationToken);
        return domains.OrderByDescending(domain => domain.IsPrimary).ThenBy(domain => domain.HostName, StringComparer.Ordinal).Select(PlatformContractMapper.ToDomainDto).ToArray();
    }

    /// <inheritdoc />
    public async Task<OrganizationDomainDto> AddDomainAsync(Guid organizationId, AddOrganizationDomainRequest request, CancellationToken cancellationToken = default)
    {
        await _addDomainValidator.ValidateAndThrowAsync(request, cancellationToken);
        var organization = await RequireOrganizationAsync(organizationId, cancellationToken);
        var hostName = HostNameNormalizer.Normalize(request.HostName) ?? throw new ValidationException("The request is invalid.");
        var isSystemSubdomain = hostName == HostNameNormalizer.Normalize($"{organization.Slug}.{_platformSettings.PlatformBaseDomain}");

        // HOST TAKEOVER GUARD: the platform hosts, the base domain and any other label under it
        // (another organization's future subdomain, platform./api./admin. …) and IP literals are
        // never organization hosts; only the organization's own system subdomain is allowed there.
        if (!isSystemSubdomain && IsReservedHost(hostName))
        {
            throw new ValidationException("The request is invalid.");
        }

        // Only a verified host may be primary; custom hosts start Pending (verification is CP-06).
        if (request.IsPrimary && !isSystemSubdomain)
        {
            throw new ResourceConflictException("organization.domain_unverified", "Only a verified host can be primary.");
        }

        if (await _controlPlane.Domains.ExistsAsync(domain => domain.HostName == hostName, cancellationToken))
        {
            throw new ResourceConflictException("organization.host_taken", "Host name is already registered.");
        }

        var domain = new OrganizationDomain
        {
            OrganizationId = organization.Id,
            HostName = hostName,
            IsPrimary = request.IsPrimary,
            VerificationStatus = isSystemSubdomain ? DomainVerificationStatus.Verified : DomainVerificationStatus.Pending,
        };

        await _controlPlane.ExecuteInTransactionAsync(async token =>
        {
            if (request.IsPrimary)
            {
                foreach (var existing in await _controlPlane.Domains.FindAsync(candidate => candidate.OrganizationId == organization.Id && candidate.IsPrimary, token))
                {
                    existing.IsPrimary = false;
                    await _controlPlane.Domains.UpdateAsync(existing, token);
                }
            }

            await _controlPlane.Domains.AddAsync(domain, token);
            await _controlPlane.SaveChangesAsync(token);
        }, cancellationToken);

        await AuditAsync(PlatformAuditAction.DomainAdded, organization.Id, nameof(OrganizationDomain), domain.Id, cancellationToken);
        return PlatformContractMapper.ToDomainDto(domain);
    }

    /// <inheritdoc />
    public async Task RemoveDomainAsync(Guid organizationId, Guid domainId, CancellationToken cancellationToken = default)
    {
        await RequireOrganizationAsync(organizationId, cancellationToken);
        var domains = await _controlPlane.Domains.FindAsync(domain => domain.OrganizationId == organizationId, cancellationToken);
        var domain = domains.FirstOrDefault(candidate => candidate.Id == domainId)
            ?? throw new ResourceNotFoundException(isPhiResource: false);

        if (domain.VerificationStatus == DomainVerificationStatus.Verified
            && domains.Count(candidate => candidate.VerificationStatus == DomainVerificationStatus.Verified) <= 1)
        {
            throw new ResourceConflictException("organization.last_verified_domain", "The last verified host cannot be removed.");
        }

        if (domain.IsPrimary)
        {
            throw new ResourceConflictException("organization.primary_domain", "Make another host primary before removing this one.");
        }

        await _controlPlane.Domains.DeleteAsync(domain, cancellationToken);
        await _controlPlane.SaveChangesAsync(cancellationToken);
        await AuditAsync(PlatformAuditAction.DomainRemoved, organizationId, nameof(OrganizationDomain), domain.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OrganizationBrandingDetailDto> GetBrandingAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        await RequireOrganizationAsync(organizationId, cancellationToken);

        // A read never writes: a missing row renders as the defaults and is created on first update.
        var branding = (await _controlPlane.Brandings.FindAsync(candidate => candidate.OrganizationId == organizationId, cancellationToken)).FirstOrDefault()
            ?? new OrganizationBranding { OrganizationId = organizationId };
        return PlatformContractMapper.ToBrandingDto(branding);
    }

    /// <inheritdoc />
    public async Task<OrganizationBrandingDetailDto> UpdateBrandingAsync(Guid organizationId, UpdateOrganizationBrandingRequest request, CancellationToken cancellationToken = default)
    {
        await _brandingValidator.ValidateAndThrowAsync(request, cancellationToken);
        await RequireOrganizationAsync(organizationId, cancellationToken);
        var branding = await GetOrCreateBrandingAsync(organizationId, cancellationToken);

        branding.LogoStorageKey = string.IsNullOrWhiteSpace(request.LogoStorageKey) ? null : request.LogoStorageKey.Trim();
        branding.PrimaryColorToken = (BrandingColorToken)request.PrimaryColorToken;
        branding.AccentColorToken = (BrandingColorToken)request.AccentColorToken;
        branding.SupportEmail = string.IsNullOrWhiteSpace(request.SupportEmail) ? null : request.SupportEmail.Trim();
        branding.SupportPhone = string.IsNullOrWhiteSpace(request.SupportPhone) ? null : request.SupportPhone.Trim();
        await _controlPlane.Brandings.UpdateAsync(branding, cancellationToken);
        await _controlPlane.SaveChangesAsync(cancellationToken);

        _brandingCache.Invalidate(organizationId);
        await AuditAsync(PlatformAuditAction.BrandingUpdated, organizationId, nameof(OrganizationBranding), branding.Id, cancellationToken);
        return PlatformContractMapper.ToBrandingDto(branding);
    }

    private bool IsReservedHost(string hostName)
    {
        if (_platformSettings.PlatformHostNames.Contains(hostName))
        {
            return true;
        }

        var baseDomain = HostNameNormalizer.Normalize(_platformSettings.PlatformBaseDomain);
        if (baseDomain is not null && (hostName == baseDomain || hostName.EndsWith("." + baseDomain, StringComparison.Ordinal)))
        {
            return true;
        }

        return hostName.Split('.').All(label => label.All(char.IsAsciiDigit));
    }

    private async Task<OrganizationEntity> RequireOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await _controlPlane.Organizations.GetByIdAsync(organizationId, cancellationToken)
            ?? throw new ResourceNotFoundException(isPhiResource: false);

    private async Task<OrganizationBranding> GetOrCreateBrandingAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var existing = (await _controlPlane.Brandings.FindAsync(branding => branding.OrganizationId == organizationId, cancellationToken)).FirstOrDefault();
        if (existing is not null)
        {
            return existing;
        }

        var branding = new OrganizationBranding { OrganizationId = organizationId };
        await _controlPlane.Brandings.AddAsync(branding, cancellationToken);
        await _controlPlane.SaveChangesAsync(cancellationToken);
        return branding;
    }

    private async Task<OrganizationDto> ToDtoAsync(OrganizationEntity organization, CancellationToken cancellationToken)
    {
        var domains = await _controlPlane.Domains.FindAsync(domain => domain.OrganizationId == organization.Id, cancellationToken);
        var dataPlane = (await _controlPlane.DataPlanes.FindAsync(plane => plane.OrganizationId == organization.Id, cancellationToken)).FirstOrDefault();
        return PlatformContractMapper.ToDto(organization, domains, dataPlane);
    }

    private Task AuditAsync(PlatformAuditAction action, Guid organizationId, string entityType, Guid entityId, CancellationToken cancellationToken) =>
        _auditLogger.LogAsync(
            new PlatformAuditEntry(_currentUser.UserId, organizationId, action, entityType, entityId, _currentUser.CorrelationId),
            cancellationToken);
}
