using CarePath.Domain.Entities.Scheduling;
using FluentAssertions;

namespace CarePath.Domain.Tests.Entities;

public class VisitPhotoTests
{
    [Fact]
    public void PhotoUrl_DefaultsToEmptyString()
    {
        var photo = new VisitPhoto();
        photo.PhotoUrl.Should().BeEmpty();
    }

    [Fact]
    public void Caption_DefaultsToNull()
    {
        var photo = new VisitPhoto();
        photo.Caption.Should().BeNull();
    }

    [Fact]
    public void TakenAt_DefaultsToRecentUtcNow()
    {
        var before = DateTime.UtcNow;
        var photo = new VisitPhoto();

        photo.TakenAt.Should().BeOnOrAfter(before)
            .And.BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void PhotoUrl_CanBeSet()
    {
        var url = "https://storage.blob.core.windows.net/photos/visit-001.jpg";
        var photo = new VisitPhoto { PhotoUrl = url };
        photo.PhotoUrl.Should().Be(url);
    }
}
