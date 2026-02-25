using CarePath.Domain.Entities.Clinical;
using FluentAssertions;

namespace CarePath.Domain.Tests.Entities;

public class CarePlanTests
{
    [Fact]
    public void IsActive_DefaultsToTrue()
    {
        var plan = new CarePlan();
        plan.IsActive.Should().BeTrue();
    }

    [Fact]
    public void EndDate_DefaultsToNull()
    {
        var plan = new CarePlan();
        plan.EndDate.Should().BeNull();
    }

    [Fact]
    public void Goals_DefaultsToNull()
    {
        var plan = new CarePlan();
        plan.Goals.Should().BeNull();
    }

    [Fact]
    public void Interventions_DefaultsToNull()
    {
        var plan = new CarePlan();
        plan.Interventions.Should().BeNull();
    }

    [Fact]
    public void Notes_DefaultsToNull()
    {
        var plan = new CarePlan();
        plan.Notes.Should().BeNull();
    }

    [Fact]
    public void Title_DefaultsToEmptyString()
    {
        var plan = new CarePlan();
        plan.Title.Should().BeEmpty();
    }

    [Fact]
    public void CarePlan_CanBeDeactivated()
    {
        var plan = new CarePlan { IsActive = false };
        plan.IsActive.Should().BeFalse();
    }

    [Fact]
    public void CarePlan_CanHaveEndDate()
    {
        var endDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var plan = new CarePlan { EndDate = endDate };
        plan.EndDate.Should().Be(endDate);
    }
}
