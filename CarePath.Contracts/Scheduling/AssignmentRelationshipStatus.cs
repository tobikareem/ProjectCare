namespace CarePath.Contracts.Scheduling;

/// <summary>Derived state of a client-caregiver scheduling relationship.</summary>
public enum AssignmentRelationshipStatus
{
    /// <summary>The pair has an upcoming or in-progress non-cancelled shift.</summary>
    Current = 1,

    /// <summary>The pair has only historical non-cancelled shifts.</summary>
    Previous = 2,
}
