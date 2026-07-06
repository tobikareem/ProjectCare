using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.Transitions.Services;
using FluentAssertions;

namespace CarePath.Infrastructure.Tests.Transitions;

public sealed class RuleBasedDischargeExtractionServiceTests
{
    [Fact]
    public async Task ExtractAsync_WhenContentHasKnownSections_ReturnsDeterministicPendingReviewInputs()
    {
        // Arrange
        var service = new RuleBasedDischargeExtractionService();
        const string rawContent = """
            Medication: take warfarin as directed.
            Follow-up appointment with cardiology.
            Call 911 for chest pain.
            """;

        // Act
        var result = await service.ExtractAsync(rawContent, DischargeDocumentSourceType.PdfUpload);

        // Assert
        result.Should().HaveCount(3);
        result[0].Category.Should().Be(TransitionInstructionCategory.Medication);
        result[0].NeedsPharmacistReview.Should().BeTrue();
        result[1].Category.Should().Be(TransitionInstructionCategory.Appointment);
        result[2].Category.Should().Be(TransitionInstructionCategory.WarningSigns);
        result.Select(instruction => instruction.SourceText).Should().OnlyContain(source => !string.IsNullOrWhiteSpace(source));
    }

    [Fact]
    public async Task ExtractAsync_WhenContentIsEmpty_ReturnsLowConfidenceReviewInstruction()
    {
        // Arrange
        var service = new RuleBasedDischargeExtractionService();

        // Act
        var result = await service.ExtractAsync(string.Empty, DischargeDocumentSourceType.PdfUpload);

        // Assert
        result.Should().ContainSingle();
        result[0].Category.Should().Be(TransitionInstructionCategory.Other);
        result[0].ConfidenceScore.Should().BeLessThan(0.75m);
    }
}
