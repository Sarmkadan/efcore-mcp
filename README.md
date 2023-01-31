# efcore-mcp

An MCP server that lets any MCP client (Claude Code, Cursor, or anything else that speaks MCP) inspect and query your EF Core `DbContext` - model, relationships, data and migrations - straight from the compiled assembly.

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

## Tools

| Tool | Description |
| --- | --- |
| `context_info` | DbContext type, provider, database and connectivity check. |
| `list_entities` | Names of all entity types in the model. |
| `describe_model` | Full model: properties, keys, foreign keys, navigations, indexes. |
| `describe_entity` | Full structure of one entity by CLR, short or table name. |
| `explain_schema` | Human-readable markdown explanation of the whole model. |
| `explain_entity` | Human-readable explanation of one entity. |
| `relationship_graph` | Entity relationships as a Mermaid erDiagram. |
| `query_sql` | Guarded read-only SQL SELECT against the database. |
| `query_entity` | Read entity rows via EF Core with paging and ordering. |
| `count_entity` | Count rows in an entity set. |
| `migration_status` | Applied and pending migrations, plus model drift detection. |
| `diff_pending_changes` | Diff the model against the last migration snapshot. |

## Safety

`query_sql` accepts a single `SELECT` (or `WITH ... SELECT`) statement only. Comments and string literals are stripped before validation, multiple statements are rejected, and mutating keywords (`INSERT`, `UPDATE`, `DELETE`, `DROP`, `ALTER`, `EXEC`, `PRAGMA`, ...) are blocklisted. There is no write path.

## Requirements

- .NET 10 runtime
- A context assembly built against EF Core 10

## License

MIT
