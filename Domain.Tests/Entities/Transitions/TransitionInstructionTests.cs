using CarePath.Domain.Entities.Transitions;
using FluentAssertions;

namespace CarePath.Domain.Tests.Entities.Transitions;

public class TransitionInstructionTests
{
    // ── IsLowConfidence ────────────────────────────────────────────────────────

    [Fact]
    public void IsLowConfidence_ReturnsTrue_WhenConfidenceScoreIsBelowThreshold()
    {
        var instruction = new TransitionInstruction { ConfidenceScore = 0.74m };

        instruction.IsLowConfidence.Should().BeTrue();
    }

    [Fact]
    public void IsLowConfidence_ReturnsFalse_WhenConfidenceScoreIsExactlyAtThreshold()
    {
        // 0.75 is the boundary — at threshold means NOT low confidence
        var instruction = new TransitionInstruction { ConfidenceScore = 0.75m };

        instruction.IsLowConfidence.Should().BeFalse();
    }

    [Fact]
    public void IsLowConfidence_ReturnsFalse_WhenConfidenceScoreIsAboveThreshold()
    {
        var instruction = new TransitionInstruction { ConfidenceScore = 0.95m };

        instruction.IsLowConfidence.Should().BeFalse();
    }

    [Fact]
    public void IsLowConfidence_ReturnsTrue_WhenConfidenceScoreIsZero()
    {
        var instruction = new TransitionInstruction { ConfidenceScore = 0.0m };

        instruction.IsLowConfidence.Should().BeTrue();
    }

    [Fact]
    public void IsLowConfidence_ReturnsFalse_WhenConfidenceScoreIsMaximum()
    {
        var instruction = new TransitionInstruction { ConfidenceScore = 1.0m };

        instruction.IsLowConfidence.Should().BeFalse();
    }

    [Fact]
    public void IsLowConfidence_ReturnsTrue_WhenConfidenceScoreIsJustBelowThreshold()
    {
        var instruction = new TransitionInstruction { ConfidenceScore = 0.7499m };

        instruction.IsLowConfidence.Should().BeTrue();
    }
}
