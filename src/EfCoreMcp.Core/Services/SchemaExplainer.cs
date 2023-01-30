using System.Text;
using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Domain;

namespace EfCoreMcp.Core.Services;

public sealed class SchemaExplainer(IModelIntrospector introspector) : ISchemaExplainer
{
    public string ExplainModel()
    {
        var model = introspector.DescribeModel();
        var sb = new StringBuilder();
        sb.AppendLine($"# {model.ContextName}");
        sb.AppendLine($"Provider: {model.ProviderName ?? "unknown"}");
        sb.AppendLine($"Entities: {model.Entities.Count}");
        sb.AppendLine();
        foreach (var entity in model.Entities)
            AppendEntity(sb, entity, headerLevel: 2);
        return sb.ToString();
    }

    public string ExplainEntity(string entityName)
    {
        var entity = introspector.DescribeEntity(entityName)
            ?? throw new InvalidOperationException($"Entity '{entityName}' not found in the model.");
        var sb = new StringBuilder();
        AppendEntity(sb, entity, headerLevel: 1);
        return sb.ToString();
    }

    public string RenderRelationshipGraph()
    {
        var model = introspector.DescribeModel();
        var sb = new StringBuilder();
        sb.AppendLine("erDiagram");
        foreach (var entity in model.Entities.Where(e => !e.IsOwned))
        {
            sb.AppendLine($"    {Sanitize(entity.Name)} {{");
            foreach (var prop in entity.Properties)
            {
                var marker = prop.IsPrimaryKey ? " PK" : prop.IsForeignKey ? " FK" : "";
                sb.AppendLine($"        {Sanitize(prop.ClrType.TrimEnd('?'))} {Sanitize(prop.Name)}{marker}");
            }
            sb.AppendLine("    }");
        }
        foreach (var entity in model.Entities)
        foreach (var fk in entity.ForeignKeys)
        {
            var cardinality = fk.IsUnique ? "||--o|" : "||--o{";
            sb.AppendLine($"    {Sanitize(fk.PrincipalEntity)} {cardinality} {Sanitize(fk.DependentEntity)} : \"{string.Join(", ", fk.Properties)}\"");
        }
        return sb.ToString();
    }

    private static void AppendEntity(StringBuilder sb, EntityDescriptor entity, int headerLevel)
    {
        sb.Append('#', headerLevel).Append(' ').AppendLine(entity.Name);
        var location = entity.Schema is null ? entity.TableName : $"{entity.Schema}.{entity.TableName}";
        sb.AppendLine($"Table: {location ?? "(not mapped)"}{(entity.IsOwned ? " (owned)" : "")}");
        if (entity.Comment is { } comment)
            sb.AppendLine(comment);
        sb.AppendLine();
        sb.AppendLine("| Property | Type | Column | Nullable | Notes |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var p in entity.Properties)
        {
            var notes = string.Join(", ", NotesFor(p));
            sb.AppendLine($"| {p.Name} | {p.ClrType} | {p.ColumnName} ({p.ColumnType}) | {(p.IsNullable ? "yes" : "no")} | {notes} |");
        }
        if (entity.ForeignKeys.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Relationships:");
            foreach (var fk in entity.ForeignKeys)
                sb.AppendLine($"- {fk.DependentEntity}.({string.Join(", ", fk.Properties)}) -> {fk.PrincipalEntity}.({string.Join(", ", fk.PrincipalProperties)}) [{fk.DeleteBehavior}{(fk.IsRequired ? ", required" : "")}]");
        }
        if (entity.Indexes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Indexes:");
            foreach (var ix in entity.Indexes)
                sb.AppendLine($"- {ix.Name}: ({string.Join(", ", ix.Properties)}){(ix.IsUnique ? " unique" : "")}{(ix.Filter is null ? "" : $" filter: {ix.Filter}")}");
        }
        sb.AppendLine();
    }

    private static IEnumerable<string> NotesFor(PropertyDescriptor p)
    {
        if (p.IsPrimaryKey) yield return "PK";
        if (p.IsForeignKey) yield return "FK";
        if (p.IsShadow) yield return "shadow";
        if (p.IsConcurrencyToken) yield return "concurrency token";
        if (p.MaxLength is { } len) yield return $"max {len}";
        if (p.ValueGenerated != "Never") yield return $"generated: {p.ValueGenerated}";
        if (p.DefaultValueSql is { } def) yield return $"default: {def}";
    }

    private static string Sanitize(string value) =>
        new([.. value.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_')]);
}
