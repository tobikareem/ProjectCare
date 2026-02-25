using CarePath.Domain.Entities.Common;
using FluentAssertions;

namespace CarePath.Domain.Tests.Entities;

public class BaseEntityTests
{
    // Concrete subclass for testing the abstract base
    private sealed class TestEntity : BaseEntity { }

    [Fact]
    public void Id_DefaultsToNonEmptyGuid()
    {
        var entity = new TestEntity();
        entity.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Id_IsUniquePerInstance()
    {
        var a = new TestEntity();
        var b = new TestEntity();
        a.Id.Should().NotBe(b.Id);
    }

    [Fact]
    public void CreatedAt_DefaultsToRecentUtcNow()
    {
        var before = DateTime.UtcNow;
        var entity = new TestEntity();

        entity.CreatedAt.Should().BeOnOrAfter(before)
            .And.BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdatedAt_DefaultsToNull()
    {
        var entity = new TestEntity();
        entity.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void CreatedBy_DefaultsToNull()
    {
        var entity = new TestEntity();
        entity.CreatedBy.Should().BeNull();
    }

    [Fact]
    public void UpdatedBy_DefaultsToNull()
    {
        var entity = new TestEntity();
        entity.UpdatedBy.Should().BeNull();
    }

    [Fact]
    public void IsDeleted_DefaultsToFalse()
    {
        var entity = new TestEntity();
        entity.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void IsDeleted_CanBeSetToTrue()
    {
        var entity = new TestEntity { IsDeleted = true };
        entity.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void UpdatedAt_CanBeSet()
    {
        var timestamp = new DateTime(2026, 2, 16, 12, 0, 0, DateTimeKind.Utc);
        var entity = new TestEntity { UpdatedAt = timestamp };
        entity.UpdatedAt.Should().Be(timestamp);
    }

    [Fact]
    public void AuditFields_CanBeSetTogether()
    {
        var entity = new TestEntity
        {
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = "system",
            UpdatedBy = "admin@carepathhealth.com"
        };

        entity.CreatedBy.Should().Be("system");
        entity.UpdatedBy.Should().Be("admin@carepathhealth.com");
        entity.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Id_CanBeOverriddenViaInitSetter()
    {
        // Verifies the init-only Id contract: caller can supply a deterministic Id
        // (e.g., when rehydrating from persistence), but cannot reassign after construction.
        var fixedId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var entity = new TestEntity { Id = fixedId };
        entity.Id.Should().Be(fixedId);
    }

    [Fact]
    public void CreatedAt_CanBeOverriddenViaInitSetter()
    {
        // Verifies the init-only CreatedAt contract: caller can supply a historical timestamp
        // (e.g., when rehydrating from persistence), but cannot reassign after construction.
        var fixedTime = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var entity = new TestEntity { CreatedAt = fixedTime };
        entity.CreatedAt.Should().Be(fixedTime);
    }
}
