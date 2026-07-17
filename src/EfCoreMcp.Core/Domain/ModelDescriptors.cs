namespace EfCoreMcp.Core.Domain;

public sealed record ModelDescriptor(
    string ContextName,
    string? ProviderName,
    IReadOnlyList<EntityDescriptor> Entities);

public sealed record EntityDescriptor(
    string Name,
    string ClrType,
    string? TableName,
    string? Schema,
    bool IsOwned,
    string? Comment,
    IReadOnlyList<PropertyDescriptor> Properties,
    KeyDescriptor? PrimaryKey,
    IReadOnlyList<KeyDescriptor> AlternateKeys,
    IReadOnlyList<ForeignKeyDescriptor> ForeignKeys,
    IReadOnlyList<NavigationDescriptor> Navigations,
    IReadOnlyList<IndexDescriptor> Indexes);

public sealed record PropertyDescriptor(
    string Name,
    string ClrType,
    string? ColumnName,
    string? ColumnType,
    bool IsNullable,
    bool IsPrimaryKey,
    bool IsForeignKey,
    bool IsShadow,
    int? MaxLength,
    int? Precision,
    int? Scale,
    string? DefaultValueSql,
    string ValueGenerated,
    bool IsConcurrencyToken);

public sealed record KeyDescriptor(
    string? Name,
    IReadOnlyList<string> Properties,
    bool IsPrimary);

public sealed record ForeignKeyDescriptor(
    string? ConstraintName,
    string PrincipalEntity,
    string DependentEntity,
    IReadOnlyList<string> Properties,
    IReadOnlyList<string> PrincipalProperties,
    string DeleteBehavior,
    bool IsRequired,
    bool IsUnique);

public sealed record NavigationDescriptor(
    string Name,
    string TargetEntity,
    bool IsCollection,
    bool IsOnDependent,
    string? InverseName);

public sealed record IndexDescriptor(
    string? Name,
    IReadOnlyList<string> Properties,
    bool IsUnique,
    string? Filter);
