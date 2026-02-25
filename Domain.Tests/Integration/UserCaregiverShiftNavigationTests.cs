using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Enumerations;
using FluentAssertions;

namespace CarePath.Domain.Tests.Integration;

/// <summary>
/// Tests that entity navigation properties can be wired in-memory without EF Core,
/// verifying object-graph relationships are correctly modelled in the Domain layer.
/// </summary>
public class UserCaregiverShiftNavigationTests
{
    [Fact]
    public void Caregiver_LinksToUser_ViaNavigationProperty()
    {
        var user = new User { FirstName = "Maria", LastName = "Santos", Role = UserRole.Caregiver };
        var caregiver = new Caregiver { UserId = user.Id, User = user };

        caregiver.User.Should().BeSameAs(user);
        caregiver.UserId.Should().Be(user.Id);
        caregiver.User.FullName.Should().Be("Maria Santos");
    }

    [Fact]
    public void Shift_LinksToCaregiver_ViaNavigationProperty()
    {
        var user = new User { FirstName = "Maria", LastName = "Santos" };
        var caregiver = new Caregiver { UserId = user.Id, User = user };
        var shift = new Shift
        {
            CaregiverId = caregiver.Id,
            Caregiver = caregiver,
            ScheduledStartTime = new DateTime(2026, 2, 16, 9, 0, 0, DateTimeKind.Utc),
            ScheduledEndTime   = new DateTime(2026, 2, 16, 17, 0, 0, DateTimeKind.Utc)
        };

        shift.Caregiver.Should().BeSameAs(caregiver);
        shift.Caregiver.User.FullName.Should().Be("Maria Santos");
    }

    [Fact]
    public void Caregiver_ShiftsCollection_CanContainMultipleShifts()
    {
        var caregiver = new Caregiver();
        var shift1 = new Shift { CaregiverId = caregiver.Id };
        var shift2 = new Shift { CaregiverId = caregiver.Id };
        caregiver.Shifts.Add(shift1);
        caregiver.Shifts.Add(shift2);

        caregiver.Shifts.Should().HaveCount(2);
        caregiver.Shifts.Should().Contain(shift1);
        caregiver.Shifts.Should().Contain(shift2);
    }

    [Fact]
    public void UnassignedShift_HasNullCaregiverIdAndNavigation()
    {
        var shift = new Shift();
        shift.CaregiverId.Should().BeNull();
        shift.Caregiver.Should().BeNull();
    }

    [Fact]
    public void Caregiver_VisitNotesCollection_CanContainNotes()
    {
        var caregiver = new Caregiver();
        var note = new VisitNote { CaregiverId = caregiver.Id };
        caregiver.VisitNotes.Add(note);

        caregiver.VisitNotes.Should().ContainSingle()
            .Which.CaregiverId.Should().Be(caregiver.Id);
    }
}
