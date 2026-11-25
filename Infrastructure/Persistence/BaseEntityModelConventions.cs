using System.Linq.Expressions;
using CarePath.Domain.Entities.Common;
using CarePath.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CarePath.Infrastructure.Persistence;

/// <summary>
/// Model conventions shared by every CarePath DbContext: the soft-delete query filter and UTC
/// round-tripping for all <see cref="BaseEntity"/> types. Applied once per model build.
/// </summary>
public static class BaseEntityModelConventions
{
    private static readonly UtcDateTimeConverter UtcDateTimeConverter = new();
    private static readonly NullableUtcDateTimeConverter NullableUtcDateTimeConverter = new();

    /// <summary>Applies the soft-delete filter and UTC converters to every <see cref="BaseEntity"/> in the model.</summary>
    /// <param name="builder">Model builder of the context being configured.</param>
    public static void Apply(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes()
                     .Where(entityType => typeof(BaseEntity).IsAssignableFrom(entityType.ClrType)))
        {
            ApplySoftDeleteQueryFilter(builder, entityType);
            ApplyUtcDateTimeConverters(entityType);
        }
    }

    /// <summary>
    /// Applies the UTC converters to every <see cref="DateTime"/> and <see cref="Nullable{DateTime}"/>
    /// property of one entity type. Used for the few non-<see cref="BaseEntity"/> types that still
    /// store timestamps.
    /// </summary>
    /// <param name="entityType">Entity type to configure.</param>
    public static void ApplyUtcDateTimeConverters(IMutableEntityType entityType)
    {
        foreach (var property in entityType.GetProperties())
        {
            if (property.ClrType == typeof(DateTime))
            {
                property.SetValueConverter(UtcDateTimeConverter);
            }
            else if (property.ClrType == typeof(DateTime?))
            {
                property.SetValueConverter(NullableUtcDateTimeConverter);
            }
        }
    }

    private static void ApplySoftDeleteQueryFilter(ModelBuilder builder, IMutableEntityType entityType)
    {
        var parameter = Expression.Parameter(entityType.ClrType, "entity");
        var property = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
        var filter = Expression.Lambda(Expression.Equal(property, Expression.Constant(false)), parameter);

        builder.Entity(entityType.ClrType).HasQueryFilter(filter);
    }
}
