# efcore-mcp

An MCP server that lets any MCP client (Claude Code, Cursor, or anything else that speaks MCP) inspect, query and analyze your EF Core `DbContext` - model, relationships, data, migrations and design pitfalls - straight from the compiled assembly.

Existing MCP database servers speak raw SQL and know nothing about EF Core conventions - fluent configuration, navigation properties, shadow properties, migration snapshots. This one loads your `DbContext` and answers from the actual EF Core model.

## Install

```
dotnet tool install -g EfCoreMcp
```

## Quickstart

Point the server at your compiled context assembly and register it in your MCP client:

```json
{
  "mcpServers": {
    "efcore": {
      "command": "efcore-mcp",
      "args": [
        "--assembly", "/path/to/bin/Debug/net10.0/MyApp.dll",
        "--context", "MyDbContext"
      ]
    }
  }
}
```

`--context` is optional when the assembly contains a single `DbContext`. Use `--connection` to override the connection string and `--provider` to force a provider. The same values can come from `EFCORE_MCP_ASSEMBLY`, `EFCORE_MCP_CONTEXT` and `EFCORE_MCP_CONNECTION`. The server runs over stdio.

### How the context is created

The server loads your assembly into an isolated `AssemblyLoadContext` (sibling DLLs next to the assembly are resolved automatically), then instantiates the context via:

1. an `IDesignTimeDbContextFactory<TContext>` found in the same assembly, if one exists;
2. otherwise a parameterless constructor.

If your context only has a `DbContextOptions` constructor, add a design-time factory - the same one `dotnet ef` uses:

```csharp
public class MyContextFactory : IDesignTimeDbContextFactory<MyDbContext>
{
    public MyDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<MyDbContext>()
            .UseNpgsql("Host=localhost;Database=myapp")
            .Options);
}
```

`--connection` overrides whatever connection string the factory or `OnConfiguring` set, so you can point the same model at a staging copy of the database.

## Tools

### Model inspection

| Tool | Description |
| --- | --- |
| `context_info` | DbContext type, provider, database and connectivity check. |
| `list_entities` | Names of all entity types in the model. |
| `describe_model` | Full model: properties, keys, foreign keys, navigations, indexes. |
| `describe_entity` | Full structure of one entity by CLR, short or table name. |
| `explain_schema` | Human-readable markdown explanation of the whole model. |
| `explain_entity` | Human-readable explanation of one entity. |
| `relationship_graph` | Entity relationships as a Mermaid erDiagram. |

### Analysis

| Tool | Description |
| --- | --- |
| `validate_model` | Scan the model for common EF Core pitfalls (see below). |
| `suggest_indexes` | Suggest missing indexes based on foreign keys in the model. |
| `explain_relationship` | Shortest foreign-key chain between two entities, with cardinality and delete behavior per hop. |
| `dependency_order` | Topological sort of entities: safe insert order, safe delete order, cyclic entities. |

### Data

| Tool | Description |
| --- | --- |
| `query_sql` | Guarded read-only SQL SELECT against the database. |
| `query_entity` | Read entity rows via EF Core with paging and ordering. |
| `count_entity` | Count rows in an entity set. |

### Migrations

| Tool | Description |
| --- | --- |
| `migration_status` | Applied and pending migrations, plus model drift detection. |
| `diff_pending_changes` | Diff the model against the last migration snapshot. |

## Model validation rules

`validate_model` returns findings with a severity, a stable code, the affected entity/property, and a concrete recommendation:

| Code | Severity | Finds |
| --- | --- | --- |
| `EFMCP001` | warning | Entity without a primary key (keyless entity type). |
| `EFMCP002` | info | String property without `HasMaxLength` - maps to `nvarchar(max)`/`text`. |
| `EFMCP003` | warning | Decimal property without explicit precision - provider default may silently truncate. |
| `EFMCP004` | warning | Optional relationship configured with cascade delete. |
| `EFMCP005` | info | Foreign key not covered by any index (conventional FK index removed). |
| `EFMCP006` | info | Collection navigation without an inverse reference navigation. |
| `EFMCP007` | info | Shadow foreign key - relationship can only be set by loading the principal. |
| `EFMCP008` | warning | Entity is a cascade-delete target from multiple principals (SQL Server rejects multiple cascade paths). |

## Example session

Typical prompts once the server is registered:

- *"How is `Invoice` related to `Warehouse`?"* → `explain_relationship` walks the FK graph: `Invoice.(OrderId) references Order -> Order.(WarehouseId) references Warehouse`.
- *"In what order can I seed these tables?"* → `dependency_order` returns a topological insert order and flags cyclic entities that need two-phase inserts.
- *"Anything wrong with my model?"* → `validate_model` returns coded findings such as `EFMCP003 Sale.Amount: decimal without precision`.
- *"Show me the 10 latest orders"* → `query_entity` with `orderBy: "CreatedAt", orderDescending: true, take: 10`.
- *"Did the model drift from the last migration?"* → `migration_status` reports applied/pending migrations and pending model changes; `diff_pending_changes` lists the exact operations a new migration would contain.

Entity name arguments accept the CLR name, short name or table name, case-insensitively. Typos get a `Did you mean: ...?` suggestion instead of a bare failure.

## Safety

`query_sql` accepts a single `SELECT` (or `WITH ... SELECT`) statement only. Comments and string literals are stripped before validation, multiple statements are rejected, and mutating keywords (`INSERT`, `UPDATE`, `DELETE`, `DROP`, `ALTER`, `EXEC`, `PRAGMA`, ...) are blocklisted. There is no write path.

## Requirements

- .NET 10 runtime
- A context assembly built against EF Core 10

## Development

```
dotnet build
dotnet test
```

The solution splits into `EfCoreMcp.Core` (services, no MCP dependency, fully unit-testable) and `EfCoreMcp` (CLI + MCP tool surface). Analyzers operate on plain descriptor records produced by `ModelIntrospector`, so new rules can be tested against hand-built models without a database.

## License

MIT
