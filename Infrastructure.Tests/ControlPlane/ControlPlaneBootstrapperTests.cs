using CarePath.Application.Abstractions.Audit;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Platform;
using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.ControlPlane.Identity;
using CarePath.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using CarePath.Infrastructure.ControlPlane;
using CarePath.Infrastructure.ControlPlane.Bootstrap;
using CarePath.Infrastructure.ControlPlane.Persistence;
using CarePath.Infrastructure.Persistence.Interceptors;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace CarePath.Infrastructure.Tests.ControlPlane;

public sealed class ControlPlaneBootstrapperTests
{
    [Fact]
    public async Task EnsureDefaultOrganization_OnEmptyControlPlane_CreatesOrganizationDomainsBrandingAndDataPlane()
    {
        // Arrange
        await using var context = CreateContext();
        var audit = new Mock<IPlatformAuditLogger>();
        var bootstrapper = CreateBootstrapper(context, DefaultOptions(), audit.Object);

        // Act
        var organizationId = await bootstrapper.EnsureDefaultOrganizationAsync();

        // Assert
        var organization = await context.Organizations.SingleAsync();
        organizationId.Should().Be(organization.Id);
        organization.Slug.Should().Be("carepath");
        organization.Status.Should().Be(OrganizationStatus.Active);
        organization.ProvisioningState.Should().Be(ProvisioningState.Provisioned);
        organization.IsAdmissible.Should().BeTrue();

        var domains = await context.OrganizationDomains.OrderBy(domain => domain.HostName).ToListAsync();
        domains.Select(domain => domain.HostName).Should().Equal("carepath.localhost", "localhost");
        domains.Should().OnlyContain(domain => domain.VerificationStatus == DomainVerificationStatus.Verified);
        domains.Single(domain => domain.IsPrimary).HostName.Should().Be("localhost");

        (await context.OrganizationBrandings.SingleAsync()).OrganizationId.Should().Be(organization.Id);
        var dataPlane = await context.OrganizationDataPlanes.SingleAsync();
        dataPlane.SecretReference.Should().Be("ConnectionStrings:DefaultConnection");
        dataPlane.Provider.Should().Be(DataPlaneProvider.SqlServer);

        audit.Verify(logger => logger.LogAsync(
            It.Is<PlatformAuditEntry>(entry => entry.Action == PlatformAuditAction.BackfillOrganizationCreated && entry.OrganizationId == organization.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureDefaultOrganization_WhenRunTwice_ChangesNothing()
    {
        // Arrange
        await using var context = CreateContext();
        var audit = new Mock<IPlatformAuditLogger>();
        var bootstrapper = CreateBootstrapper(context, DefaultOptions(), audit.Object);
        var firstId = await bootstrapper.EnsureDefaultOrganizationAsync();
        var snapshot = await Snapshot(context);

        // Act
        var secondId = await bootstrapper.EnsureDefaultOrganizationAsync();

        // Assert
        secondId.Should().Be(firstId);
        (await Snapshot(context)).Should().Be(snapshot);
        audit.Verify(logger => logger.LogAsync(It.IsAny<PlatformAuditEntry>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureDefaultOrganization_WhenControlPlaneIsEmptyAndConfigurationIsMissing_ThrowsNamingTheKey()
    {
        // Arrange
        await using var context = CreateContext();
        var bootstrapper = CreateBootstrapper(context, new ControlPlaneOptions(), Mock.Of<IPlatformAuditLogger>());

        // Act
        var act = async () => await bootstrapper.EnsureDefaultOrganizationAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ControlPlane:DefaultOrganization*");
        (await context.Organizations.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task EnsureDefaultOrganization_WhenControlPlaneHasOrganizationsAndNoConfiguration_IsANoOp()
    {
        // Arrange
        await using var context = CreateContext();
        context.Organizations.Add(new Organization { LegalName = "Other", DisplayName = "Other", Slug = "other" });
        await context.SaveChangesAsync();
        var bootstrapper = CreateBootstrapper(context, new ControlPlaneOptions(), Mock.Of<IPlatformAuditLogger>());

        // Act
        var result = await bootstrapper.EnsureDefaultOrganizationAsync();

        // Assert
        result.Should().BeNull();
        (await context.Organizations.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task EnsureDefaultOrganization_WhenSlugBelongsToARemovedOrganization_ThrowsAConfigurationError()
    {
        // Arrange
        await using var context = CreateContext();
        context.Organizations.Add(new Organization { LegalName = "Old", DisplayName = "Old", Slug = "carepath", IsDeleted = true });
        await context.SaveChangesAsync();
        var bootstrapper = CreateBootstrapper(context, DefaultOptions(), Mock.Of<IPlatformAuditLogger>());

        // Act
        var act = async () => await bootstrapper.EnsureDefaultOrganizationAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*removed*");
    }

    [Fact]
    public async Task EnsureDefaultOrganization_WhenAHostBelongsToAnotherOrganization_ThrowsAConfigurationError()
    {
        // Arrange
        await using var context = CreateContext();
        var other = new Organization { LegalName = "Other", DisplayName = "Other", Slug = "other" };
        context.Organizations.Add(other);
        context.OrganizationDomains.Add(new OrganizationDomain { OrganizationId = other.Id, HostName = "localhost", VerificationStatus = DomainVerificationStatus.Verified });
        await context.SaveChangesAsync();
        var bootstrapper = CreateBootstrapper(context, DefaultOptions(), Mock.Of<IPlatformAuditLogger>());

        // Act
        var act = async () => await bootstrapper.EnsureDefaultOrganizationAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already registered*");
    }

    [Theory]
    [InlineData("Platform", "localhost")]
    [InlineData("ab", "localhost")]
    [InlineData("carepath", "bad host")]
    [InlineData("carepath", "")]
    public async Task EnsureDefaultOrganization_WhenSlugOrHostIsInvalid_ThrowsAndWritesNothing(string slug, string host)
    {
        // Arrange
        await using var context = CreateContext();
        var options = DefaultOptions();
        options.DefaultOrganization!.Slug = slug;
        options.DefaultOrganization.HostNames = host.Length == 0 ? [] : [host];
        var bootstrapper = CreateBootstrapper(context, options, Mock.Of<IPlatformAuditLogger>());

        // Act
        var act = async () => await bootstrapper.EnsureDefaultOrganizationAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ControlPlane:DefaultOrganization*");
        (await context.Organizations.AnyAsync()).Should().BeFalse();
    }

    private static async Task<string> Snapshot(ControlPlaneDbContext context)
    {
        var organizations = await context.Organizations.Select(organization => new { organization.Id, organization.UpdatedAt }).ToListAsync();
        var domains = await context.OrganizationDomains.CountAsync();
        var brandings = await context.OrganizationBrandings.CountAsync();
        var dataPlanes = await context.OrganizationDataPlanes.CountAsync();
        return $"{string.Join(',', organizations)}|{domains}|{brandings}|{dataPlanes}";
    }

    private static ControlPlaneOptions DefaultOptions() => new()
    {
        DefaultOrganization = new DefaultOrganizationOptions
        {
            LegalName = "CarePath Health LLC",
            DisplayName = "CarePath Health",
            Slug = "carepath",
            HostNames = ["LOCALHOST:5240", "carepath.localhost", "localhost"],
            DataPlane = new DefaultDataPlaneOptions { Provider = DataPlaneProvider.SqlServer, SecretReference = "ConnectionStrings:DefaultConnection" }
        }
    };

    [Fact]
    public async Task CopyLegacyIdentity_WhenLegacyTableHasRows_CopiesHashesAndStampsVerbatimAndIsIdempotent()
    {
        // Arrange — a SQLite tenant database with a hand-made legacy AspNetUsers table.
        await using var fixture = await SqliteTenantFixture.CreateAsync(createLegacyTable: true);
        var userId = Guid.NewGuid();
        await fixture.AddTenantUserAsync(userId, "Legacy", "Person", "legacy@carepath.local");
        await fixture.InsertLegacyIdentityAsync(userId, userId, "legacy@carepath.local", "HASH==", "stamp-1", lockoutEnd: DateTimeOffset.Parse("2030-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture), accessFailedCount: 3);
        await using var context = CreateContext();
        var bootstrapper = CreateBootstrapper(context, DefaultOptions(), Mock.Of<IPlatformAuditLogger>(), fixture.Tenant);

        // Act
        var first = await bootstrapper.CopyLegacyIdentityAsync();
        var second = await bootstrapper.CopyLegacyIdentityAsync();

        // Assert
        first.Should().Be(1);
        second.Should().Be(0);
        var platformUser = await context.Users.SingleAsync();
        platformUser.Id.Should().Be(userId);
        platformUser.PasswordHash.Should().Be("HASH==");
        platformUser.SecurityStamp.Should().Be("stamp-1");
        platformUser.AccessFailedCount.Should().Be(3);
        platformUser.LockoutEnd.Should().Be(DateTimeOffset.Parse("2030-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        platformUser.DisplayName.Should().Be("Legacy Person");
        platformUser.IsPlatformAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task CopyLegacyIdentity_WhenARowBreaksTheIdInvariant_FailsClosed()
    {
        // Arrange
        await using var fixture = await SqliteTenantFixture.CreateAsync(createLegacyTable: true);
        await fixture.InsertLegacyIdentityAsync(Guid.NewGuid(), Guid.NewGuid(), "mismatch@carepath.local", "HASH==", "stamp");
        await using var context = CreateContext();
        var bootstrapper = CreateBootstrapper(context, DefaultOptions(), Mock.Of<IPlatformAuditLogger>(), fixture.Tenant);

        // Act
        var act = async () => await bootstrapper.CopyLegacyIdentityAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*D-CP04-02*");
        (await context.Users.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task CopyLegacyIdentity_WhenLegacyTableIsAbsent_IsANoOp()
    {
        // Arrange
        await using var fixture = await SqliteTenantFixture.CreateAsync(createLegacyTable: false);
        await using var context = CreateContext();
        var bootstrapper = CreateBootstrapper(context, DefaultOptions(), Mock.Of<IPlatformAuditLogger>(), fixture.Tenant);

        // Act
        var copied = await bootstrapper.CopyLegacyIdentityAsync();

        // Assert
        copied.Should().Be(0);
    }

    [Fact]
    public async Task EnsureMemberships_CreatesOneMembershipPerUserWithAnIdentityMirroringActiveStatus()
    {
        // Arrange
        await using var context = CreateContext();
        var tenant = CreateTenantContext();
        var organizationId = Guid.NewGuid();
        context.Organizations.Add(new Organization { Id = organizationId, LegalName = "Org", DisplayName = "Org", Slug = "org" });
        var active = NewUser(UserRole.Coordinator, isActive: true);
        var inactive = NewUser(UserRole.Caregiver, isActive: false);
        var noIdentity = NewUser(UserRole.Client, isActive: true);
        tenant.DomainUsers.AddRange(active, inactive, noIdentity);
        context.Users.AddRange(NewPlatformUser(active), NewPlatformUser(inactive));
        await tenant.SaveChangesAsync();
        await context.SaveChangesAsync();
        var audit = new Mock<IPlatformAuditLogger>();
        var bootstrapper = CreateBootstrapper(context, DefaultOptions(), audit.Object, tenant);

        // Act
        var created = await bootstrapper.EnsureMembershipsAsync(organizationId);
        var again = await bootstrapper.EnsureMembershipsAsync(organizationId);

        // Assert
        created.Should().Be(2);
        again.Should().Be(0);
        var memberships = await context.OrganizationMemberships.ToListAsync();
        memberships.Should().HaveCount(2);
        var activeMembership = memberships.Single(membership => membership.PlatformUserId == active.Id);
        activeMembership.TenantUserId.Should().Be(active.Id);
        activeMembership.Role.Should().Be(UserRole.Coordinator);
        activeMembership.Status.Should().Be(MembershipStatus.Active);
        memberships.Single(membership => membership.PlatformUserId == inactive.Id).Status.Should().Be(MembershipStatus.Inactive);
        memberships.Should().NotContain(membership => membership.PlatformUserId == noIdentity.Id);
        (await tenant.DomainUsers.SingleAsync(user => user.Id == active.Id)).PlatformUserId.Should().Be(active.Id);
        (await tenant.DomainUsers.SingleAsync(user => user.Id == inactive.Id)).PlatformUserId.Should().Be(inactive.Id);
        audit.Verify(logger => logger.LogAsync(It.Is<PlatformAuditEntry>(entry => entry.Action == PlatformAuditAction.BackfillMembershipsCreated), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsurePlatformAdmin_CreatesTheAdministratorOnceAndFailsClosedWithoutAPassword()
    {
        // Arrange
        using var provider = CreateIdentityProvider(out var context);
        var options = DefaultOptions();
        options.PlatformAdmin = new PlatformAdminOptions { Email = "ops@carepath.local", DisplayName = "Ops", Password = null };
        var userManager = provider.GetRequiredService<UserManager<PlatformUser>>();
        var bootstrapper = new ControlPlaneBootstrapper(context, CreateTenantContext(), userManager, Options.Create(options), Mock.Of<IPlatformAuditLogger>(), new FakeTimeProvider(), NullLogger<ControlPlaneBootstrapper>.Instance);

        // Act
        var missingPassword = async () => await bootstrapper.EnsurePlatformAdminAsync();
        await missingPassword.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ControlPlane:PlatformAdmin:Password*");

        options.PlatformAdmin.Password = "Operator!Pass1";
        var created = await bootstrapper.EnsurePlatformAdminAsync();
        var again = await bootstrapper.EnsurePlatformAdminAsync();

        // Assert
        created.Should().BeTrue();
        again.Should().BeFalse();
        var admin = await userManager.FindByEmailAsync("ops@carepath.local");
        admin!.IsPlatformAdmin.Should().BeTrue();
        (await userManager.CheckPasswordAsync(admin, "Operator!Pass1")).Should().BeTrue();
    }

    [Fact]
    public async Task EnsurePlatformAdmin_WhenTheEmailBelongsToANonAdministrator_NeverElevates()
    {
        // Arrange
        using var provider = CreateIdentityProvider(out var context);
        var userManager = provider.GetRequiredService<UserManager<PlatformUser>>();
        await userManager.CreateAsync(new PlatformUser { UserName = "staff@carepath.local", Email = "staff@carepath.local", DisplayName = "Staff" }, "Staff!Pass1");
        var options = DefaultOptions();
        options.PlatformAdmin = new PlatformAdminOptions { Email = "staff@carepath.local", Password = "Operator!Pass1" };
        var bootstrapper = new ControlPlaneBootstrapper(context, CreateTenantContext(), userManager, Options.Create(options), Mock.Of<IPlatformAuditLogger>(), new FakeTimeProvider(), NullLogger<ControlPlaneBootstrapper>.Instance);

        // Act
        var act = async () => await bootstrapper.EnsurePlatformAdminAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*never elevates*");
        (await userManager.FindByEmailAsync("staff@carepath.local"))!.IsPlatformAdmin.Should().BeFalse();
    }

    private static User NewUser(UserRole role, bool isActive) => new()
    {
        FirstName = "Test",
        LastName = role.ToString(),
        Email = $"{Guid.NewGuid():N}@carepath.local",
        PhoneNumber = "555-0100",
        Role = role,
        IsActive = isActive,
    };

    private static PlatformUser NewPlatformUser(User user) => new()
    {
        Id = user.Id,
        UserName = user.Email,
        NormalizedUserName = user.Email.ToUpperInvariant(),
        Email = user.Email,
        NormalizedEmail = user.Email.ToUpperInvariant(),
        DisplayName = user.FullName,
    };

    private static ControlPlaneBootstrapper CreateBootstrapper(ControlPlaneDbContext context, ControlPlaneOptions options, IPlatformAuditLogger audit, CarePathDbContext? tenant = null) =>
        new(context, tenant ?? CreateTenantContext(), CreateIdentityProvider(context).GetRequiredService<UserManager<PlatformUser>>(), Options.Create(options), audit, new FakeTimeProvider(), NullLogger<ControlPlaneBootstrapper>.Instance);

    private static ControlPlaneDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ControlPlaneDbContext(options, new AuditableEntityInterceptor(new HttpContextAccessor()));
    }

    private static CarePathDbContext CreateTenantContext()
    {
        var options = new DbContextOptionsBuilder<CarePathDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CarePathDbContext(options, new AuditableEntityInterceptor(new HttpContextAccessor()));
    }

    private static ServiceProvider CreateIdentityProvider(out ControlPlaneDbContext context)
    {
        var created = CreateContext();
        context = created;
        return CreateIdentityProvider(created);
    }

    private static ServiceProvider CreateIdentityProvider(ControlPlaneDbContext context)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(context);
        services.AddIdentityCore<PlatformUser>().AddEntityFrameworkStores<ControlPlaneDbContext>();
        return services.BuildServiceProvider();
    }

    /// <summary>SQLite tenant database so the raw-SQL legacy copy can be exercised without SQL Server.</summary>
    private sealed class SqliteTenantFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private SqliteTenantFixture(SqliteConnection connection, CarePathDbContext tenant)
        {
            _connection = connection;
            Tenant = tenant;
        }

        public CarePathDbContext Tenant { get; }

        public static async Task<SqliteTenantFixture> CreateAsync(bool createLegacyTable)
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<CarePathDbContext>().UseSqlite(connection).Options;
            var tenant = new CarePathDbContext(options, new AuditableEntityInterceptor(new HttpContextAccessor()));
            await tenant.Database.EnsureCreatedAsync();

            if (createLegacyTable)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE "AspNetUsers" (
                        "Id" TEXT NOT NULL PRIMARY KEY, "DomainUserId" TEXT NOT NULL, "UserName" TEXT, "NormalizedUserName" TEXT,
                        "Email" TEXT, "NormalizedEmail" TEXT, "EmailConfirmed" INTEGER NOT NULL, "PasswordHash" TEXT,
                        "SecurityStamp" TEXT, "ConcurrencyStamp" TEXT, "PhoneNumber" TEXT, "PhoneNumberConfirmed" INTEGER NOT NULL,
                        "TwoFactorEnabled" INTEGER NOT NULL, "LockoutEnd" TEXT, "LockoutEnabled" INTEGER NOT NULL, "AccessFailedCount" INTEGER NOT NULL)
                    """;
                await command.ExecuteNonQueryAsync();
            }

            return new SqliteTenantFixture(connection, tenant);
        }

        public async Task AddTenantUserAsync(Guid id, string firstName, string lastName, string email)
        {
            Tenant.DomainUsers.Add(new User { Id = id, FirstName = firstName, LastName = lastName, Email = email, PhoneNumber = "555-0100", Role = UserRole.Coordinator });
            await Tenant.SaveChangesAsync();
        }

        public async Task InsertLegacyIdentityAsync(Guid id, Guid domainUserId, string email, string passwordHash, string securityStamp, DateTimeOffset? lockoutEnd = null, int accessFailedCount = 0)
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = """
                INSERT INTO "AspNetUsers" ("Id","DomainUserId","UserName","NormalizedUserName","Email","NormalizedEmail","EmailConfirmed","PasswordHash","SecurityStamp","ConcurrencyStamp","PhoneNumber","PhoneNumberConfirmed","TwoFactorEnabled","LockoutEnd","LockoutEnabled","AccessFailedCount")
                VALUES ($id,$domain,$email,$nemail,$email,$nemail,1,$hash,$stamp,'concurrency','555-0100',1,0,$lockout,1,$failed)
                """;
            command.Parameters.AddWithValue("$id", id.ToString().ToUpperInvariant());
            command.Parameters.AddWithValue("$domain", domainUserId.ToString().ToUpperInvariant());
            command.Parameters.AddWithValue("$email", email);
            command.Parameters.AddWithValue("$nemail", email.ToUpperInvariant());
            command.Parameters.AddWithValue("$hash", passwordHash);
            command.Parameters.AddWithValue("$stamp", securityStamp);
            command.Parameters.AddWithValue("$lockout", lockoutEnd.HasValue ? lockoutEnd.Value.ToString("O") : DBNull.Value);
            command.Parameters.AddWithValue("$failed", accessFailedCount);
            await command.ExecuteNonQueryAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Tenant.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
