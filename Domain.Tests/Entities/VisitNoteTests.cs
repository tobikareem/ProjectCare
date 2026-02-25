using CarePath.Domain.Entities.Scheduling;
using FluentAssertions;

namespace CarePath.Domain.Tests.Entities;

public class VisitNoteTests
{
    [Fact]
    public void ActivityFlags_AllDefaultToFalse()
    {
        var note = new VisitNote();
        note.PersonalCare.Should().BeFalse();
        note.MealPreparation.Should().BeFalse();
        note.Medication.Should().BeFalse();
        note.LightHousekeeping.Should().BeFalse();
        note.Companionship.Should().BeFalse();
        note.Transportation.Should().BeFalse();
        note.Exercise.Should().BeFalse();
    }

    [Fact]
    public void VitalSignFields_AreNullable_AndDefaultToNull()
    {
        var note = new VisitNote();
        note.BloodPressureSystolic.Should().BeNull();
        note.BloodPressureDiastolic.Should().BeNull();
        note.Temperature.Should().BeNull();
        note.HeartRate.Should().BeNull();
    }

    [Fact]
    public void PhiNoteFields_AreNullable_AndDefaultToNull()
    {
        var note = new VisitNote();
        note.Activities.Should().BeNull();
        note.ClientCondition.Should().BeNull();
        note.Concerns.Should().BeNull();
        note.Medications.Should().BeNull();
    }

    [Fact]
    public void SignatureUrlFields_AreNullable_AndDefaultToNull()
    {
        var note = new VisitNote();
        note.CaregiverSignatureUrl.Should().BeNull();
        note.ClientOrFamilySignatureUrl.Should().BeNull();
    }

    [Fact]
    public void SignatureUrlFields_CanStoreBlobStorageUrls()
    {
        var caregiverUrl = "https://storage.blob.core.windows.net/sigs/cg-abc.png";
        var clientUrl = "https://storage.blob.core.windows.net/sigs/cl-xyz.png";
        var note = new VisitNote
        {
            CaregiverSignatureUrl = caregiverUrl,
            ClientOrFamilySignatureUrl = clientUrl
        };
        note.CaregiverSignatureUrl.Should().Be(caregiverUrl);
        note.ClientOrFamilySignatureUrl.Should().Be(clientUrl);
    }

    [Fact]
    public void Photos_DefaultsToEmptyCollection()
    {
        var note = new VisitNote();
        note.Photos.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void VitalSigns_CanBeSet()
    {
        var note = new VisitNote
        {
            BloodPressureSystolic = 120,
            BloodPressureDiastolic = 80,
            Temperature = 98.6m,
            HeartRate = 72
        };
        note.BloodPressureSystolic.Should().Be(120);
        note.BloodPressureDiastolic.Should().Be(80);
        note.Temperature.Should().Be(98.6m);
        note.HeartRate.Should().Be(72);
    }

    [Fact]
    public void VisitDateTime_HasNoConstructionTimeDefault()
    {
        // VisitDateTime has no default; it should be DateTime.MinValue (default(DateTime))
        var note = new VisitNote();
        note.VisitDateTime.Should().Be(default(DateTime));
    }
}
