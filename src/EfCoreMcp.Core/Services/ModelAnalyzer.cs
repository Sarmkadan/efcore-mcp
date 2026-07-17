using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;

namespace EfCoreMcp.Core.Services;

public sealed class ModelAnalyzer(IModelIntrospector introspector) : IModelAnalyzer
{
    public ModelValidationReport ValidateModel()
    {
        var model = introspector.DescribeModel();
        var findings = new List<ModelFinding>();
        foreach (var entity in model.Entities)
        {
            CheckPrimaryKey(entity, findings);
            CheckStringProperties(entity, findings);
            CheckDecimalProperties(entity, findings);
            CheckForeignKeys(entity, findings);
            CheckNavigations(entity, findings);
            CheckShadowForeignKeys(entity, findings);
        }
        CheckMultipleCascadePaths(model, findings);
        return new ModelValidationReport(
            model.Entities.Count,
            findings
                .OrderBy(f => SeverityRank(f.Severity))
                .ThenBy(f => f.Entity, StringComparer.Ordinal)
                .ToList());
    }

    public IReadOnlyList<IndexSuggestion> SuggestIndexes()
    {
        var model = introspector.DescribeModel();
        var suggestions = new List<IndexSuggestion>();
        foreach (var entity in model.Entities.Where(e => !e.IsOwned))
        {
            foreach (var fk in entity.ForeignKeys)
            {
                if (IsCoveredByIndexOrKey(entity, fk.Properties))
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

    private static void CheckPrimaryKey(EntityDescriptor entity, List<ModelFinding> findings)
    {
        if (entity.PrimaryKey is null && !entity.IsOwned)
            findings.Add(new ModelFinding(
                "warning", "EFMCP001", entity.Name, null,
                "Entity has no primary key (keyless entity type).",
                "Keyless entities cannot be tracked, updated or used as principals. If this is intentional (view/raw-SQL projection), ignore; otherwise add HasKey()."));
    }

    private static void CheckStringProperties(EntityDescriptor entity, List<ModelFinding> findings)
    {
        foreach (var p in entity.Properties)
        {
            if (p.ClrType is "String" && p.MaxLength is null && !p.IsPrimaryKey && !p.IsForeignKey)
                findings.Add(new ModelFinding(
                    "info", "EFMCP002", entity.Name, p.Name,
                    $"String property '{p.Name}' has no max length; most providers map it to an unbounded column (nvarchar(max)/text).",
                    "Set HasMaxLength(n) where a bound is known - unbounded columns block index creation on some providers and hide data-quality issues."));
        }
    }

    private static void CheckDecimalProperties(EntityDescriptor entity, List<ModelFinding> findings)
    {
        foreach (var p in entity.Properties)
        {
            if (p.ClrType is "Decimal" or "Decimal?" && p.Precision is null)
                findings.Add(new ModelFinding(
                    "warning", "EFMCP003", entity.Name, p.Name,
                    $"Decimal property '{p.Name}' has no explicit precision/column type; the provider default may silently truncate values.",
                    "Configure HasPrecision(p, s) or HasColumnType(\"decimal(p,s)\") to make rounding behaviour explicit."));
        }
    }

    private static void CheckForeignKeys(EntityDescriptor entity, List<ModelFinding> findings)
    {
        foreach (var fk in entity.ForeignKeys)
        {
            if (!fk.IsRequired && fk.DeleteBehavior == "Cascade")
                findings.Add(new ModelFinding(
                    "warning", "EFMCP004", entity.Name, string.Join(", ", fk.Properties),
                    $"Optional relationship to {fk.PrincipalEntity} is configured with cascade delete.",
                    "Deleting the principal will delete dependents that could have survived with a NULL FK. Use DeleteBehavior.SetNull or ClientSetNull unless deletion is intended."));
            if (!IsCoveredByIndexOrKey(entity, fk.Properties))
                findings.Add(new ModelFinding(
                    "info", "EFMCP005", entity.Name, string.Join(", ", fk.Properties),
                    $"Foreign key to {fk.PrincipalEntity} ({string.Join(", ", fk.Properties)}) is not covered by any index.",
                    "EF Core creates FK indexes by convention; a missing one means it was removed or the FK is composite with a reordered index. Add HasIndex over the FK columns."));
        }
    }

    private static void CheckNavigations(EntityDescriptor entity, List<ModelFinding> findings)
    {
        foreach (var nav in entity.Navigations)
        {
            if (nav.IsCollection && nav.InverseName is null)
                findings.Add(new ModelFinding(
                    "info", "EFMCP006", entity.Name, nav.Name,
                    $"Collection navigation '{nav.Name}' to {nav.TargetEntity} has no inverse reference navigation.",
                    "Without an inverse, loading dependents and fixing up the relationship requires the FK value; adding the reference navigation makes Include() chains and change tracking clearer."));
        }
    }

    private static void CheckShadowForeignKeys(EntityDescriptor entity, List<ModelFinding> findings)
    {
        foreach (var p in entity.Properties)
        {
            if (p.IsShadow && p.IsForeignKey)
                findings.Add(new ModelFinding(
                    "info", "EFMCP007", entity.Name, p.Name,
                    $"Foreign key '{p.Name}' is a shadow property - the relationship is navigation-only.",
                    "Shadow FKs work but cannot be set without loading the principal. Map an explicit FK property to allow setting the relationship by id."));
        }
    }

    private static void CheckMultipleCascadePaths(ModelDescriptor model, List<ModelFinding> findings)
    {
        foreach (var entity in model.Entities)
        {
            var cascadePrincipals = entity.ForeignKeys
                .Where(fk => fk.DeleteBehavior == "Cascade")
                .Select(fk => fk.PrincipalEntity)
                .Distinct()
                .ToList();
            if (cascadePrincipals.Count > 1)
                findings.Add(new ModelFinding(
                    "warning", "EFMCP008", entity.Name, null,
                    $"Entity is a cascade-delete target from multiple principals: {string.Join(", ", cascadePrincipals)}.",
                    "SQL Server rejects schemas with multiple cascade paths onto one table ('may cause cycles or multiple cascade paths'). Set one side to Restrict/NoAction and handle deletion in code."));
        }
    }

    private static bool IsCoveredByIndexOrKey(EntityDescriptor entity, IReadOnlyList<string> fkProperties)
    {
        return entity.Indexes.Any(ix => StartsWith(ix.Properties, fkProperties))
            || (entity.PrimaryKey is { } pk && StartsWith(pk.Properties, fkProperties))
            || entity.AlternateKeys.Any(k => StartsWith(k.Properties, fkProperties));
    }

    private static bool StartsWith(IReadOnlyList<string> haystack, IReadOnlyList<string> prefix) =>
        haystack.Count >= prefix.Count && prefix.SequenceEqual(haystack.Take(prefix.Count));

    private static int SeverityRank(string severity) => severity switch
    {
        "error" => 0,
        "warning" => 1,
        _ => 2
    };
}
