using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CarePath.Infrastructure.Persistence;

/// <summary>
/// Development-only synthetic seed data for local CarePath databases.
/// </summary>
public static class CarePathDbContextSeed
{
    private const string SeedPasswordConfigurationKey = "SeedData:DefaultPassword";

    private static readonly Guid AdminUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CaregiverUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ClientUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid CaregiverProfileId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ClientProfileId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    /// <summary>
    /// Seeds synthetic development data. No operation is performed outside Development.
    /// </summary>
    /// <param name="context">CarePath EF Core context.</param>
    /// <param name="userManager">ASP.NET Core Identity user manager.</param>
    /// <param name="roleManager">ASP.NET Core Identity role manager.</param>
    /// <param name="configuration">Application configuration containing development-only seed settings.</param>
    /// <param name="environment">Current host environment.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    public static async Task SeedAsync(
        CarePathDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IConfiguration configuration,
        IHostEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(roleManager);
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

        await EnsureRolesAsync(roleManager);

        var admin = await EnsureDomainUserAsync(
            context,
            AdminUserId,
            firstName: "Demo",
            lastName: "Administrator",
            email: "admin@carepath.local",
            phoneNumber: "555-0100",
            role: UserRole.Admin,
            cancellationToken);

        var caregiverUser = await EnsureDomainUserAsync(
            context,
            CaregiverUserId,
            firstName: "Casey",
            lastName: "Caregiver",
            email: "caregiver@carepath.local",
            phoneNumber: "555-0101",
            role: UserRole.Caregiver,
            cancellationToken);

        var clientUser = await EnsureDomainUserAsync(
            context,
            ClientUserId,
            firstName: "Riley",
            lastName: "Client",
            email: "client@carepath.local",
            phoneNumber: "555-0102",
            role: UserRole.Client,
            cancellationToken);

        await EnsureIdentityUserAsync(userManager, admin, UserRole.Admin, seedPassword);
        await EnsureIdentityUserAsync(userManager, caregiverUser, UserRole.Caregiver, seedPassword);
        await EnsureIdentityUserAsync(userManager, clientUser, UserRole.Client, seedPassword);

        await EnsureCaregiverAsync(context, caregiverUser.Id, cancellationToken);
        await EnsureClientAsync(context, clientUser.Id, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        foreach (var roleName in Enum.GetNames<UserRole>())
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            EnsureSucceeded(result, $"create role {roleName}");
        }
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

    private static async Task EnsureIdentityUserAsync(
        UserManager<ApplicationUser> userManager,
        User domainUser,
        UserRole role,
        string seedPassword)
    {
        var identityUser = await userManager.FindByEmailAsync(domainUser.Email);

        if (identityUser is null)
        {
            identityUser = new ApplicationUser
            {
                Id = domainUser.Id,
                DomainUserId = domainUser.Id,
                UserName = domainUser.Email,
                Email = domainUser.Email,
                EmailConfirmed = true,
                PhoneNumber = domainUser.PhoneNumber,
                PhoneNumberConfirmed = true,
            };

            var createResult = await userManager.CreateAsync(identityUser, seedPassword);
            EnsureSucceeded(createResult, $"create identity user {domainUser.Email}");
        }

        var roleName = role.ToString();
        if (!await userManager.IsInRoleAsync(identityUser, roleName))
        {
            var roleResult = await userManager.AddToRoleAsync(identityUser, roleName);
            EnsureSucceeded(roleResult, $"assign role {roleName} to {domainUser.Email}");
        }
    }

    private static async Task EnsureCaregiverAsync(
        CarePathDbContext context,
        Guid userId,
        CancellationToken cancellationToken)
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

    private static async Task EnsureClientAsync(
        CarePathDbContext context,
        Guid userId,
        CancellationToken cancellationToken)
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

        var errors = string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
        throw new InvalidOperationException($"Seed operation failed to {operation}: {errors}");
    }
}
