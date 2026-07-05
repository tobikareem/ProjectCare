using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Auth;
using FluentAssertions;
using Moq;

namespace CarePath.Application.Tests.Auth;

public sealed class IdorGuardTests
{
    [Fact]
    public async Task EnsureAuthorizedAsync_WhenUserIsUnauthenticated_DeniesWithoutRevealingExistence()
    {
        // Arrange
        var currentUser = new StubCurrentUserContext(null, false, Array.Empty<string>(), "corr-1");
        var authorization = new Mock<IObjectAuthorizationService>(MockBehavior.Strict);
        var auditLogger = new Mock<IPhiAuditLogger>();
        var guard = new IdorGuard(currentUser, authorization.Object, auditLogger.Object);

        // Act
        var result = await guard.EnsureAuthorizedAsync(
            ProtectedResourceType.Client,
            Guid.NewGuid(),
            ObjectAccessAction.Read);

        // Assert
        result.IsAuthorized.Should().BeFalse();
        result.ShouldRevealExistence.Should().BeFalse();
        result.DenialCode.Should().Be("ResourceUnavailable");
        auditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry =>
                entry.ActorType == AuditActorType.Anonymous &&
                entry.Action == AuditAction.AccessDenied &&
                entry.EntityType == ProtectedResourceType.Client &&
                entry.CorrelationId == "corr-1"),
            It.IsAny<CancellationToken>()), Times.Once);
        authorization.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EnsureAuthorizedAsync_WhenObjectAuthorizationDenies_DeniesWithoutInternalReasonDisclosure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var currentUser = new StubCurrentUserContext(userId, true, new[] { ApplicationRoles.Caregiver }, "corr-2");
        var authorization = new Mock<IObjectAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(
                It.Is<ObjectAccessRequest>(request =>
                    request.User.UserId == userId &&
                    request.ResourceType == ProtectedResourceType.VisitNote &&
                    request.ResourceId == resourceId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ObjectAuthorizationResult.Denied("NotAssigned"));

        var auditLogger = new Mock<IPhiAuditLogger>();
        var guard = new IdorGuard(currentUser, authorization.Object, auditLogger.Object);

        // Act
        var result = await guard.EnsureAuthorizedAsync(
            ProtectedResourceType.VisitNote,
            resourceId,
            ObjectAccessAction.Read);

        // Assert
        result.IsAuthorized.Should().BeFalse();
        result.ShouldRevealExistence.Should().BeFalse();
        result.DenialCode.Should().Be("ResourceUnavailable");
        auditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry =>
                entry.ActorUserId == userId &&
                entry.ActorType == AuditActorType.User &&
                entry.Action == AuditAction.AccessDenied &&
                entry.EntityType == ProtectedResourceType.VisitNote &&
                entry.EntityId == resourceId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureAuthorizedAsync_WhenObjectAuthorizationAllows_AuditsSuccessfulAccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var currentUser = new StubCurrentUserContext(userId, true, new[] { ApplicationRoles.Coordinator }, "corr-3");
        var authorization = new Mock<IObjectAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ObjectAccessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ObjectAuthorizationResult.Authorized());

        var auditLogger = new Mock<IPhiAuditLogger>();
        var guard = new IdorGuard(currentUser, authorization.Object, auditLogger.Object);

        // Act
        var result = await guard.EnsureAuthorizedAsync(
            ProtectedResourceType.CarePlan,
            resourceId,
            ObjectAccessAction.Read);

        // Assert
        result.IsAuthorized.Should().BeTrue();
        result.ShouldRevealExistence.Should().BeTrue();
        auditLogger.Verify(logger => logger.LogAsync(
            It.Is<PhiAuditEntry>(entry =>
                entry.ActorUserId == userId &&
                entry.ActorType == AuditActorType.User &&
                entry.Action == AuditAction.Read &&
                entry.EntityType == ProtectedResourceType.CarePlan &&
                entry.EntityId == resourceId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class StubCurrentUserContext : ICurrentUserContext
    {
        public StubCurrentUserContext(
            Guid? userId,
            bool isAuthenticated,
            IEnumerable<string> roles,
            string? correlationId)
        {
            UserId = userId;
            IsAuthenticated = isAuthenticated;
            Roles = new HashSet<string>(roles, StringComparer.Ordinal);
            CorrelationId = correlationId;
        }

        public Guid? UserId { get; }

        public string? UserName => null;

        public bool IsAuthenticated { get; }

        public IReadOnlySet<string> Roles { get; }

        public string? CorrelationId { get; }
    }
}