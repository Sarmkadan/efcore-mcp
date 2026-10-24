using EfCoreMcp.Core.Domain;

namespace EfCoreMcp.Core.Services.Rules;

/// <summary>
/// Answers which columns of an entity are served by a key or index, shared by the foreign-key and key-column rules.
/// </summary>
internal static class KeyCoverage
{
    /// <summary>
    /// Determines whether a set of foreign-key columns is served by an index or key on the entity.
    /// </summary>
    /// <param name="entity">The dependent entity that declares the foreign key.</param>
    /// <param name="fkProperties">The foreign-key column names.</param>
    /// <returns><see langword="true"/> when a non-filtered index, the primary key or an alternate key leads with exactly the foreign-key columns.</returns>
    public static bool IsCoveredByIndexOrKey(EntityDescriptor entity, IReadOnlyList<string> fkProperties) =>
        entity.Indexes.Any(ix => ix.Filter is null && LeadingColumnsMatch(ix.Properties, fkProperties))
        || (entity.PrimaryKey is { } pk && LeadingColumnsMatch(pk.Properties, fkProperties))
        || entity.AlternateKeys.Any(k => LeadingColumnsMatch(k.Properties, fkProperties));

    /// <summary>
    /// Collects every property that participates in the primary key, an alternate key, a foreign key or an index.
    /// </summary>
    /// <param name="entity">The entity to inspect.</param>
    /// <returns>The set of member property names.</returns>
    public static HashSet<string> KeyAndIndexMembers(EntityDescriptor entity)
    {
        var members = new HashSet<string>(StringComparer.Ordinal);
        if (entity.PrimaryKey is { } pk)
            members.UnionWith(pk.Properties);
        foreach (var key in entity.AlternateKeys)
            members.UnionWith(key.Properties);
        foreach (var fk in entity.ForeignKeys)
            members.UnionWith(fk.Properties);
        foreach (var index in entity.Indexes)
            members.UnionWith(index.Properties);
        return members;
    }

    // Column order inside the leading prefix does not matter for an equality lookup: (B, A) serves a join on (A, B).
    private static bool LeadingColumnsMatch(IReadOnlyList<string> index, IReadOnlyList<string> fk) =>
        index.Count >= fk.Count && index.Take(fk.Count).ToHashSet(StringComparer.Ordinal).SetEquals(fk);
}
