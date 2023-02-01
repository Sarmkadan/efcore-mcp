using EfCoreMcp.Core.Domain;
using EfCoreMcp.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EfCoreMcp.Core.Services;

/// <summary>
/// Provides extension methods for <see cref="ModelIntrospector"/>.
/// </summary>
public static class ModelIntrospectorExtensions
{
    /// <summary>
    /// Gets a list of all entity names that are owned by the database context.
    /// </summary>
    /// <param name="introspector">The model introspector.</param>
    /// <returns>A read-only list of entity names.</returns>
    /// <exception cref="ArgumentNullException">Thrown when introspector is null.</exception>
    public static IReadOnlyList<string> GetAllEntityNames(this ModelIntrospector introspector)
    {
        ArgumentNullException.ThrowIfNull(introspector);
        return introspector.ListEntityNames();
    }

    /// <summary>
    /// Checks if an entity with the given name exists in the model.
    /// </summary>
    /// <param name="introspector">The model introspector.</param>
    /// <param name="entityName">The name of the entity to check.</param>
    /// <returns>True if the entity exists; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when introspector is null.</exception>
    /// <exception cref="ArgumentException">Thrown when entityName is null or empty.</exception>
    public static bool EntityExists(this ModelIntrospector introspector, string entityName)
    {
        ArgumentNullException.ThrowIfNull(introspector);
        ArgumentException.ThrowIfNullOrEmpty(entityName);
        return introspector.DescribeEntity(entityName) is not null;
    }

    /// <summary>
    /// Retrieves a list of all entity descriptors currently in the model.
    /// </summary>
    /// <param name="introspector">The model introspector.</param>
    /// <returns>A read-only list of entity descriptors.</returns>
    /// <exception cref="ArgumentNullException">Thrown when introspector is null.</exception>
    public static IReadOnlyList<EntityDescriptor> GetAllEntities(this ModelIntrospector introspector)
    {
        ArgumentNullException.ThrowIfNull(introspector);
        var model = introspector.DescribeModel();
        return model.Entities;
    }
}
