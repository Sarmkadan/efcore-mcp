# Examples

## MCP client configuration

[`mcp-config.json`](mcp-config.json) shows a complete client registration. Drop the `mcpServers` block into your client's config (`.mcp.json` for Claude Code, `mcp.json` for Cursor). Only `--assembly` is required; `--context` becomes mandatory when the assembly contains more than one `DbContext`.

## What the server needs from your context

The target assembly must expose a `DbContext` that the server can construct, in one of these ways:

- an `IDesignTimeDbContextFactory<T>` in the same assembly (the same mechanism `dotnet ef` uses), or
- a public parameterless constructor whose `OnConfiguring` sets up the provider.

A minimal context that works out of the box:

```csharp
public class AppDbContext : DbContext
{
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnConfiguring(DbContextOptionsBuilder options) =>
        options.UseSqlite("Data Source=app.db");
}
```

Build the project (`dotnet build`), point `--assembly` at the produced dll, and ask your client things like "describe the model", "which entities reference Customer" or "run `SELECT COUNT(*) FROM Customers`".
