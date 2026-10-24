using EfCoreMcp.Core.Domain;
using static EfCoreMcp.Core.Services.ModelAnalyzerConstants;

namespace EfCoreMcp.Core.Services.Rules;

/// <summary>
/// EFMCP005: flags foreign keys that no index or key leads with. Column order inside a composite foreign key is ignored.
/// </summary>
internal sealed class UnindexedForeignKeyRule : IModelRule
{
    /// <inheritdoc />
    public ModelRuleDescriptor Descriptor { get; } = new(
        CodeEfMcp005, SeverityInfo, "Unindexed foreign key",
        "Foreign key columns are not led by any non-filtered index, primary key or alternate key.");

    /// <inheritdoc />
    public IEnumerable<ModelFinding> Evaluate(ModelDescriptor model) =>
        model.Entities.SelectMany(e => e.ForeignKeys
            .Where(fk => !KeyCoverage.IsCoveredByIndexOrKey(e, fk.Properties))
            .Select(fk => new ModelFinding(Descriptor.Severity, Descriptor.Id, e.Name, string.Join(", ", fk.Properties),
                $"Foreign key to {fk.PrincipalEntity} ({string.Join(", ", fk.Properties)}) is not covered by any index.",
                "EF Core creates FK indexes by convention; a missing one means it was removed, or the only index is filtered or does not lead with the FK columns. Add HasIndex over the FK columns.")));
}
