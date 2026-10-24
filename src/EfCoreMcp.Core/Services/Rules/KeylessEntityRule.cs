using EfCoreMcp.Core.Domain;
using static EfCoreMcp.Core.Services.ModelAnalyzerConstants;

namespace EfCoreMcp.Core.Services.Rules;

/// <summary>
/// EFMCP001: flags entity types that have no primary key.
/// </summary>
internal sealed class KeylessEntityRule : IModelRule
{
    /// <inheritdoc />
    public ModelRuleDescriptor Descriptor { get; } = new(
        CodeEfMcp001, SeverityWarning, "Keyless entity",
        "Entity type has no primary key. Keyless entities cannot be tracked, updated or used as principals.");

    /// <inheritdoc />
    public IEnumerable<ModelFinding> Evaluate(ModelDescriptor model) =>
        model.Entities
            .Where(e => e.PrimaryKey is null && !e.IsOwned)
            .Select(e => new ModelFinding(Descriptor.Severity, Descriptor.Id, e.Name, null,
                "Entity has no primary key (keyless entity type).",
                "Keyless entities cannot be tracked, updated or used as principals. If this is intentional (view/raw-SQL projection), ignore; otherwise add HasKey()."));
}
