# efcore-mcp

MCP server for EF Core. Points MCP clients at a compiled DbContext assembly and exposes the model as tools: entity/relationship/key/index introspection, human-readable schema explanations, a Mermaid relationship graph, guarded read-only SQL and entity queries, migration status and pending-model-change diffs. Unlike raw-SQL database servers it understands EF Core conventions - fluent configuration, navigations, shadow properties, migration snapshots.

```
dotnet run --project src/EfCoreMcp -- --assembly /path/to/MyApp.dll --context MyDbContext
```

Runs over stdio; register it in your MCP client config with the command above.
