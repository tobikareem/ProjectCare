using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Scheduling;
using FluentAssertions;

namespace CarePath.Domain.Tests.Integration;

public class ShiftVisitNoteNavigationTests
{
    [Fact]
    public void VisitNote_LinksToShift_ViaNavigationProperty()
    {
        var shift = new Shift();
        var caregiver = new Caregiver();
        var note = new VisitNote
        {
            ShiftId = shift.Id,
            Shift = shift,
            CaregiverId = caregiver.Id,
            Caregiver = caregiver,
            VisitDateTime = new DateTime(2026, 2, 16, 10, 0, 0, DateTimeKind.Utc)
        };

        note.Shift.Should().BeSameAs(shift);
        note.ShiftId.Should().Be(shift.Id);
    }

    [Fact]
    public void Shift_VisitNotesCollection_CanContainMultipleNotes()
    {
        var shift = new Shift();
        shift.VisitNotes.Add(new VisitNote());
        shift.VisitNotes.Add(new VisitNote());

        shift.VisitNotes.Should().HaveCount(2);
    }

    [Fact]
    public void VisitPhoto_LinksToVisitNote_ViaNavigationProperty()
    {
        var note = new VisitNote();
        var photo = new VisitPhoto
        {
            VisitNoteId = note.Id,
            VisitNote = note,
            PhotoUrl = "https://storage.blob.core.windows.net/photos/photo1.jpg"
        };

        photo.VisitNote.Should().BeSameAs(note);
        photo.VisitNoteId.Should().Be(note.Id);
    }

    [Fact]
    public void VisitNote_PhotosCollection_CanContainMultiplePhotos()
    {
        var note = new VisitNote();
        note.Photos.Add(new VisitPhoto { PhotoUrl = "https://storage.blob.core.windows.net/photos/1.jpg" });
        note.Photos.Add(new VisitPhoto { PhotoUrl = "https://storage.blob.core.windows.net/photos/2.jpg" });

        note.Photos.Should().HaveCount(2);
    }

    [Fact]
    public void VisitNote_WithActivityFlags_CanBeLinkedToShift()
    {
        var shift = new Shift();
        var note = new VisitNote
        {
            ShiftId = shift.Id,
            PersonalCare = true,
            MealPreparation = true,
            Companionship = false
        };
        shift.VisitNotes.Add(note);

        shift.VisitNotes.Should().ContainSingle();
        note.PersonalCare.Should().BeTrue();
        note.MealPreparation.Should().BeTrue();
        note.Companionship.Should().BeFalse();
    }
}
