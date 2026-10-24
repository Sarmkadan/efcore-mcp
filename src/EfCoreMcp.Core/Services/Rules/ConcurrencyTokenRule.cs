using EfCoreMcp.Core.Domain;
using static EfCoreMcp.Core.Services.ModelAnalyzerConstants;

namespace EfCoreMcp.Core.Services.Rules;

/// <summary>
/// EFMCP012: flags keyed root entity types with no concurrency token, where concurrent updates silently overwrite each other.
/// Derived types inherit their root's tokens, so only root entity types are checked.
/// </summary>
internal sealed class ConcurrencyTokenRule : IModelRule
{
    /// <inheritdoc />
    public ModelRuleDescriptor Descriptor { get; } = new(
        CodeEfMcp012, SeverityInfo, "Missing concurrency token",
        "Keyed entity has no concurrency token, so concurrent updates to the same row are last-writer-wins.");

    /// <inheritdoc />
    public IEnumerable<ModelFinding> Evaluate(ModelDescriptor model) =>
        model.Entities
            .Where(e => e.PrimaryKey is not null && e.BaseEntity is null && !e.Properties.Any(p => p.IsConcurrencyToken))
            .Select(e => new ModelFinding(Descriptor.Severity, Descriptor.Id, e.Name, null,
                "Entity has no concurrency token; concurrent updates to the same row silently overwrite each other.",
                "Add a rowversion ([Timestamp] / IsRowVersion()) or an IsConcurrencyToken() column so UPDATE statements verify the value that was read."));
}
