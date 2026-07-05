using CarePath.Application.Scheduling.Commands;
using CarePath.Application.Scheduling.Validators;
using CarePath.Domain.Enumerations;
using FluentAssertions;

namespace CarePath.Application.Tests.Scheduling;

public sealed class CreateShiftCommandValidatorTests
{
    private readonly CreateShiftCommandValidator validator = new();

    [Fact]
    public void Validate_WhenDateRangeIsInvalid_RejectsCommand()
    {
        // Arrange
        var command = CreateValidCommand() with
        {
            ScheduledEndUtc = DateTime.SpecifyKind(new DateTime(2026, 7, 5, 8, 0, 0), DateTimeKind.Utc),
            ScheduledStartUtc = DateTime.SpecifyKind(new DateTime(2026, 7, 5, 9, 0, 0), DateTimeKind.Utc)
        };

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(CreateShiftCommand.ScheduledEndUtc));
    }

    [Fact]
    public void Validate_WhenIdentifiersAreEmpty_RejectsCommand()
    {
        // Arrange
        var command = CreateValidCommand() with
        {
            ClientId = Guid.Empty,
            CaregiverId = Guid.Empty
        };

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName)
            .Should().Contain(new[]
            {
                nameof(CreateShiftCommand.ClientId),
                nameof(CreateShiftCommand.CaregiverId)
            });
    }

    [Fact]
    public void Validate_WhenTimesAreNotUtc_RejectsCommand()
    {
        // Arrange
        var command = CreateValidCommand() with
        {
            ScheduledStartUtc = DateTime.SpecifyKind(new DateTime(2026, 7, 5, 8, 0, 0), DateTimeKind.Local),
            ScheduledEndUtc = DateTime.SpecifyKind(new DateTime(2026, 7, 5, 12, 0, 0), DateTimeKind.Local)
        };

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName)
            .Should().Contain(new[]
            {
                nameof(CreateShiftCommand.ScheduledStartUtc),
                nameof(CreateShiftCommand.ScheduledEndUtc)
            });
    }

    [Fact]
    public void Validate_WhenCommandIsValid_AcceptsCommand()
    {
        // Arrange
        var command = CreateValidCommand();

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    private static CreateShiftCommand CreateValidCommand() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.SpecifyKind(new DateTime(2026, 7, 5, 8, 0, 0), DateTimeKind.Utc),
            DateTime.SpecifyKind(new DateTime(2026, 7, 5, 12, 0, 0), DateTimeKind.Utc),
            15,
            45m,
            28m,
            ServiceType.InHomeCare);
}