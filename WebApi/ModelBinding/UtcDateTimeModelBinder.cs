using CarePath.WebApi.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CarePath.WebApi.ModelBinding;

/// <summary>
/// Wraps the framework <see cref="DateTime"/> binder for query and route values and normalizes
/// the bound result to <see cref="DateTimeKind.Utc"/>, mirroring what
/// <see cref="UtcDateTimeJsonConverter"/> does for JSON bodies.
/// </summary>
public sealed class UtcDateTimeModelBinder : IModelBinder
{
    private readonly IModelBinder _inner;

    /// <summary>Initializes the binder around the framework DateTime binder.</summary>
    /// <param name="inner">Framework binder that performs the actual parsing.</param>
    public UtcDateTimeModelBinder(IModelBinder inner)
    {
        _inner = inner;
    }

    /// <inheritdoc />
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        await _inner.BindModelAsync(bindingContext);

        if (bindingContext.Result.IsModelSet && bindingContext.Result.Model is DateTime value)
        {
            bindingContext.Result = ModelBindingResult.Success(UtcDateTimeNormalizer.Normalize(value));
        }
    }
}
