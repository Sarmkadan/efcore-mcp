using EfCoreMcp.Core.Domain;
using static EfCoreMcp.Core.Services.ModelAnalyzerConstants;

namespace EfCoreMcp.Core.Services.Rules;

/// <summary>
/// EFMCP006: flags collection navigations that have no inverse reference navigation.
/// </summary>
internal sealed class InverseNavigationRule : IModelRule
{
    /// <inheritdoc />
    public ModelRuleDescriptor Descriptor { get; } = new(
        CodeEfMcp006, SeverityInfo, "Collection navigation without inverse",
        "Collection navigation has no inverse reference navigation, which makes Include chains and change tracking harder to follow.");

    /// <inheritdoc />
    public IEnumerable<ModelFinding> Evaluate(ModelDescriptor model) =>
        model.Entities.SelectMany(e => e.Navigations
            .Where(n => n.IsCollection && n.InverseName is null)
            .Select(n => new ModelFinding(Descriptor.Severity, Descriptor.Id, e.Name, n.Name,
                $"Collection navigation '{n.Name}' to {n.TargetEntity} has no inverse reference navigation.",
                "Without an inverse, loading dependents and fixing up the relationship requires the FK value; adding the reference navigation makes Include() chains and change tracking clearer.")));
}
