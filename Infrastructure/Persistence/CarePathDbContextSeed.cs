using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Platform;
using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.ControlPlane.Identity;
using CarePath.Infrastructure.ControlPlane.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CarePath.Infrastructure.Persistence;

/// <summary>
/// Development-only synthetic seed data for local CarePath databases: tenant users and profiles,
/// their platform identities and memberships in the default organization, and a second
/// organization (<c>helpinghands</c>) sharing the development database so two-tenant behavior can
/// be exercised locally (D-S8-9). Never runs outside Development.
/// </summary>
public static class CarePathDbContextSeed
{
    private const string SeedPasswordConfigurationKey = "SeedData:DefaultPassword";
    private const string DefaultOrganizationSlugKey = "ControlPlane:DefaultOrganization:Slug";

    /// <summary>Slug of the second development organization.</summary>
    public const string SecondOrganizationSlug = "helpinghands";

    private static readonly Guid AdminUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CaregiverUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ClientUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid CoordinatorUserId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid ClinicianUserId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid CaregiverProfileId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ClientProfileId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid SecondOrganizationId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    /// <summary>
    /// Seeds synthetic development data. No operation is performed outside Development.
    /// </summary>
    /// <param name="context">Tenant EF Core context.</param>
    /// <param name="controlPlane">Control-plane EF Core context.</param>
    /// <param name="userManager">Platform identity manager.</param>
    /// <param name="configuration">Configuration holding the development-only seed password and default organization slug.</param>
    /// <param name="environment">Current host environment.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    public static async Task SeedAsync(
        CarePathDbContext context,
        ControlPlaneDbContext controlPlane,
        UserManager<PlatformUser> userManager,
        IConfiguration configuration,
        IHostEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(controlPlane);
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        if (!environment.IsDevelopment())
        {
            return;
        }

        var seedPassword = configuration[SeedPasswordConfigurationKey];
        if (string.IsNullOrWhiteSpace(seedPassword))
        {
            throw new InvalidOperationException($"Development seed password must be configured at '{SeedPasswordConfigurationKey}' using user secrets or an environment variable.");
        }

        var defaultOrganization = await FindDefaultOrganizationAsync(controlPlane, configuration, cancellationToken);

        var admin = await EnsureDomainUserAsync(context, AdminUserId, "Demo", "Administrator", "admin@carepath.local", "555-0100", UserRole.Admin, cancellationToken);
        var caregiverUser = await EnsureDomainUserAsync(context, CaregiverUserId, "Casey", "Caregiver", "caregiver@carepath.local", "555-0101", UserRole.Caregiver, cancellationToken);
        var clientUser = await EnsureDomainUserAsync(context, ClientUserId, "Riley", "Client", "client@carepath.local", "555-0102", UserRole.Client, cancellationToken);
        var coordinatorUser = await EnsureDomainUserAsync(context, CoordinatorUserId, "Jordan", "Coordinator", "coordinator@carepath.local", "555-0103", UserRole.Coordinator, cancellationToken);
        var clinicianUser = await EnsureDomainUserAsync(context, ClinicianUserId, "Taylor", "Clinician", "clinician@carepath.local", "555-0104", UserRole.Clinician, cancellationToken);

        foreach (var user in new[] { admin, caregiverUser, clientUser, coordinatorUser, clinicianUser })
        {
            await EnsurePlatformUserAsync(userManager, user, seedPassword);
            await EnsureMembershipAsync(controlPlane, defaultOrganization.Id, user, user.Role, cancellationToken);
            user.PlatformUserId = user.Id;
        }

        var secondOrganization = await EnsureSecondOrganizationAsync(controlPlane, defaultOrganization, cancellationToken);
        await EnsureMembershipAsync(controlPlane, secondOrganization.Id, caregiverUser, UserRole.Caregiver, cancellationToken);

