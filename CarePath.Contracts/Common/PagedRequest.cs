namespace CarePath.Contracts.Common;

/// <summary>
/// Standard paging parameters for list endpoints. Values are clamped on assignment so
/// clients can never request unbounded pages (protects large PHI tables such as Shift and VisitNote).
/// </summary>
public class PagedRequest
{
    /// <summary>Default page size applied when none (or an invalid one) is supplied.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>Maximum page size the API will honor. Larger requests are clamped, not rejected.</summary>
    public const int MaxPageSize = 100;

    private readonly int _pageNumber = 1;
    private readonly int _pageSize = DefaultPageSize;

    /// <summary>One-based page number. Values below 1 are clamped to 1.</summary>
    public int PageNumber
    {
        get => _pageNumber;
        init => _pageNumber = value < 1 ? 1 : value;
    }

    /// <summary>Page size. Non-positive values fall back to <see cref="DefaultPageSize"/>; values above <see cref="MaxPageSize"/> are clamped.</summary>
    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value < 1 ? DefaultPageSize : Math.Min(value, MaxPageSize);
    }
}
