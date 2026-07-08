using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;

namespace CarePath.WebApi.ModelBinding;

/// <summary>
/// Supplies <see cref="UtcDateTimeModelBinder"/> for <see cref="DateTime"/> and nullable
/// <see cref="DateTime"/> parameters bound from query strings and route values.
/// </summary>
public sealed class UtcDateTimeModelBinderProvider : IModelBinderProvider
{
    /// <inheritdoc />
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Metadata.UnderlyingOrModelType != typeof(DateTime))
        {
            return null;
        }

        var loggerFactory = context.Services.GetRequiredService<ILoggerFactory>();
        var inner = new DateTimeModelBinder(
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AdjustToUniversal,
            loggerFactory);

        return new UtcDateTimeModelBinder(inner);
    }
}