        await EnsureCaregiverAsync(context, caregiverUser.Id, cancellationToken);
        await EnsureClientAsync(context, clientUser.Id, cancellationToken);

        await controlPlane.SaveChangesAsync(cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Organization> FindDefaultOrganizationAsync(ControlPlaneDbContext controlPlane, IConfiguration configuration, CancellationToken cancellationToken)
    {
        var slug = configuration[DefaultOrganizationSlugKey];
        var organization = string.IsNullOrWhiteSpace(slug)
            ? await controlPlane.Organizations.OrderBy(candidate => candidate.CreatedAt).FirstOrDefaultAsync(cancellationToken)
            : await controlPlane.Organizations.FirstOrDefaultAsync(candidate => candidate.Slug == slug, cancellationToken);

        return organization
            ?? throw new InvalidOperationException("The development seed requires the default organization; run the control-plane bootstrap first.");
    }

    private static async Task<User> EnsureDomainUserAsync(
        CarePathDbContext context,
        Guid id,
        string firstName,
        string lastName,
        string email,
        string phoneNumber,
        UserRole role,
        CancellationToken cancellationToken)
    {
        var existing = await context.DomainUsers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(user => user.Email == email, cancellationToken);

        if (existing is not null)
        {
            existing.IsDeleted = false;
            existing.IsActive = true;
            await context.SaveChangesAsync(cancellationToken);
            return existing;
        }

        var user = new User
        {
            Id = id,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PhoneNumber = phoneNumber,
            Address = "100 Synthetic Demo Lane",
            City = "Demo City",
            State = "Maryland",
            ZipCode = "00000",
            Role = role,
            IsActive = true,
        };

        await context.DomainUsers.AddAsync(user, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return user;
    }

    private static async Task EnsurePlatformUserAsync(UserManager<PlatformUser> userManager, User domainUser, string seedPassword)
    {
        var platformUser = await userManager.FindByEmailAsync(domainUser.Email);
        if (platformUser is not null)
        {
            return;
        }

        platformUser = new PlatformUser
        {
            Id = domainUser.Id,
            UserName = domainUser.Email,
            Email = domainUser.Email,
            EmailConfirmed = true,
            PhoneNumber = domainUser.PhoneNumber,
            PhoneNumberConfirmed = true,
            DisplayName = domainUser.FullName,
        };

        var createResult = await userManager.CreateAsync(platformUser, seedPassword);
        EnsureSucceeded(createResult, $"create platform identity for synthetic user {domainUser.Id:N}");
    }

    private static async Task EnsureMembershipAsync(ControlPlaneDbContext controlPlane, Guid organizationId, User user, UserRole role, CancellationToken cancellationToken)
    {
        var membership = await controlPlane.OrganizationMemberships
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.PlatformUserId == user.Id, cancellationToken);

        if (membership is not null)
        {
            membership.IsDeleted = false;
            membership.Status = MembershipStatus.Active;
            return;
        }

        controlPlane.OrganizationMemberships.Add(new OrganizationMembership
        {
            OrganizationId = organizationId,
            PlatformUserId = user.Id,
            TenantUserId = user.Id,
            Role = role,
            Status = MembershipStatus.Active,
        });
    }

    private static async Task<Organization> EnsureSecondOrganizationAsync(ControlPlaneDbContext controlPlane, Organization defaultOrganization, CancellationToken cancellationToken)
    {
        var existing = await controlPlane.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(candidate => candidate.Slug == SecondOrganizationSlug, cancellationToken);
        if (existing is not null)
        {
            existing.IsDeleted = false;
            return existing;
        }

        var organization = new Organization
        {
            Id = SecondOrganizationId,
            LegalName = "Helping Hands Home Care (synthetic)",
            DisplayName = "Helping Hands",
            Slug = SecondOrganizationSlug,
            Status = OrganizationStatus.Active,
            ProvisioningState = ProvisioningState.Provisioned,
            DefaultTimeZone = defaultOrganization.DefaultTimeZone,
            DataRegion = defaultOrganization.DataRegion,
        };
        controlPlane.Organizations.Add(organization);
        controlPlane.OrganizationDomains.Add(new OrganizationDomain
        {
            OrganizationId = organization.Id,
            HostName = $"{SecondOrganizationSlug}.localhost",
            IsPrimary = true,
            VerificationStatus = DomainVerificationStatus.Verified,
        });
        controlPlane.OrganizationBrandings.Add(new OrganizationBranding
        {
            OrganizationId = organization.Id,
            PrimaryColorToken = BrandingColorToken.Teal700,
            AccentColorToken = BrandingColorToken.Green,
            SupportEmail = "support@helpinghands.local",
        });

        // Development only: the second organization shares the development database until CP-06
        // provisions real data planes. Copies the default organization's locator verbatim.
        var defaultDataPlane = await controlPlane.OrganizationDataPlanes
            .FirstOrDefaultAsync(candidate => candidate.OrganizationId == defaultOrganization.Id, cancellationToken);
        controlPlane.OrganizationDataPlanes.Add(new OrganizationDataPlane
        {
            OrganizationId = organization.Id,
            Provider = defaultDataPlane?.Provider ?? DataPlaneProvider.SqlServer,
            SecretReference = defaultDataPlane?.SecretReference ?? "ConnectionStrings:DefaultConnection",
            RegisteredAtUtc = DateTime.UtcNow,
        });

        return organization;
    }

    private static async Task EnsureCaregiverAsync(CarePathDbContext context, Guid userId, CancellationToken cancellationToken)
    {
        var caregiver = await context.Caregivers
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(caregiver => caregiver.UserId == userId, cancellationToken);

        if (caregiver is not null)
        {
            caregiver.IsDeleted = false;
            return;
        }

        await context.Caregivers.AddAsync(new Caregiver
        {
            Id = CaregiverProfileId,
            UserId = userId,
            EmploymentType = EmploymentType.W2Employee,
            HourlyPayRate = 22.50m,
            HireDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            HasDementiaCare = true,
            HasMobilityAssistance = true,
            HasMedicationManagement = true,
            AvailableWeekdays = true,
            AvailableWeekends = true,
            MaxWeeklyHours = 32,
        }, cancellationToken);
    }

    private static async Task EnsureClientAsync(CarePathDbContext context, Guid userId, CancellationToken cancellationToken)
    {
        var client = await context.Clients
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(client => client.UserId == userId, cancellationToken);

        if (client is not null)
        {
            client.IsDeleted = false;
            return;
        }

        await context.Clients.AddAsync(new Client
        {
            Id = ClientProfileId,
            UserId = userId,
            DateOfBirth = new DateTime(1950, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EmergencyContactName = "Synthetic Emergency Contact",
            EmergencyContactPhone = "555-0199",
            EmergencyContactRelationship = "Demo Contact",
            RequiresMobilityAssistance = true,
            RequiresMedicationManagement = true,
            RequiresCompanionship = true,
            SpecialInstructions = "Synthetic demo instruction only. Do not use for real care.",
            MedicalConditions = "Synthetic demo condition only - not real PHI.",
            Allergies = "Synthetic demo allergy only - not real PHI.",
            ServiceType = ServiceType.InHomeCare,
            HourlyBillRate = 42.00m,
            EstimatedWeeklyHours = 20,
            Latitude = 39.083997,
            Longitude = -77.152758,
            LocationNotes = "Synthetic demo location only.",
            InsuranceProvider = "Synthetic Demo Insurance",
            InsurancePolicyNumber = "DEMO-POLICY-0001",
            MedicaidNumber = "DEMO-MEDICAID-0001",
        }, cancellationToken);
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join("; ", result.Errors.Select(error => error.Code));
        throw new InvalidOperationException($"Seed operation failed to {operation}: {errors}");
    }
}
