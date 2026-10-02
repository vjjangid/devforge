using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevForge.Infrastructure.Persistence.Configurations;

/// <summary>
/// Enums are stored by name (readable in psql, stable if members are reordered) and guarded by a
/// CHECK constraint so the column can never hold a value the code does not know.
/// </summary>
internal static class EnumColumn
{
    public const int MaxLength = 20;

    public static void HasEnumCheckConstraint<TEntity, TEnum>(
        this TableBuilder<TEntity> table,
        string constraintName,
        string columnName)
        where TEntity : class
        where TEnum : struct, Enum
    {
        table.HasCheckConstraint(constraintName, $"{columnName} IN ({SqlList(Enum.GetValues<TEnum>())})");
    }

    public static string SqlList<TEnum>(IEnumerable<TEnum> values)
        where TEnum : struct, Enum =>
        string.Join(", ", values.Select(value => $"'{value}'"));
}
