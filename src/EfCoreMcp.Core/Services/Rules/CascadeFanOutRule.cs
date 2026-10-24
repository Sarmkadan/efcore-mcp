using EfCoreMcp.Core.Domain;
using static EfCoreMcp.Core.Services.ModelAnalyzerConstants;

namespace EfCoreMcp.Core.Services.Rules;

/// <summary>
/// EFMCP010: flags entities whose database-cascade chain reaches several dependent entity types, so one delete can wipe many tables.
/// </summary>
internal sealed class CascadeFanOutRule : IModelRule
{
    /// <inheritdoc />
    public ModelRuleDescriptor Descriptor { get; } = new(
        CodeEfMcp010, SeverityInfo, "Wide cascade-delete chain",
        "A single delete cascades transitively through several dependent entity types.");

    /// <inheritdoc />
    public IEnumerable<ModelFinding> Evaluate(ModelDescriptor model)
    {
        var dependentsByPrincipal = model.Entities
            .SelectMany(e => e.ForeignKeys.Where(fk => fk.DeleteBehavior == DeleteBehaviorCascade))
            .ToLookup(fk => fk.PrincipalEntity, fk => fk.DependentEntity, StringComparer.Ordinal);

        foreach (var entity in model.Entities)
        {
            var reached = CascadeReach(entity.Name, dependentsByPrincipal);
            if (reached.Count < CascadeFanOutThreshold)
                continue;
            yield return new ModelFinding(Descriptor.Severity, Descriptor.Id, entity.Name, null,
                $"Deleting a {entity.Name} row cascades through {reached.Count} dependent entity types: {string.Join(", ", reached.Order(StringComparer.Ordinal))}.",
                "A single delete removes rows from every listed table. Confirm each cascade is intended, or switch intermediate links to Restrict/SetNull to limit the blast radius.");
        }
    }

    // Breadth-first walk over cascade edges; the root and already-visited entities are skipped, so cycles terminate.
    private static HashSet<string> CascadeReach(string root, ILookup<string, string> dependentsByPrincipal)
    {
        var reached = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            foreach (var dependent in dependentsByPrincipal[queue.Dequeue()])
            {
                if (dependent != root && reached.Add(dependent))
                    queue.Enqueue(dependent);
            }
        }
        return reached;
    }
}
