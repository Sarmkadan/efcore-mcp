using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EfCoreMcp.Core.Services;

public sealed class ModelIntrospector(IDbContextProvider contextProvider) : IModelIntrospector
{
    public ModelDescriptor DescribeModel()
    {
        var ctx = contextProvider.GetContext();
        var entities = DesignTimeModel(ctx).GetEntityTypes()
            .Select(Describe)
            .OrderBy(e => e.Name, StringComparer.Ordinal)
            .ToList();
        return new ModelDescriptor(
            ctx.GetType().Name,
            ctx.Database.ProviderName,
            entities);
    }

    public EntityDescriptor? DescribeEntity(string entityName)
    {
        var entityType = FindEntityType(entityName);
        return entityType is null ? null : Describe(entityType);
    }

    public IReadOnlyList<string> ListEntityNames() =>
        DesignTimeModel(contextProvider.GetContext()).GetEntityTypes()
            .Select(e => e.ShortName())
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

    private static IModel DesignTimeModel(DbContext ctx) =>
        ctx.GetService<IDesignTimeModel>().Model;

    internal IEntityType? FindEntityType(string entityName)
    {
        var model = DesignTimeModel(contextProvider.GetContext());
        return model.GetEntityTypes().FirstOrDefault(e =>
            string.Equals(e.Name, entityName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.ShortName(), entityName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.GetTableName(), entityName, StringComparison.OrdinalIgnoreCase));
    }

    private static EntityDescriptor Describe(IEntityType entity)
    {
        var pk = entity.FindPrimaryKey();
        return new EntityDescriptor(
            entity.ShortName(),
            entity.ClrType.FullName ?? entity.ClrType.Name,
            entity.GetTableName(),
            entity.GetSchema(),
            entity.IsOwned(),
            entity.GetComment(),
            entity.GetProperties().Select(p => Describe(p, pk)).ToList(),
            pk is null ? null : Describe(pk),
            entity.GetKeys().Where(k => !k.IsPrimaryKey()).Select(Describe).ToList(),
            entity.GetForeignKeys().Select(Describe).ToList(),
            entity.GetNavigations().Select(Describe).ToList(),
            entity.GetIndexes().Select(Describe).ToList());
    }

    private static PropertyDescriptor Describe(IProperty property, IKey? pk) => new(
        property.Name,
        FormatClrType(property.ClrType),
        property.GetColumnName(),
        property.GetColumnType(),
        property.IsNullable,
        pk?.Properties.Contains(property) ?? false,
        property.IsForeignKey(),
        property.IsShadowProperty(),
        property.GetMaxLength(),
        property.GetDefaultValueSql(),
        property.ValueGenerated.ToString(),
        property.IsConcurrencyToken);

    private static KeyDescriptor Describe(IKey key) => new(
        key.GetName(),
        key.Properties.Select(p => p.Name).ToList(),
        key.IsPrimaryKey());

    private static ForeignKeyDescriptor Describe(IForeignKey fk) => new(
        fk.GetConstraintName(),
        fk.PrincipalEntityType.ShortName(),
        fk.DeclaringEntityType.ShortName(),
        fk.Properties.Select(p => p.Name).ToList(),
        fk.PrincipalKey.Properties.Select(p => p.Name).ToList(),
        fk.DeleteBehavior.ToString(),
        fk.IsRequired,
        fk.IsUnique);

    private static NavigationDescriptor Describe(INavigation nav) => new(
        nav.Name,
        nav.TargetEntityType.ShortName(),
        nav.IsCollection,
        nav.IsOnDependent,
        nav.Inverse?.Name);

    private static IndexDescriptor Describe(IIndex index) => new(
        index.GetDatabaseName(),
        index.Properties.Select(p => p.Name).ToList(),
        index.IsUnique,
        index.GetFilter());

    private static string FormatClrType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        return underlying is null ? type.Name : underlying.Name + "?";
    }
}
