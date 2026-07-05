namespace CarePath.WebApi.Middleware;

public static class ProblemDetailsMiddlewareExtensions
{
    public static IApplicationBuilder UseCarePathProblemDetails(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ProblemDetailsMiddleware>();
    }
}