using System.Text;
using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;

namespace EfCoreMcp.Core.Services;

/// <summary>
/// Provides methods to analyze relationships between entities in a model.
/// </summary>
public sealed class RelationshipAnalyzer : IRelationshipAnalyzer
{
    private const int MaxPathFindingDepth = 50;
    private const int MaxPathsToExplore = 1000;
    private readonly IModelIntrospector introspector;

    /// <summary>
    /// Initializes a new instance of the <see cref="RelationshipAnalyzer"/> class with the specified model introspector.
    /// </summary>
    /// <param name="introspector">The model introspector used to retrieve the model descriptor.</param>
    public RelationshipAnalyzer(IModelIntrospector introspector)
    {
        ArgumentNullException.ThrowIfNull(introspector);
        this.introspector = introspector;
    }

    public RelationshipPath ExplainRelationship(string fromEntity, string toEntity)
    {
        ArgumentException.ThrowIfNullOrEmpty(fromEntity);
        ArgumentException.ThrowIfNullOrEmpty(toEntity);

        var model = introspector.DescribeModel();
        var from = Resolve(model, fromEntity);
        var to = Resolve(model, toEntity);
        var hops = FindShortestPath(model, from.Name, to.Name);
        if (hops is null)
            return new RelationshipPath(from.Name, to.Name, false, [],
                $"{from.Name} and {to.Name} are not connected by any chain of foreign keys.");
        var sb = new StringBuilder();
        sb.Append($"{from.Name} reaches {to.Name} in {hops.Count} hop{(hops.Count == 1 ? "" : "s")}: ");
        sb.AppendJoin(" -> ", hops.Select(h => h.NavigationDescription));
        return new RelationshipPath(from.Name, to.Name, true, hops, sb.ToString());
    }

    /// <summary>
    /// Gets the dependency order of entities based on foreign key dependencies.
    /// </summary>
    /// <returns>A <see cref="DependencyOrder"/> containing the insertion order, deletion order, cyclic entities, and detected cycles.</returns>
    public DependencyOrder GetDependencyOrder()
    {
        ArgumentNullException.ThrowIfNull(introspector);

        var model = introspector.DescribeModel();
        var entities = model.Entities.Where(e => !e.IsOwned).Select(e => e.Name).ToList();

        // Build dependency graph: Dependent depends on principal: principals must be inserted first.
        var dependsOn = model.Entities
            .Where(e => !e.IsOwned)
            .ToDictionary(
                e => e.Name,
                e => (IReadOnlyList<string>)e.ForeignKeys
                    .Select(fk => fk.PrincipalEntity)
                    .Where(p => p != e.Name)
                    .Distinct()
                    .ToList());

        var order = new List<string>();
        var resolved = new HashSet<string>();
        var remaining = new HashSet<string>(entities);
        var detectedCycles = new List<List<string>>();

        while (remaining.Count > 0)
        {
            var ready = remaining
                .Where(e => dependsOn.GetValueOrDefault(e, []).All(d => resolved.Contains(d) || !remaining.Contains(d)))
                .OrderBy(e => e, StringComparer.Ordinal)
                .ToList();

            if (ready.Count == 0)
            {
                // Remainder is cyclic - detect all cycles
                var cycles = DetectCycles(dependsOn, remaining);
                detectedCycles.AddRange(cycles);
                break;
            }

            foreach (var e in ready)
            {
                order.Add(e);
                resolved.Add(e);
                remaining.Remove(e);
            }
        }

        var cyclic = remaining.OrderBy(e => e, StringComparer.Ordinal).ToList();
        var deleteOrder = ((IEnumerable<string>)order).Reverse().ToList();
        return new DependencyOrder(order, deleteOrder, cyclic, detectedCycles);
    }

