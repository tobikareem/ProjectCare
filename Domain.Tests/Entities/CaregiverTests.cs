using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Enumerations;
using FluentAssertions;

namespace CarePath.Domain.Tests.Entities;

public class CaregiverTests
{
    [Fact]
    public void EmploymentType_DefaultsToW2Employee()
    {
        var caregiver = new Caregiver();
        caregiver.EmploymentType.Should().Be(EmploymentType.W2Employee);
    }

    [Fact]
    public void AvailableWeekdays_DefaultsToTrue()
    {
        var caregiver = new Caregiver();
        caregiver.AvailableWeekdays.Should().BeTrue();
    }

    [Fact]
    public void AvailableWeekends_DefaultsToFalse()
    {
        var caregiver = new Caregiver();
        caregiver.AvailableWeekends.Should().BeFalse();
    }

    [Fact]
    public void AvailableNights_DefaultsToFalse()
    {
        var caregiver = new Caregiver();
        caregiver.AvailableNights.Should().BeFalse();
    }

    [Fact]
    public void MaxWeeklyHours_DefaultsTo40()
    {
        var caregiver = new Caregiver();
        caregiver.MaxWeeklyHours.Should().Be(40);
    }

    [Fact]
    public void TotalShiftsCompleted_DefaultsToZero()
    {
        var caregiver = new Caregiver();
        caregiver.TotalShiftsCompleted.Should().Be(0);
    }

    [Fact]
    public void NoShowCount_DefaultsToZero()
    {
        var caregiver = new Caregiver();
        caregiver.NoShowCount.Should().Be(0);
    }

    [Fact]
    public void RecordCompletedShift_IncrementsTotalShiftsCompleted()
    {
        var caregiver = new Caregiver();
        caregiver.RecordCompletedShift();
        caregiver.TotalShiftsCompleted.Should().Be(1);
    }

    [Fact]
    public void RecordCompletedShift_AccumulatesAcrossMultipleCalls()
    {
        var caregiver = new Caregiver();
        caregiver.RecordCompletedShift();
        caregiver.RecordCompletedShift();
        caregiver.RecordCompletedShift();
        caregiver.TotalShiftsCompleted.Should().Be(3);
    }

    [Fact]
    public void RecordNoShow_IncrementsNoShowCount()
    {
        var caregiver = new Caregiver();
        caregiver.RecordNoShow();
        caregiver.NoShowCount.Should().Be(1);
    }

    [Fact]
    public void RecordNoShow_AccumulatesAcrossMultipleCalls()
    {
        var caregiver = new Caregiver();
        caregiver.RecordNoShow();
        caregiver.RecordNoShow();
        caregiver.NoShowCount.Should().Be(2);
    }

    [Fact]
    public void RecordCompletedShift_DoesNotAffectNoShowCount()
    {
        var caregiver = new Caregiver();
        caregiver.RecordCompletedShift();
        caregiver.NoShowCount.Should().Be(0);
    }

    [Fact]
    public void RecordNoShow_DoesNotAffectTotalShiftsCompleted()
    {
        var caregiver = new Caregiver();
        caregiver.RecordNoShow();
        caregiver.TotalShiftsCompleted.Should().Be(0);
    }

    [Fact]
    public void AverageRating_DefaultsToNull()
    {
        var caregiver = new Caregiver();
        caregiver.AverageRating.Should().BeNull();
    }

    [Fact]
    public void SkillFlags_AllDefaultToFalse()
    {
        var caregiver = new Caregiver();
        caregiver.HasDementiaCare.Should().BeFalse();
        caregiver.HasAlzheimersCare.Should().BeFalse();
        caregiver.HasMobilityAssistance.Should().BeFalse();
        caregiver.HasMedicationManagement.Should().BeFalse();
    }

    [Fact]
    public void Certifications_DefaultsToEmptyCollection()
    {
        var caregiver = new Caregiver();
        caregiver.Certifications.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Shifts_DefaultsToEmptyCollection()
    {
        var caregiver = new Caregiver();
        caregiver.Shifts.Should().NotBeNull().And.BeEmpty();
    }
}
