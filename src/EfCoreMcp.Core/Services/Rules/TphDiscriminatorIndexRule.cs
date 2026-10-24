using EfCoreMcp.Core.Domain;
using static EfCoreMcp.Core.Services.ModelAnalyzerConstants;

namespace EfCoreMcp.Core.Services.Rules;

/// <summary>
/// EFMCP013: flags a table-per-hierarchy root whose discriminator column is not the leading column of any index.
/// Every query against the hierarchy filters on the discriminator. TPT and TPC hierarchies have no discriminator and are not checked.
/// </summary>
internal sealed class TphDiscriminatorIndexRule : IModelRule
{
    /// <inheritdoc />
    public ModelRuleDescriptor Descriptor { get; } = new(
        CodeEfMcp013, SeverityInfo, "Unindexed TPH discriminator",
        "Table-per-hierarchy root stores its discriminator without an index that leads with it.");

    /// <inheritdoc />
    public IEnumerable<ModelFinding> Evaluate(ModelDescriptor model)
    {
        foreach (var root in model.Entities)
        {
            if (root is not { BaseEntity: null, MappingStrategy: MappingStrategyTph, DiscriminatorProperty: { } discriminator })
                continue;
            if (!model.Entities.Any(e => e.BaseEntity == root.Name))
                continue;
            if (root.Indexes.Any(ix => ix.Properties.FirstOrDefault() == discriminator))
                continue;
            yield return new ModelFinding(Descriptor.Severity, Descriptor.Id, root.Name, discriminator,
                $"TPH hierarchy rooted at {root.Name} stores its discriminator '{discriminator}' without an index; queries on any type in the hierarchy filter on it.",
                $"Add an index that leads with the discriminator, for example modelBuilder.Entity<{root.Name}>().HasIndex(\"{discriminator}\").");
        }
    }
}
