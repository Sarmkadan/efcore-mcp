using EfCoreMcp.Core.Domain;
using static EfCoreMcp.Core.Services.ModelAnalyzerConstants;

namespace EfCoreMcp.Core.Services.Rules;

/// <summary>
/// EFMCP007: flags foreign keys created by convention as shadow properties, which usually indicates a mapping mistake.
/// </summary>
internal sealed class ShadowForeignKeyRule : IModelRule
{
    /// <inheritdoc />
    public ModelRuleDescriptor Descriptor { get; } = new(
        CodeEfMcp007, SeverityInfo, "Shadow foreign key",
        "Foreign key exists only as a shadow property, so the relationship can be set only through the navigation.");

    /// <inheritdoc />
    public IEnumerable<ModelFinding> Evaluate(ModelDescriptor model) =>
        model.Entities.SelectMany(e => e.Properties
            .Where(p => p.IsShadow && p.IsForeignKey)
            .Select(p => new ModelFinding(Descriptor.Severity, Descriptor.Id, e.Name, p.Name,
                $"Foreign key '{p.Name}' is a shadow property - the relationship is navigation-only.",
                "Shadow FKs work but cannot be set without loading the principal. Map an explicit FK property to allow setting the relationship by id.")));
}
