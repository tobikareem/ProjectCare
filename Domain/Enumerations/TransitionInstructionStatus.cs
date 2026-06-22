namespace CarePath.Domain.Enumerations;

/// <summary>
/// Review status of a single <see cref="Entities.Transitions.TransitionInstruction"/>
/// within the clinician review workflow.
/// </summary>
public enum TransitionInstructionStatus
{
    /// <summary>Awaiting clinician review. No reminder will fire for this instruction yet.</summary>
    Pending = 1,

    /// <summary>Clinician confirmed the instruction as extracted. Ready for reminder scheduling.</summary>
    Approved = 2,

    /// <summary>Clinician rejected the instruction (e.g. extraction error or outdated info).</summary>
    Rejected = 3,

    /// <summary>Clinician edited the instruction text before approving. Source text is preserved.</summary>
    Modified = 4
}
