using EfCoreMcp.Core.Domain;
using static EfCoreMcp.Core.Services.ModelAnalyzerConstants;

namespace EfCoreMcp.Core.Services.Rules;

/// <summary>
/// EFMCP011: flags a non-nullable reference navigation whose foreign key is optional. Such a navigation is
/// declared as always present, but EF leaves it null when the FK column is NULL.
/// </summary>
internal sealed class NavigationNullabilityRule : IModelRule
{
    /// <inheritdoc />
    public ModelRuleDescriptor Descriptor { get; } = new(
        CodeEfMcp011, SeverityWarning, "Required navigation over optional foreign key",
        "Non-nullable reference navigation is backed by an optional foreign key, so it can be null at runtime.");

    /// <inheritdoc />
    public IEnumerable<ModelFinding> Evaluate(ModelDescriptor model)
    {
        foreach (var entity in model.Entities)
        {
            foreach (var nav in entity.Navigations.Where(n => n.IsOnDependent && !n.IsCollection && n.IsClrNullable == false))
            {
                var keys = entity.ForeignKeys.Where(fk => fk.PrincipalEntity == nav.TargetEntity).ToList();
                if (keys.Count == 0 || keys.Any(fk => fk.IsRequired))
                    continue;
                var columns = string.Join(", ", keys.SelectMany(fk => fk.Properties).Distinct(StringComparer.Ordinal));
                yield return new ModelFinding(Descriptor.Severity, Descriptor.Id, entity.Name, nav.Name,
                    $"Reference navigation '{nav.Name}' to {nav.TargetEntity} is declared non-nullable, but its foreign key ({columns}) is optional and can be NULL.",
                    "Make the foreign key required (non-nullable property or IsRequired()), or declare the navigation nullable. Otherwise a row with a NULL FK loads with a null navigation and code that dereferences it throws.");
            }
        }
    }
}
