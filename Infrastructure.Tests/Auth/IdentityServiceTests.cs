using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.Auth;
using CarePath.Infrastructure.Identity;
using CarePath.Infrastructure.Persistence;
using CarePath.Infrastructure.Persistence.Interceptors;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.Storage;

namespace CarePath.Infrastructure.Tests.Auth;

public sealed class IdentityServiceTests
{
    private const string Password = "ValidPassword1";

    [Fact]
    public async Task ValidateCredentialsAsync_WhenCredentialsAreValid_ReturnsRolesAndUpdatesLastLoginAt()
    {
        // Arrange
        await using var fixture = await IdentityFixture.CreateAsync(isActive: true, isDeleted: false, UserRole.Clinician);
        var service = fixture.ServiceProvider.GetRequiredService<IdentityService>();

        // Act
        var result = await service.ValidateCredentialsAsync(fixture.Email, Password);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.UserId.Should().Be(fixture.UserId);
        result.Email.Should().Be(fixture.Email);
        result.Roles.Should().ContainSingle(UserRole.Clinician.ToString());

        var context = fixture.ServiceProvider.GetRequiredService<CarePathDbContext>();
        var domainUser = await context.DomainUsers.SingleAsync(user => user.Id == fixture.UserId);
        domainUser.LastLoginAt.Should().NotBeNull();
        domainUser.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WhenPasswordIsInvalid_ReturnsInvalidCredentialsWithoutUpdatingLastLoginAt()
    {
        // Arrange
        await using var fixture = await IdentityFixture.CreateAsync(isActive: true, isDeleted: false, UserRole.Admin);
        var service = fixture.ServiceProvider.GetRequiredService<IdentityService>();

        // Act
        var result = await service.ValidateCredentialsAsync(fixture.Email, "WrongPassword1");

        // Assert
        result.Succeeded.Should().BeFalse();
        result.FailureCode.Should().Be("InvalidCredentials");

        var context = fixture.ServiceProvider.GetRequiredService<CarePathDbContext>();
        var domainUser = await context.DomainUsers.SingleAsync(user => user.Id == fixture.UserId);
        domainUser.LastLoginAt.Should().BeNull();
    }

    [Fact]
    public async Task GetUserAsync_WhenDomainUserIsInactive_ReturnsInactiveUserFailure()
    {
        // Arrange
        await using var fixture = await IdentityFixture.CreateAsync(isActive: false, isDeleted: false, UserRole.Client);
        var service = fixture.ServiceProvider.GetRequiredService<IdentityService>();

        // Act
        var result = await service.GetUserAsync(fixture.UserId);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.FailureCode.Should().Be("InactiveUser");
    }


    [Fact]
    public async Task ValidateCredentialsAsync_WhenDomainUserIsSoftDeleted_ReturnsInvalidCredentials()
    {
        // Arrange
        await using var fixture = await IdentityFixture.CreateAsync(isActive: true, isDeleted: true, UserRole.Admin);
        var service = fixture.ServiceProvider.GetRequiredService<IdentityService>();

        // Act
        var result = await service.ValidateCredentialsAsync(fixture.Email, Password);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.FailureCode.Should().Be("UserNotFound");
    }

    [Fact]
    public async Task GetUserAsync_WhenDomainUserIsSoftDeleted_ReturnsUserNotFoundFailure()
    {
        // Arrange
        await using var fixture = await IdentityFixture.CreateAsync(isActive: true, isDeleted: true, UserRole.Admin);
        var service = fixture.ServiceProvider.GetRequiredService<IdentityService>();

        // Act
        var result = await service.GetUserAsync(fixture.UserId);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.FailureCode.Should().Be("UserNotFound");
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WhenPasswordFailsRepeatedly_LocksOutUser()
    {
        // Arrange
        await using var fixture = await IdentityFixture.CreateAsync(isActive: true, isDeleted: false, UserRole.Admin);
        var service = fixture.ServiceProvider.GetRequiredService<IdentityService>();

        // Act
        var firstFailure = await service.ValidateCredentialsAsync(fixture.Email, "WrongPassword1");
        var userManager = fixture.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var userAfterFirstFailure = await userManager.FindByIdAsync(fixture.UserId.ToString());
        var firstFailureCount = userAfterFirstFailure?.AccessFailedCount;

        var secondFailure = await service.ValidateCredentialsAsync(fixture.Email, "WrongPassword1");
        var lockedOutResult = await service.ValidateCredentialsAsync(fixture.Email, Password);

        // Assert
        firstFailure.FailureCode.Should().Be("InvalidCredentials");
        firstFailureCount.Should().Be(1);
        secondFailure.FailureCode.Should().Be("LockedOut");
        lockedOutResult.FailureCode.Should().Be("LockedOut");

        var user = await userManager.FindByIdAsync(fixture.UserId.ToString());
        user.Should().NotBeNull();
        var isLockedOut = await userManager.IsLockedOutAsync(user!);
        isLockedOut.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenTokenIsRotated_RejectsReuseAndStoresOnlyHash()
    {
        // Arrange
        await using var fixture = await IdentityFixture.CreateAsync(isActive: true, isDeleted: false, UserRole.Admin);
        var service = fixture.ServiceProvider.GetRequiredService<IdentityService>();

        // Act
        var firstToken = await service.IssueRefreshTokenAsync(fixture.UserId);
        var rotated = await service.RotateRefreshTokenAsync(firstToken);
        var reused = await service.RotateRefreshTokenAsync(firstToken);

        // Assert
        firstToken.Should().NotBeNullOrWhiteSpace();
        rotated.Succeeded.Should().BeTrue();
        rotated.RefreshToken.Should().NotBeNullOrWhiteSpace();
        rotated.RefreshToken.Should().NotBe(firstToken);
        reused.Succeeded.Should().BeFalse();
        reused.FailureCode.Should().Be("InvalidCredentials");

        var context = fixture.ServiceProvider.GetRequiredService<CarePathDbContext>();
        var user = await context.Set<ApplicationUser>().SingleAsync(identityUser => identityUser.Id == fixture.UserId);
        user.RefreshTokenHash.Should().NotBeNullOrWhiteSpace();
        user.RefreshTokenHash.Should().NotBe(rotated.RefreshToken);
    }

    [Fact]
    public async Task RotateRefreshTokenAsync_WhenTokenIsExpired_ReturnsInvalidCredentials()
    {
        // Arrange
        await using var fixture = await IdentityFixture.CreateAsync(isActive: true, isDeleted: false, UserRole.Admin);
        var service = fixture.ServiceProvider.GetRequiredService<IdentityService>();
        var refreshToken = await service.IssueRefreshTokenAsync(fixture.UserId);

        var context = fixture.ServiceProvider.GetRequiredService<CarePathDbContext>();
        var user = await context.Set<ApplicationUser>().SingleAsync(identityUser => identityUser.Id == fixture.UserId);
        user.RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await context.SaveChangesAsync();

        // Act
        var result = await service.RotateRefreshTokenAsync(refreshToken);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.FailureCode.Should().Be("InvalidCredentials");
    }

    private sealed class IdentityFixture : IAsyncDisposable
    {
        private IdentityFixture(ServiceProvider rootProvider, IServiceScope scope, Guid userId, string email)
        {
            RootProvider = rootProvider;
            Scope = scope;
            UserId = userId;
            Email = email;
        }

        private ServiceProvider RootProvider { get; }

        private IServiceScope Scope { get; }

        public IServiceProvider ServiceProvider => Scope.ServiceProvider;

        public Guid UserId { get; }

        public string Email { get; }

        public static async Task<IdentityFixture> CreateAsync(bool isActive, bool isDeleted, UserRole role)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpContextAccessor();
            services.AddScoped<AuditableEntityInterceptor>();
            var databaseRoot = new InMemoryDatabaseRoot();
            services.AddDbContext<CarePathDbContext>(options =>
                options.UseInMemoryDatabase(Guid.NewGuid().ToString(), databaseRoot));
            services
                .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
                {
                    options.Password.RequiredLength = 8;
                    options.Password.RequireDigit = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireNonAlphanumeric = false;
                    options.Lockout.AllowedForNewUsers = true;
                    options.Lockout.MaxFailedAccessAttempts = 2;
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                    options.User.RequireUniqueEmail = true;
                })
                .AddEntityFrameworkStores<CarePathDbContext>()
                .AddDefaultTokenProviders();
            services.AddScoped<IdentityService>();

            var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true,
            });

            var userId = Guid.NewGuid();
            var email = $"auth-{Guid.NewGuid():N}@carepath.local";
            var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CarePathDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

            await context.DomainUsers.AddAsync(new User
            {
                Id = userId,
                FirstName = "Synthetic",
                LastName = "User",
                Email = email,
                PhoneNumber = "555-0100",
                Role = role,
                IsActive = isActive,
                IsDeleted = isDeleted,
            });
            await context.SaveChangesAsync();

            var roleName = role.ToString();
            var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            roleResult.Succeeded.Should().BeTrue();

            var identityUser = new ApplicationUser
            {
                Id = userId,
                DomainUserId = userId,
                UserName = email,
                Email = email,
                EmailConfirmed = true,
            };
            var createResult = await userManager.CreateAsync(identityUser, Password);
            createResult.Succeeded.Should().BeTrue();

            var addRoleResult = await userManager.AddToRoleAsync(identityUser, roleName);
            addRoleResult.Succeeded.Should().BeTrue();

            return new IdentityFixture(provider, scope, userId, email);
        }

        public async ValueTask DisposeAsync()
        {
            Scope.Dispose();
            await RootProvider.DisposeAsync();
        }
    }
}
