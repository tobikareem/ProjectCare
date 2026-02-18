using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Enumerations;
using FluentAssertions;

namespace CarePath.Domain.Tests.Entities;

public class ClientTests
{
    [Fact]
    public void Age_CalculatesCorrectly_WhenBirthdayAlreadyPassedThisYear()
    {
        // 1980-01-01: birthday (Jan 1) has passed by Feb 16, 2026 → age = 46
        // Expected value hardcoded for 2026-02-16; update if test suite runs in a future year.
        var dob = new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var client = new Client { DateOfBirth = dob };
        client.Age.Should().Be(46);
    }

    [Fact]
    public void Age_CalculatesCorrectly_WhenBirthdayHasNotOccurredYetThisYear()
    {
        // 1985-12-31: birthday (Dec 31) has not occurred yet by Feb 16, 2026 → age = 40
        // Expected value hardcoded for 2026-02-16; update if test suite runs in a future year.
        var dob = new DateTime(1985, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var client = new Client { DateOfBirth = dob };
        client.Age.Should().Be(40);
    }

    [Fact]
    public void Age_IsLeapYearSafe_ForFeb29Birthday()
    {
        // 2000-02-29: leap day birthday; as of Feb 16, 2026 the birthday hasn't occurred yet → age = 25
        // Expected value hardcoded for 2026-02-16; update if test suite runs in a future year.
        var dob = new DateTime(2000, 2, 29, 0, 0, 0, DateTimeKind.Utc);
        var client = new Client { DateOfBirth = dob };
        var act = () => client.Age;
        act.Should().NotThrow();
        client.Age.Should().Be(25);
    }

    [Fact]
    public void Age_ReturnsZero_ForNewborn()
    {
        var dob = DateTime.UtcNow.AddDays(-1);
        var client = new Client { DateOfBirth = dob };
        client.Age.Should().Be(0);
    }

    [Fact]
    public void ServiceType_DefaultsToInHomeCare()
    {
        var client = new Client();
        client.ServiceType.Should().Be(ServiceType.InHomeCare);
    }

    [Fact]
    public void CareRequirementFlags_AllDefaultToFalse()
    {
        var client = new Client();
        client.RequiresDementiaCare.Should().BeFalse();
        client.RequiresMobilityAssistance.Should().BeFalse();
        client.RequiresMedicationManagement.Should().BeFalse();
        client.RequiresCompanionship.Should().BeFalse();
    }

    [Fact]
    public void Shifts_DefaultsToEmptyCollection()
    {
        var client = new Client();
        client.Shifts.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void CarePlans_DefaultsToEmptyCollection()
    {
        var client = new Client();
        client.CarePlans.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Invoices_DefaultsToEmptyCollection()
    {
        var client = new Client();
        client.Invoices.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void PhiFields_AreNullable()
    {
        var client = new Client
        {
            MedicalConditions = null,
            Allergies = null,
            SpecialInstructions = null
        };
        client.MedicalConditions.Should().BeNull();
        client.Allergies.Should().BeNull();
        client.SpecialInstructions.Should().BeNull();
    }

}
