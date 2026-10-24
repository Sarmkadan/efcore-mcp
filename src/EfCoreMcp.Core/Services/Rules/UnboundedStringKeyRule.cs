using EfCoreMcp.Core.Domain;
using static EfCoreMcp.Core.Services.ModelAnalyzerConstants;

namespace EfCoreMcp.Core.Services.Rules;

/// <summary>
/// EFMCP009: flags string properties with no max length that participate in a primary key, alternate key, foreign key or index.
/// On SQL Server such columns map to nvarchar(max), which cannot be a key or indexed column.
/// </summary>
internal sealed class UnboundedStringKeyRule : IModelRule
{
    /// <inheritdoc />
    public ModelRuleDescriptor Descriptor { get; } = new(
        CodeEfMcp009, SeverityWarning, "Unbounded string in key or index",
        "String property with no max length is a key, foreign key or index column, which databases such as SQL Server cannot index.");

    /// <inheritdoc />
    public IEnumerable<ModelFinding> Evaluate(ModelDescriptor model) =>
        model.Entities.SelectMany(e =>
        {
            var keyed = KeyCoverage.KeyAndIndexMembers(e);
            return e.Properties
                .Where(p => p.ClrType is ClrTypeString && p.MaxLength is null && keyed.Contains(p.Name))
                .Select(p => new ModelFinding(Descriptor.Severity, Descriptor.Id, e.Name, p.Name,
                    $"String property '{p.Name}' is part of a key or index but has no max length; on SQL Server it maps to nvarchar(max), which cannot be a key or indexed column.",
                    "Bound the column with HasMaxLength(n) and keep composite keys well under the 900-byte SQL Server index key limit."));
        });
}
