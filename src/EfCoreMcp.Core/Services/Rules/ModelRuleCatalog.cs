using EfCoreMcp.Core.Domain;

namespace EfCoreMcp.Core.Services.Rules;

/// <summary>
/// The registry of model validation rules, ordered by rule ID. It drives <see cref="ModelAnalyzer"/> and resolves rule suppressions.
/// </summary>
public static class ModelRuleCatalog
{
    internal static IReadOnlyList<IModelRule> Rules { get; } =
    [
        new KeylessEntityRule(),
        new UnboundedStringRule(),
        new DecimalPrecisionRule(),
        new OptionalCascadeRule(),
        new UnindexedForeignKeyRule(),
        new InverseNavigationRule(),
        new ShadowForeignKeyRule(),
        new MultipleCascadePathsRule(),
        new UnboundedStringKeyRule(),
        new CascadeFanOutRule(),
        new NavigationNullabilityRule(),
        new ConcurrencyTokenRule(),
        new TphDiscriminatorIndexRule(),
    ];

    /// <summary>
    /// Gets the descriptors of every model validation rule, ordered by rule ID. Use these to document or select rules.
    /// </summary>
    public static IReadOnlyList<ModelRuleDescriptor> All { get; } = Rules.Select(r => r.Descriptor).ToList();

    /// <summary>
    /// Validates and normalizes a set of rule IDs to suppress.
    /// </summary>
    /// <param name="ruleIds">The rule IDs to suppress; matching is case-insensitive. <see langword="null"/> suppresses nothing.</param>
    /// <returns>A case-insensitive set containing the suppressed rule IDs.</returns>
    /// <exception cref="ArgumentException">Thrown when an entry is <see langword="null"/> or empty, or does not name a known rule.</exception>
    public static HashSet<string> ResolveSuppressions(IReadOnlyCollection<string>? ruleIds)
    {
        var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in ruleIds ?? [])
        {
            ArgumentException.ThrowIfNullOrEmpty(id);
            if (!All.Any(rule => string.Equals(rule.Id, id, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException(
                    $"Unknown model rule '{id}'. Known rules: {string.Join(", ", All.Select(rule => rule.Id))}.",
                    nameof(ruleIds));
            resolved.Add(id);
        }
        return resolved;
    }
}
