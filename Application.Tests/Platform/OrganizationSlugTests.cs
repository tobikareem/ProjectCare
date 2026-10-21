using CarePath.Application.Platform;
using FluentAssertions;

namespace CarePath.Application.Tests.Platform;

public sealed class OrganizationSlugTests
{
    [Theory]
    [InlineData("brightcare")]
    [InlineData("bright-care")]
    [InlineData("abc")]
    [InlineData("a1b")]
    [InlineData("helping-hands-2")]
    public void IsValid_WhenSlugIsWellFormed_ReturnsTrue(string slug)
    {
        OrganizationSlug.IsValid(slug).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("BrightCare")]
    [InlineData("-brightcare")]
    [InlineData("brightcare-")]
    [InlineData("bright--care")]
    [InlineData("bright care")]
    [InlineData("bright.care")]
    public void IsValid_WhenSlugIsMalformed_ReturnsFalse(string? slug)
    {
        OrganizationSlug.IsValid(slug).Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenSlugIsFortyOneCharacters_ReturnsFalse()
    {
        OrganizationSlug.IsValid(new string('a', 41)).Should().BeFalse();
        OrganizationSlug.IsValid(new string('a', 40)).Should().BeTrue();
    }

    [Theory]
    [InlineData("platform")]
    [InlineData("admin")]
    [InlineData("api")]
    [InlineData("www")]
    [InlineData("login")]
    public void IsValid_WhenSlugIsReserved_ReturnsFalse(string slug)
    {
        OrganizationSlug.Reserved.Should().Contain(slug);
        OrganizationSlug.IsValid(slug).Should().BeFalse();
    }
}
