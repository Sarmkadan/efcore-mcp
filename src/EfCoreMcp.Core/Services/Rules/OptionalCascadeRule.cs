using EfCoreMcp.Core.Domain;
using static EfCoreMcp.Core.Services.ModelAnalyzerConstants;

namespace EfCoreMcp.Core.Services.Rules;

/// <summary>
/// EFMCP004: flags optional relationships configured with database cascade delete.
/// </summary>
internal sealed class OptionalCascadeRule : IModelRule
{
    /// <inheritdoc />
    public ModelRuleDescriptor Descriptor { get; } = new(
        CodeEfMcp004, SeverityWarning, "Optional relationship with cascade delete",
        "An optional foreign key is configured with cascade delete, so deleting the principal removes dependents that could have survived.");

    /// <inheritdoc />
    public IEnumerable<ModelFinding> Evaluate(ModelDescriptor model) =>
        model.Entities.SelectMany(e => e.ForeignKeys
            .Where(fk => !fk.IsRequired && fk.DeleteBehavior == DeleteBehaviorCascade)
            .Select(fk => new ModelFinding(Descriptor.Severity, Descriptor.Id, e.Name, string.Join(", ", fk.Properties),
                $"Optional relationship to {fk.PrincipalEntity} is configured with cascade delete.",
                "Deleting the principal will delete dependents that could have survived with a NULL FK. Use DeleteBehavior.SetNull or ClientSetNull unless deletion is intended.")));
}
