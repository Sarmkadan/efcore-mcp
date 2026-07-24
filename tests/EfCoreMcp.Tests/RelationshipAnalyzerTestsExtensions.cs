using System;
using System.Collections.Generic;
using System.Linq;
using EfCoreMcp.Core.Domain;

namespace EfCoreMcp.Tests;

/// <summary>
/// Extension methods for <see cref="RelationshipPath"/> that provide convenient
/// assertions and helper methods for testing relationship analysis scenarios.
/// </summary>
public static class RelationshipAnalyzerTestsExtensions
{
    /// <summary>
    /// Asserts that a relationship path is valid and found.
    /// </summary>
    /// <param name="path">The relationship path to validate.</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    public static void AssertFound(this RelationshipPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!path.Found)
        {
            throw new InvalidOperationException("Relationship path not found.");
        }
    }

    /// <summary>
    /// Gets the intermediate entities in a relationship path (excluding source and target).
    /// </summary>
    /// <param name="path">The relationship path.</param>
    /// <returns>Sequence of intermediate entity names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    public static IEnumerable<string> GetIntermediateEntities(this RelationshipPath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (path.Hops.Count <= 2)
        {
            return Array.Empty<string>();
        }

        return path.Hops
            .Skip(1)
            .Take(path.Hops.Count - 2)
            .Select(h => h.ToEntity);
    }

    /// <summary>
    /// Gets the full entity path as a sequence of entity names from source to target.
    /// </summary>
    /// <param name="path">The relationship path.</param>
    /// <returns>Sequence of entity names in traversal order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    public static IEnumerable<string> GetEntityPath(this RelationshipPath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        yield return path.FromEntity;
        foreach (var hop in path.Hops)
        {
            yield return hop.ToEntity;
        }
    }

    /// <summary>
    /// Asserts that a relationship path contains a specific intermediate entity.
    /// </summary>
    /// <param name="path">The relationship path.</param>
    /// <param name="expectedIntermediateEntity">The expected intermediate entity name.</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    public static void AssertContainsIntermediateEntity(
        this RelationshipPath path,
        string expectedIntermediateEntity)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentException.ThrowIfNullOrEmpty(expectedIntermediateEntity);

        var intermediates = path.GetIntermediateEntities().ToList();
        if (!intermediates.Contains(expectedIntermediateEntity))
        {
            throw new InvalidOperationException(
                $"Expected intermediate entity '{expectedIntermediateEntity}' not found in path.");
        }
    }


    /// <summary>
    /// Gets the navigation description for the first hop in the relationship path.
    /// </summary>
    /// <param name="path">The relationship path.</param>
    /// <returns>The navigation description.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    public static string GetFirstHopNavigation(this RelationshipPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.Hops.Count > 0 ? path.Hops[0].NavigationDescription : string.Empty;
    }

    /// <summary>
    /// Gets the foreign key properties for the first hop in the relationship path.
    /// </summary>
    /// <param name="path">The relationship path.</param>
    /// <returns>Sequence of foreign key property names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    public static IEnumerable<string> GetFirstHopForeignKeys(this RelationshipPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.Hops.Count > 0 ? path.Hops[0].ForeignKeyProperties : Array.Empty<string>();
    }
}
