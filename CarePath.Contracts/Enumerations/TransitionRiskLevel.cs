namespace CarePath.Contracts.Enumerations;

/// <summary>
/// Client-safe mirror of <c>CarePath.Domain.Enumerations.TransitionRiskLevel</c>.
/// Values must stay identical to the Domain enum (parity is verified by tests).
/// Controls reminder intensity (High = daily touch).
/// </summary>
public enum TransitionRiskLevel
{
    /// <summary>Low readmission risk.</summary>
    Low = 1,

    /// <summary>Medium readmission risk.</summary>
    Medium = 2,

    /// <summary>High readmission risk; daily touch cadence.</summary>
    High = 3
}
