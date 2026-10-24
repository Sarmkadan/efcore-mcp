using EfCoreMcp.Core.Domain;
using static EfCoreMcp.Core.Services.ModelAnalyzerConstants;

namespace EfCoreMcp.Core.Services.Rules;

/// <summary>
/// EFMCP002: flags string properties with no max length that are not part of any key or index (see EFMCP009 for those).
/// </summary>
internal sealed class UnboundedStringRule : IModelRule
{
    /// <inheritdoc />
    public ModelRuleDescriptor Descriptor { get; } = new(
        CodeEfMcp002, SeverityInfo, "Unbounded string column",
        "String property has no max length, so most providers map it to an unbounded column (nvarchar(max)/text).");

    /// <inheritdoc />
    public IEnumerable<ModelFinding> Evaluate(ModelDescriptor model) =>
        model.Entities.SelectMany(e =>
        {
            var keyed = KeyCoverage.KeyAndIndexMembers(e);
            return e.Properties
                .Where(p => p.ClrType is ClrTypeString && p.MaxLength is null && !keyed.Contains(p.Name))
                .Select(p => new ModelFinding(Descriptor.Severity, Descriptor.Id, e.Name, p.Name,
                    $"String property '{p.Name}' has no max length; most providers map it to an unbounded column (nvarchar(max)/text).",
                    "Set HasMaxLength(n) where a bound is known - unbounded columns hide data-quality issues and can block indexing on some providers."));
        });
}
