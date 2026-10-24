using EfCoreMcp.Core.Domain;
using static EfCoreMcp.Core.Services.ModelAnalyzerConstants;

namespace EfCoreMcp.Core.Services.Rules;

/// <summary>
/// EFMCP008: flags entities that database-cascade from more than one principal, which SQL Server rejects as multiple cascade paths.
/// </summary>
internal sealed class MultipleCascadePathsRule : IModelRule
{
    /// <inheritdoc />
    public ModelRuleDescriptor Descriptor { get; } = new(
        CodeEfMcp008, SeverityWarning, "Multiple cascade paths",
        "Entity is a database-cascade target from more than one principal, which SQL Server rejects.");

    /// <inheritdoc />
    public IEnumerable<ModelFinding> Evaluate(ModelDescriptor model) =>
        model.Entities
            .Select(e => (Entity: e, Principals: e.ForeignKeys
                .Where(fk => fk.DeleteBehavior == DeleteBehaviorCascade)
                .Select(fk => fk.PrincipalEntity)
                .Distinct(StringComparer.Ordinal)
                .ToList()))
            .Where(x => x.Principals.Count > 1)
            .Select(x => new ModelFinding(Descriptor.Severity, Descriptor.Id, x.Entity.Name, null,
                $"Entity is a cascade-delete target from multiple principals: {string.Join(", ", x.Principals)}.",
                "SQL Server rejects schemas with multiple cascade paths onto one table ('may cause cycles or multiple cascade paths'). Set one side to Restrict/NoAction and handle deletion in code."));
}
