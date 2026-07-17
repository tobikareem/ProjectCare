using CarePath.Contracts.Common;

namespace CarePath.Contracts.Scheduling;

/// <summary>PHI-adjacent assignment-history filters sent in an authenticated request body.</summary>
public sealed class AssignmentHistorySearchRequest : PagedRequest
{
    /// <summary>Optional person-name search. Never place this value in a URL or log.</summary>
    public string? SearchTerm { get; init; }

    /// <summary>Optional derived relationship status.</summary>
    public AssignmentRelationshipStatus? Status { get; init; }

    /// <summary>Optional inclusive UTC lower bound for overlapping shifts.</summary>
    public DateTime? FromUtc { get; init; }

    /// <summary>Optional exclusive UTC upper bound for overlapping shifts.</summary>
    public DateTime? ToUtc { get; init; }
}
