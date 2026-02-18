using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Enumerations;
using FluentAssertions;

namespace CarePath.Domain.Tests.Entities;

public class UserTests
{
    [Fact]
    public void FullName_ConcatenatesFirstAndLastName()
    {
        var user = new User { FirstName = "Jane", LastName = "Doe" };
        user.FullName.Should().Be("Jane Doe");
    }

    [Fact]
    public void FullName_WithEmptyLastName_ReturnsFirstNameOnly()
    {
        var user = new User { FirstName = "Alice", LastName = string.Empty };
        user.FullName.Should().Be("Alice");
    }

    [Fact]
    public void FullName_WithEmptyFirstName_ReturnsLastNameOnly()
    {
        var user = new User { FirstName = string.Empty, LastName = "Doe" };
        user.FullName.Should().Be("Doe");
    }

    [Fact]
    public void IsActive_DefaultsToTrue()
    {
        var user = new User();
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public void State_DefaultsToMaryland()
    {
        var user = new User();
        user.State.Should().Be("Maryland");
    }

    [Fact]
    public void State_IsNullable_CanBeSetToNull()
    {
        var user = new User { State = null };
        user.State.Should().BeNull();
    }

    [Fact]
    public void LastLoginAt_DefaultsToNull()
    {
        var user = new User();
        user.LastLoginAt.Should().BeNull();
    }

    [Fact]
    public void Email_DefaultsToEmptyString()
    {
        var user = new User();
        user.Email.Should().BeEmpty();
    }

    [Fact]
    public void Role_CanBeAssigned()
    {
        var user = new User { Role = UserRole.Coordinator };
        user.Role.Should().Be(UserRole.Coordinator);
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Coordinator)]
    [InlineData(UserRole.Caregiver)]
    [InlineData(UserRole.Client)]
    [InlineData(UserRole.FacilityManager)]
    public void Role_AllValuesCanBeAssigned(UserRole role)
    {
        var user = new User { Role = role };
        user.Role.Should().Be(role);
    }
}
