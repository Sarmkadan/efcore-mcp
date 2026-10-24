using EfCoreMcp.Core.Domain;
using static EfCoreMcp.Core.Services.ModelAnalyzerConstants;

namespace EfCoreMcp.Core.Services.Rules;

/// <summary>
/// EFMCP003: flags decimal properties with no explicit precision, where the provider default may truncate values.
/// </summary>
internal sealed class DecimalPrecisionRule : IModelRule
{
    /// <inheritdoc />
    public ModelRuleDescriptor Descriptor { get; } = new(
        CodeEfMcp003, SeverityWarning, "Decimal without precision",
        "Decimal property has no explicit precision or column type, so the provider default may silently round or truncate values.");

    /// <inheritdoc />
    public IEnumerable<ModelFinding> Evaluate(ModelDescriptor model) =>
        model.Entities.SelectMany(e => e.Properties
            .Where(p => p.ClrType is ClrTypeDecimal or ClrTypeDecimalNullable && p.Precision is null)
            .Select(p => new ModelFinding(Descriptor.Severity, Descriptor.Id, e.Name, p.Name,
                $"Decimal property '{p.Name}' has no explicit precision/column type; the provider default may silently truncate values.",
                "Configure HasPrecision(p, s) or HasColumnType(\"decimal(p,s)\") to make rounding behaviour explicit.")));
}
