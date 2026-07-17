using System.Text;
using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;

namespace EfCoreMcp.Core.Services;

public sealed class RelationshipAnalyzer(IModelIntrospector introspector) : IRelationshipAnalyzer
{
    public RelationshipPath ExplainRelationship(string fromEntity, string toEntity)
    {
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

    public DependencyOrder GetDependencyOrder()
    {
        var model = introspector.DescribeModel();
        var entities = model.Entities.Where(e => !e.IsOwned).Select(e => e.Name).ToList();
        // Dependent depends on principal: principals must be inserted first.
        var dependsOn = model.Entities
            .Where(e => !e.IsOwned)
            .ToDictionary(
                e => e.Name,
                e => e.ForeignKeys
                    .Select(fk => fk.PrincipalEntity)
                    .Where(p => p != e.Name)
                    .Distinct()
                    .ToList());
        var order = new List<string>();
        var resolved = new HashSet<string>();
        var remaining = new HashSet<string>(entities);
        while (remaining.Count > 0)
        {
            var ready = remaining
                .Where(e => dependsOn.GetValueOrDefault(e, []).All(d => resolved.Contains(d) || !remaining.Contains(d)))
                .OrderBy(e => e, StringComparer.Ordinal)
                .ToList();
            if (ready.Count == 0)
                break; // remainder is cyclic
            foreach (var e in ready)
            {
                order.Add(e);
                resolved.Add(e);
                remaining.Remove(e);
            }
        }
        var cyclic = remaining.OrderBy(e => e, StringComparer.Ordinal).ToList();
        var deleteOrder = ((IEnumerable<string>)order).Reverse().ToList();
        return new DependencyOrder(order, deleteOrder, cyclic);
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

    private static List<RelationshipHop>? FindShortestPath(ModelDescriptor model, string from, string to)
    {
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
        var queue = new Queue<string>([from]);
        var cameFrom = new Dictionary<string, (string Prev, RelationshipHop Hop)> { };
        var visited = new HashSet<string> { from };
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var (neighbor, hop) in edges.GetValueOrDefault(current, []))
            {
                if (!visited.Add(neighbor))
                    continue;
                cameFrom[neighbor] = (current, hop);
                if (neighbor == to)
                {
                    var path = new List<RelationshipHop>();
                    for (var node = to; node != from; node = cameFrom[node].Prev)
                        path.Add(cameFrom[node].Hop);
                    path.Reverse();
                    return path;
                }
                queue.Enqueue(neighbor);
            }
        }
        return null;
    }

    private static void AddEdge(
        Dictionary<string, List<(string, RelationshipHop)>> edges, string from, string to, RelationshipHop hop)
    {
        if (!edges.TryGetValue(from, out var list))
            edges[from] = list = [];
        list.Add((to, hop));
    }
}
