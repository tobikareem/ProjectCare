using CarePath.Contracts.Common;

namespace CarePath.Client.Http;

/// <summary>
/// Helpers for translating paging parameters into query strings.
/// </summary>
public static class PagedRequestExtensions
{
    /// <summary>
    /// Renders the paging parameters as a query string fragment (no leading <c>?</c>),
    /// e.g. <c>pageNumber=2&amp;pageSize=20</c>. Values are already clamped by
    /// <see cref="PagedRequest"/>.
    /// </summary>
    /// <param name="request">The paging parameters.</param>
    /// <returns>The query string fragment.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="request"/> is null.</exception>
    public static string ToQueryString(this PagedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return $"pageNumber={request.PageNumber}&pageSize={request.PageSize}";
    }
}
