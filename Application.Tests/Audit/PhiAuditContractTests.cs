using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using FluentAssertions;

namespace CarePath.Application.Tests.Audit;

public sealed class PhiAuditContractTests
{
    [Fact]
    public void PhiAuditEntry_WhenInspected_DoesNotExposePhiPayloadFields()
    {
        // Arrange
        var forbiddenPropertyNames = new[]
        {
            "Payload",
            "Details",
            "Message",
            "Body",
            "RawContent",
            "SourceText",
            "PatientName",
            "Diagnosis",
            "Address",
            "DateOfBirth",
            "Ssn"
        };

        // Act
        var propertyNames = typeof(PhiAuditEntry)
            .GetProperties()
            .Select(property => property.Name)
            .ToList();

        // Assert
        propertyNames.Should().NotIntersectWith(forbiddenPropertyNames);
    }

    [Fact]
    public void PhiAuditEntry_WhenCreated_ContainsIdentifiersButNoPhiValues()
    {
        // Arrange
        var actorUserId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        // Act
        var entry = new PhiAuditEntry(
            actorUserId,
            AuditActorType.User,
            DateTime.UtcNow,
            AuditAction.Read,
            ProtectedResourceType.VisitNote,
            entityId,
            "corr-3");

        // Assert
        entry.ActorUserId.Should().Be(actorUserId);
        entry.EntityId.Should().Be(entityId);
        entry.BackgroundJobName.Should().BeNull();
        entry.GetType().GetProperties()
            .Any(property => property.Name.Contains("Content", StringComparison.OrdinalIgnoreCase))
            .Should().BeFalse();
    }
}