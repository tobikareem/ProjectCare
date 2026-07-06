using CarePath.Contracts.Common;

namespace CarePath.Application.Common.Paging;

internal static class PagedResultFactory
{
    internal static PagedResult<T> Create<T>(
        IReadOnlyList<T> items,
        int totalCount,
        int pageNumber,
        int pageSize)
    {
        return new PagedResult<T>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    internal static IReadOnlyList<T> Page<T>(IReadOnlyList<T> items, int pageNumber, int pageSize)
    {
        return items
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToArray();
    }
}
