namespace CarePath.Domain.Enumerations;

/// <summary>
/// Professional certifications a caregiver may hold.
/// Maryland regulations require specific certifications for certain service types.
/// </summary>
/// <remarks>
/// <para>
/// <b>Board credentials vs training completions:</b>
/// <see cref="CNA"/>, <see cref="LPN"/>, <see cref="RN"/>, <see cref="HHA"/>,
/// <see cref="GNA"/>, and <see cref="CRMA"/> are issued by the Maryland Board of Nursing
/// or an accredited body; they have a <c>CertificationNumber</c> and <c>IssuingAuthority</c>.
/// <see cref="CPR"/>, <see cref="FirstAid"/>, <see cref="Dementia"/>, and
/// <see cref="Alzheimers"/> are training completions — validation rules must not require
/// <c>CertificationNumber</c> or <c>IssuingAuthority</c> for these types.
/// </para>
/// <para>
/// <b>Dementia / Alzheimer's equivalence rule (for scheduling):</b>
/// Alzheimer's disease is the most common form of dementia. A caregiver with
/// <see cref="Alzheimers"/> training is qualified to care for clients whose condition
/// is classified as dementia. Scheduling logic must treat <see cref="Alzheimers"/> as
/// satisfying a <c>RequiresDementiaCare</c> requirement. The converse is not guaranteed —
/// verify with the coordinator before assigning a dementia-trained caregiver to an
/// Alzheimer's-specific placement.
/// </para>
/// </remarks>
public enum CertificationType
{
    /// <summary>Certified Nursing Assistant — entry-level direct-care credential, Maryland Board of Nursing.</summary>
    CNA = 1,

    /// <summary>Licensed Practical Nurse — intermediate nursing credential, Maryland Board of Nursing.</summary>
    LPN = 2,

    /// <summary>Registered Nurse — full nursing credential; highest clinical scope, Maryland Board of Nursing.</summary>
    RN = 3,

    /// <summary>Home Health Aide — authorised for personal care and basic health tasks in the home.</summary>
    HHA = 4,

    /// <summary>Cardiopulmonary Resuscitation certification. Training completion — no state-issued credential number.</summary>
    CPR = 5,

    /// <summary>First Aid certification. Training completion — no state-issued credential number.</summary>
    FirstAid = 6,

    /// <summary>
    /// Dementia care specialisation training completion. Not a state-issued board credential.
    /// Scheduling rule: <see cref="Alzheimers"/> satisfies this requirement (see class remarks).
    /// </summary>
    Dementia = 7,

    /// <summary>
    /// Alzheimer's disease care specialisation training completion. Not a state-issued board credential.
    /// Scheduling rule: also satisfies <see cref="Dementia"/> requirements (see class remarks).
    /// </summary>
    Alzheimers = 8,

    /// <summary>
    /// Geriatric Nursing Assistant — Maryland-specific credential regulated by the Maryland Board of Nursing.
    /// Required for direct-care placements in many Maryland long-term care facilities.
    /// Distinct from <see cref="CNA"/> — do not substitute.
    /// </summary>
    GNA = 9,

    /// <summary>
    /// Certified Residential Medication Aide — Maryland-specific credential authorising medication
    /// administration in residential and assisted living settings.
    /// Required for any shift involving medication administration in a Maryland assisted living facility.
    /// </summary>
    CRMA = 10
}
