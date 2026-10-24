using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;
using EfCoreMcp.Core.Services.Rules;
using static EfCoreMcp.Core.Services.ModelAnalyzerConstants;

namespace EfCoreMcp.Core.Services;

/// <summary>
/// Analyzes an EF Core model using an <see cref="IModelIntrospector"/> to produce validation reports
/// and index suggestions. Each validation rule in <see cref="ModelRuleCatalog"/> inspects the model and
/// reports findings tagged with a stable rule ID that can be suppressed.
/// </summary>
/// <param name="introspector">The introspector that provides a description of the EF Core model.</param>
public sealed class ModelAnalyzer(IModelIntrospector introspector) : IModelAnalyzer
{
    /// <summary>
    /// Validates the EF Core model by running every rule in <see cref="ModelRuleCatalog"/>, except those suppressed.
    /// </summary>
    /// <param name="suppressedRuleIds">Rule IDs (for example <c>EFMCP012</c>) whose findings should be omitted; matching is case-insensitive.</param>
    /// <returns>
    /// A <see cref="ModelValidationReport"/> that contains the total number of entities examined and a
    /// list of <see cref="ModelFinding"/> objects ordered by severity, entity name and rule ID.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when a suppressed rule ID is empty or does not name a known rule.</exception>
    public ModelValidationReport ValidateModel(IReadOnlyCollection<string>? suppressedRuleIds = null)
    {
        var suppressed = ModelRuleCatalog.ResolveSuppressions(suppressedRuleIds);
        var model = introspector.DescribeModel();
        var findings = ModelRuleCatalog.Rules
            .Where(rule => !suppressed.Contains(rule.Descriptor.Id))
            .SelectMany(rule => rule.Evaluate(model))
            .OrderBy(f => SeverityRank(f.Severity))
            .ThenBy(f => f.Entity, StringComparer.Ordinal)
            .ThenBy(f => f.Code, StringComparer.Ordinal)
            .ToList();
        return new ModelValidationReport(model.Entities.Count, findings);
    }

    /// <summary>
    /// Generates index suggestions for foreign keys that are not covered by an existing index or key.
    /// This helps ensure that joins and cascade delete operations can be performed efficiently.
    /// </summary>
    /// <returns>
    /// A read‑only list of <see cref="IndexSuggestion"/> objects describing missing indexes for foreign keys.
    /// </returns>
    public IReadOnlyList<IndexSuggestion> SuggestIndexes()
    {
        var model = introspector.DescribeModel();
        var suggestions = new List<IndexSuggestion>();
        foreach (var entity in model.Entities.Where(e => !e.IsOwned))
        {
            foreach (var fk in entity.ForeignKeys)
            {
                if (KeyCoverage.IsCoveredByIndexOrKey(entity, fk.Properties))
                    continue;
                suggestions.Add(new IndexSuggestion(
                    entity.Name,
                    entity.TableName,
                    fk.Properties,
                    $"Foreign key to {fk.PrincipalEntity} has no covering index; joins and cascade lookups on this relationship scan the table."));
            }
        }
        return suggestions;
    }

    private static int SeverityRank(string severity) => severity switch
    {
        SeverityError => 0,
        SeverityWarning => 1,
        _ => 2
    };
}
