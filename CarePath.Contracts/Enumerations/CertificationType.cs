namespace CarePath.Contracts.Enumerations;

/// <summary>
/// Client-safe mirror of <c>CarePath.Domain.Enumerations.CertificationType</c>.
/// Values must stay identical to the Domain enum (parity is verified by tests).
/// </summary>
public enum CertificationType
{
    /// <summary>Certified Nursing Assistant.</summary>
    CNA = 1,

    /// <summary>Licensed Practical Nurse.</summary>
    LPN = 2,

    /// <summary>Registered Nurse.</summary>
    RN = 3,

    /// <summary>Home Health Aide.</summary>
    HHA = 4,

    /// <summary>CPR certification.</summary>
    CPR = 5,

    /// <summary>First Aid certification.</summary>
    FirstAid = 6,

    /// <summary>Dementia care certification.</summary>
    Dementia = 7,

    /// <summary>Alzheimer's care certification.</summary>
    Alzheimers = 8,

    /// <summary>Geriatric Nursing Assistant (Maryland).</summary>
    GNA = 9,

    /// <summary>Certified Residential Medication Aide (Maryland).</summary>
    CRMA = 10
}
