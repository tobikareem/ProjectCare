using CarePath.Contracts.Common;

namespace CarePath.WebApi.Middleware;

public sealed class ProblemDetailsResponse : ApiProblemDetails
{
    public IReadOnlyList<ApiError> Errors { get; init; } = [];
}