    private static List<RelationshipHop>? FindShortestPath(ModelDescriptor model, string from, string to)
    {
        ArgumentException.ThrowIfNullOrEmpty(from);
        ArgumentException.ThrowIfNullOrEmpty(to);

        if (from == to)
            return [];

        // Undirected BFS over foreign keys; each edge remembers its direction.
        var edges = new Dictionary<string, List<(string Neighbor, RelationshipHop Hop)>>();
        foreach (var entity in model.Entities)
            foreach (var fk in entity.ForeignKeys)
            {
                var cardinality = fk.IsUnique ? "one-to-one" : "one-to-many";
                var forward = new RelationshipHop(
                    fk.PrincipalEntity, fk.DependentEntity,
                    $"{fk.PrincipalEntity} has {(fk.IsUnique ? "one" : "many")} {fk.DependentEntity} via ({string.Join(", ", fk.Properties)})",
                    fk.Properties, cardinality, fk.DeleteBehavior);
                var backward = new RelationshipHop(
                    fk.DependentEntity, fk.PrincipalEntity,
                    $"{fk.DependentEntity}.({string.Join(", ", fk.Properties)}) references {fk.PrincipalEntity}",
                    fk.Properties, cardinality, fk.DeleteBehavior);
                AddEdge(edges, fk.PrincipalEntity, fk.DependentEntity, forward);
                AddEdge(edges, fk.DependentEntity, fk.PrincipalEntity, backward);
            }

        var queue = new Queue<(string Node, int Depth)>();
        queue.Enqueue((from, 0));
        var cameFrom = new Dictionary<string, (string Prev, RelationshipHop Hop)>();
        var visited = new HashSet<string> { from };
        var pathsExplored = 0;

        while (queue.Count > 0 && pathsExplored < MaxPathsToExplore)
        {
            var (current, depth) = queue.Dequeue();

            if (depth >= MaxPathFindingDepth)
                continue; // Prevent combinatorial blowup from deep paths

            foreach (var (neighbor, hop) in edges.GetValueOrDefault(current, []))
            {
                pathsExplored++;

                if (!visited.Add(neighbor))
                    continue; // Already visited this node

                cameFrom[neighbor] = (current, hop);

                if (neighbor == to)
                {
                    var path = new List<RelationshipHop>();
                    for (var node = to; node != from; node = cameFrom[node].Prev)
                        path.Add(cameFrom[node].Hop);
                    path.Reverse();
                    return path;
                }

                queue.Enqueue((neighbor, depth + 1));
            }
        }

        return null; // No path found or path finding limits exceeded
    }

    private static List<List<string>> DetectCycles(
        IReadOnlyDictionary<string, IReadOnlyList<string>> dependsOn,
        IReadOnlySet<string> remainingSet)
    {
        var cycles = new List<List<string>>();
        var visited = new HashSet<string>();

        foreach (var node in remainingSet.OrderBy(x => x, StringComparer.Ordinal))
        {
            if (!visited.Contains(node))
            {
                var path = new List<string>();
                var recursionStack = new HashSet<string>();
                FindCyclesDfs(dependsOn, node, visited, recursionStack, path, cycles, remainingSet);
            }
        }

        return cycles;
    }

    private static void FindCyclesDfs(
        IReadOnlyDictionary<string, IReadOnlyList<string>> dependsOn,
        string current,
        HashSet<string> visited,
        HashSet<string> recursionStack,
        List<string> path,
        List<List<string>> cycles,
        IReadOnlySet<string> remainingSet)
    {
        visited.Add(current);
        recursionStack.Add(current);
        path.Add(current);

        foreach (var neighbor in dependsOn.GetValueOrDefault(current, []))
        {
            if (!remainingSet.Contains(neighbor))
                continue;

            if (!visited.Contains(neighbor))
            {
                FindCyclesDfs(dependsOn, neighbor, visited, recursionStack, path, cycles, remainingSet);
            }
            else if (recursionStack.Contains(neighbor))
            {
                // Found a cycle
                var cycleStartIndex = path.IndexOf(neighbor);
                var cycle = path.Skip(cycleStartIndex).ToList();
                if (cycle.Count > 1 && !cycles.Any(c => c.SequenceEqual(cycle)))
                {
                    cycles.Add(cycle);
                }
            }
        }

        recursionStack.Remove(current);
        path.RemoveAt(path.Count - 1);
    }

    private static EntityDescriptor Resolve(ModelDescriptor model, string name)
    {
        return model.Entities.FirstOrDefault(e =>
            string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.ClrType, name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.TableName, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Entity '{name}' not found in the model. Available: {string.Join(", ", model.Entities.Select(e => e.Name))}");
    }

    private static void AddEdge(
        Dictionary<string, List<(string, RelationshipHop)>> edges, string from, string to, RelationshipHop hop)
    {
        if (!edges.TryGetValue(from, out var list))
            edges[from] = list = [];
        list.Add((to, hop));
    }
}