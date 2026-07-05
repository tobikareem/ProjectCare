namespace CarePath.Contracts.Common;

/// <summary>
/// A single page of results plus paging metadata. Mirrors the shape returned by
/// the repository <c>GetPagedAsync</c> contract (items + total non-deleted count).
/// </summary>
/// <typeparam name="T">Client-safe DTO type of the page items. Never a Domain entity.</typeparam>
public class PagedResult<T>
{
    /// <summary>The materialized items for the requested page.</summary>
    public IReadOnlyList<T> Items { get; init; } = [];

    /// <summary>One-based page number that was returned.</summary>
    public int PageNumber { get; init; }

    /// <summary>Requested page size.</summary>
    public int PageSize { get; init; }

    /// <summary>Total number of matching rows across all pages.</summary>
    public int TotalCount { get; init; }

    /// <summary>Total number of pages, computed from <see cref="TotalCount"/> and <see cref="PageSize"/>. Zero when <see cref="PageSize"/> is not positive.</summary>
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>True when a previous page exists.</summary>
    public bool HasPreviousPage => PageNumber > 1;

    /// <summary>True when a subsequent page exists.</summary>
    public bool HasNextPage => PageNumber < TotalPages;
}
