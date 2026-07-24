using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;

namespace EfCoreMcp.Core.Services;

/// <summary>
/// Extension methods for <see cref="SchemaExplainer"/> that provide additional functionality
/// for analyzing and working with Entity Framework Core model schemas.
/// </summary>
public static class SchemaExplainerExtensions
{
    /// <summary>
    /// Gets all entity names in the model, excluding owned entities.
    /// </summary>
    /// <param name="explainer">The schema explainer instance.</param>
    /// <returns>An enumerable of entity names.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="explainer"/> is null.</exception>
    public static IEnumerable<string> GetEntityNames(this SchemaExplainer explainer)
    {
        ArgumentNullException.ThrowIfNull(explainer);

        var introspector = GetIntrospector(explainer);
        return introspector.ListEntityNames()
            .Where(name => !name.Contains(".")) // Filter out owned entities
            .Order(StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets all foreign key relationships in the model.
    /// </summary>
    /// <param name="explainer">The schema explainer instance.</param>
    /// <returns>A collection of foreign key relationship descriptors.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="explainer"/> is null.</exception>
    public static IReadOnlyList<Domain.ForeignKeyDescriptor> GetForeignKeys(this SchemaExplainer explainer)
    {
        ArgumentNullException.ThrowIfNull(explainer);

        var introspector = GetIntrospector(explainer);
        var model = introspector.DescribeModel();
        var result = new List<Domain.ForeignKeyDescriptor>();

        foreach (var entity in model.Entities)
        {
            foreach (var fk in entity.ForeignKeys)
            {
                result.Add(new Domain.ForeignKeyDescriptor(
                    ConstraintName: fk.ConstraintName,
                    PrincipalEntity: fk.PrincipalEntity,
                    DependentEntity: fk.DependentEntity,
                    Properties: fk.Properties,
                    PrincipalProperties: fk.PrincipalProperties,
                    DeleteBehavior: fk.DeleteBehavior,
                    IsRequired: fk.IsRequired,
                    IsUnique: fk.IsUnique
                ));
            }
        }

        return result.AsReadOnly();
    }

    /// <summary>
    /// Gets all indexes across all entities in the model.
    /// </summary>
    /// <param name="explainer">The schema explainer instance.</param>
    /// <returns>A collection of index descriptors.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="explainer"/> is null.</exception>
    public static IReadOnlyList<Domain.IndexDescriptor> GetAllIndexes(this SchemaExplainer explainer)
    {
        ArgumentNullException.ThrowIfNull(explainer);

        var introspector = GetIntrospector(explainer);
        var model = introspector.DescribeModel();
        var result = new List<Domain.IndexDescriptor>();

        foreach (var entity in model.Entities)
        {
            foreach (var index in entity.Indexes)
            {
                result.Add(new Domain.IndexDescriptor(
                    Name: index.Name,
                    Properties: index.Properties,
                    IsUnique: index.IsUnique,
                    Filter: index.Filter
                ));
            }
        }

        return result.AsReadOnly();
    }

    /// <summary>
    /// Finds an entity by name, returning null if not found.
    /// </summary>
    /// <param name="explainer">The schema explainer instance.</param>
    /// <param name="entityName">Name of the entity to find.</param>
    /// <returns>The entity descriptor, or null if not found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="explainer"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="entityName"/> is null or empty.</exception>
    public static EntityDescriptor? FindEntity(this SchemaExplainer explainer, string entityName)
    {
        ArgumentNullException.ThrowIfNull(explainer);
        ArgumentException.ThrowIfNullOrEmpty(entityName);

        var introspector = GetIntrospector(explainer);
        return introspector.DescribeEntity(entityName);
    }

    /// <summary>
    /// Gets all primary key properties for a specific entity.
    /// </summary>
    /// <param name="explainer">The schema explainer instance.</param>
    /// <param name="entityName">Name of the entity.</param>
    /// <returns>A collection of primary key property descriptors.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="explainer"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="entityName"/> is null or empty.</exception>
    public static IReadOnlyList<PropertyDescriptor> GetPrimaryKeyProperties(this SchemaExplainer explainer, string entityName)
    {
        ArgumentNullException.ThrowIfNull(explainer);
        ArgumentException.ThrowIfNullOrEmpty(entityName);

        var introspector = GetIntrospector(explainer);
        var entity = introspector.DescribeEntity(entityName);

        return (entity?.Properties
            .Where(p => p.IsPrimaryKey)
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToList() ?? []).AsReadOnly();
    }

    /// <summary>
    /// Gets all foreign key properties for a specific entity.
    /// </summary>
    /// <param name="explainer">The schema explainer instance.</param>
    /// <param name="entityName">Name of the entity.</param>
    /// <returns>A collection of foreign key property descriptors.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="explainer"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="entityName"/> is null or empty.</exception>
    public static IReadOnlyList<PropertyDescriptor> GetForeignKeyProperties(this SchemaExplainer explainer, string entityName)
    {
        ArgumentNullException.ThrowIfNull(explainer);
        ArgumentException.ThrowIfNullOrEmpty(entityName);

        var introspector = GetIntrospector(explainer);
        var entity = introspector.DescribeEntity(entityName);

        return (entity?.Properties
            .Where(p => p.IsForeignKey)
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToList() ?? []).AsReadOnly();
    }

    /// <summary>
    /// Gets all navigation properties for a specific entity.
    /// </summary>
    /// <param name="explainer">The schema explainer instance.</param>
    /// <param name="entityName">Name of the entity.</param>
    /// <returns>A collection of property descriptors that represent navigation properties.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="explainer"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="entityName"/> is null or empty.</exception>
    public static IReadOnlyList<PropertyDescriptor> GetNavigationProperties(this SchemaExplainer explainer, string entityName)
    {
        ArgumentNullException.ThrowIfNull(explainer);
        ArgumentException.ThrowIfNullOrEmpty(entityName);

        var introspector = GetIntrospector(explainer);
        var entity = introspector.DescribeEntity(entityName);

        return (entity?.Properties
            .Where(p => p.IsForeignKey || p.IsShadow)
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToList() ?? []).AsReadOnly();
    }

    /// <summary>
    /// Gets statistics about the model schema.
    /// </summary>
    /// <param name="explainer">The schema explainer instance.</param>
    /// <returns>A model statistics object.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="explainer"/> is null.</exception>
    public static SchemaStatistics GetStatistics(this SchemaExplainer explainer)
    {
        ArgumentNullException.ThrowIfNull(explainer);

        var introspector = GetIntrospector(explainer);
        var model = introspector.DescribeModel();
        var entities = model.Entities.Where(e => !e.IsOwned).ToList();
        var properties = entities.SelectMany(e => e.Properties).ToList();
        var foreignKeys = entities.SelectMany(e => e.ForeignKeys).ToList();
        var indexes = entities.SelectMany(e => e.Indexes).ToList();

        return new SchemaStatistics(
            EntityCount: entities.Count,
            PropertyCount: properties.Count,
            ForeignKeyCount: foreignKeys.Count,
            IndexCount: indexes.Count,
            OwnedEntityCount: model.Entities.Count - entities.Count,
            TotalTables: entities.Count(e => !string.IsNullOrEmpty(e.TableName))
        );
    }

    /// <summary>
    /// Gets the IModelIntrospector instance from a SchemaExplainer using reflection.
    /// </summary>
    /// <param name="explainer">The schema explainer instance.</param>
    /// <returns>The introspector instance.</returns>
    private static IModelIntrospector GetIntrospector(SchemaExplainer explainer)
    {
        var field = typeof(SchemaExplainer).GetField(
            "introspector",
            BindingFlags.Instance | BindingFlags.NonPublic);

        return (IModelIntrospector)field!.GetValue(explainer)!;
    }

    /// <summary>
    /// Descriptor for foreign key relationships.
    /// </summary>
    /// <param name="ConstraintName">The constraint name.</param>
    /// <param name="PrincipalEntity">The principal/referenced entity name.</param>
    /// <param name="DependentEntity">The dependent/owning entity name.</param>
    /// <param name="Properties">The dependent entity property names.</param>
    /// <param name="PrincipalProperties">The principal entity property names.</param>
    /// <param name="DeleteBehavior">The delete behavior for the relationship.</param>
    /// <param name="IsRequired">Whether the relationship is required.</param>
    /// <param name="IsUnique">Whether the relationship is unique (one-to-one).</param>
    public sealed record FkDescriptor(
        string? ConstraintName,
        string PrincipalEntity,
        string DependentEntity,
        IReadOnlyList<string> Properties,
        IReadOnlyList<string> PrincipalProperties,
        string DeleteBehavior,
        bool IsRequired,
        bool IsUnique);

    /// <summary>
    /// Descriptor for index information.
    /// </summary>
    /// <param name="EntityName">The entity name.</param>
    /// <param name="IndexName">The index name.</param>
    /// <param name="Properties">The indexed properties.</param>
    /// <param name="IsUnique">Whether the index is unique.</param>
    /// <param name="Filter">The filter condition, if any.</param>
    public sealed record IndexInfo(
        string EntityName,
        string IndexName,
        IReadOnlyList<string> Properties,
        bool IsUnique,
        string? Filter);

    /// <summary>
    /// Statistics about the model schema.
    /// </summary>
    /// <param name="EntityCount">Number of regular entities.</param>
    /// <param name="PropertyCount">Total number of properties across all entities.</param>
    /// <param name="ForeignKeyCount">Number of foreign key relationships.</param>
    /// <param name="IndexCount">Number of indexes across all entities.</param>
    /// <param name="OwnedEntityCount">Number of owned entities.</param>
    /// <param name="TotalTables">Number of entities with table mappings.</param>
    public sealed record SchemaStatistics(
        int EntityCount,
        int PropertyCount,
        int ForeignKeyCount,
        int IndexCount,
        int OwnedEntityCount,
        int TotalTables);
}