# EfCoreMcp

An MCP server (stdio transport) that exposes EF Core model introspection, read-only SQL and entity queries, and migration diffing for a `DbContext` in your own assembly.

## Getting Started

### Prerequisites

- .NET 10 SDK.
- A built assembly that contains your `DbContext`. The server must be able to construct it, either through a public parameterless constructor whose `OnConfiguring` sets up the provider, or through an `IDesignTimeDbContextFactory<T>` in the same assembly.

### Install or run the server

Option 1: install it as a dotnet tool from a local package build. This was verified with `dotnet pack` and `dotnet tool install`:

```bash
dotnet pack src/EfCoreMcp/EfCoreMcp.csproj -c Release -o artifacts
dotnet tool install --global --add-source ./artifacts EfCoreMcp
```

The command is then available as `efcore-mcp`. Global tools install to `~/.dotnet/tools` (`%USERPROFILE%\.dotnet\tools` on Windows), so make sure that directory is on your `PATH`.

Option 2: run it from source without installing anything:

```bash
dotnet run --project src/EfCoreMcp -- --assembly /absolute/path/to/MyApp.dll --connection "Data Source=/absolute/path/to/app.db"
```

Both options start a process that waits for MCP messages on stdin. You normally let an MCP client launch it, as shown below.

### Command-line options

| Option | Environment fallback | Description |
| --- | --- | --- |
| `--assembly`, `-a <path>` | `EFCORE_MCP_ASSEMBLY` | Path to the assembly that contains the `DbContext`. Required. |
| `--context`, `-c <name>` | `EFCORE_MCP_CONTEXT` | `DbContext` type name. Required only when the assembly contains more than one context. |
| `--connection <string>` | `EFCORE_MCP_CONNECTION` | Connection string. Overrides the connection configured in the context. |
| `--provider <name>` | n/a | Accepted but currently has no effect. The provider comes from your `DbContext`. |

### Client configuration

Claude Code reads project-scoped servers from `.mcp.json` in the project root. Claude Desktop reads `mcpServers` from `claude_desktop_config.json`. The same block works in both:

```json
{
  "mcpServers": {
    "efcore": {
      "command": "efcore-mcp",
      "args": [
        "--assembly", "/absolute/path/to/MyApp/bin/Debug/net10.0/MyApp.dll",
        "--context", "AppDbContext",
        "--connection", "Data Source=/absolute/path/to/app.db"
      ]
    }
  }
}
```

Use absolute paths. A relative `Data Source` resolves against the server's working directory, which depends on the client that launches it. If your client cannot find `efcore-mcp`, replace `"command"` with the full path to the executable, such as `~/.dotnet/tools/efcore-mcp`.

To use `dotnet run` instead of the installed tool, set `"command": "dotnet"` and `"args": ["run", "--project", "/absolute/path/to/efcore-mcp/src/EfCoreMcp", "--", "--assembly", "...", "--connection", "..."]`.

### Worked example: querying a sample database

The sample is a small shop with two entities and a context that uses SQLite:

```csharp
// Models.cs
public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public List<Order> Orders { get; set; } = new();
}

public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public decimal Total { get; set; }
    public DateTime PlacedAt { get; set; }
}

// ShopDbContext.cs
using Microsoft.EntityFrameworkCore;

public class ShopDbContext : DbContext
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnConfiguring(DbContextOptionsBuilder options) =>
        options.UseSqlite("Data Source=shop.db");
}
```

Create the database and seed it with a console app that references the project. The sample output below assumes these rows:

```csharp
using var db = new ShopDbContext();
db.Database.EnsureDeleted();
db.Database.EnsureCreated();

var ada = new Customer { Name = "Ada Lovelace", Email = "ada@example.com" };
var linus = new Customer { Name = "Linus Torvalds", Email = "linus@example.com" };
db.Customers.AddRange(ada, linus);
db.SaveChanges();

db.Orders.AddRange(
    new Order { CustomerId = ada.Id, Total = 120.50m, PlacedAt = new DateTime(2026, 3, 1, 10, 0, 0) },
    new Order { CustomerId = ada.Id, Total = 42.00m, PlacedAt = new DateTime(2026, 3, 5, 14, 30, 0) },
    new Order { CustomerId = linus.Id, Total = 9.99m, PlacedAt = new DateTime(2026, 3, 7, 9, 15, 0) });
db.SaveChanges();
```

With the server configured as shown above, ask your client something like "Which customer has spent the most?" The client turns that into a `query_sql` call. The arguments it sends are:

```json
{
  "name": "query_sql",
  "arguments": {
    "sql": "SELECT c.Name, COUNT(o.Id) AS OrderCount, SUM(o.Total) AS TotalSpent FROM Customers c LEFT JOIN Orders o ON o.CustomerId = c.Id GROUP BY c.Id, c.Name ORDER BY TotalSpent DESC",
    "maxRows": 100
  }
}
```

The result is returned as a JSON text block. It is shown here pretty-printed. Every cell is a string, including numbers. `elapsedMilliseconds` varies from run to run.

```json
{
  "columns": ["Name", "OrderCount", "TotalSpent"],
  "rows": [
    ["Ada Lovelace", "2", "162.5"],
    ["Linus Torvalds", "1", "9.99"]
  ],
  "rowCount": 2,
  "truncated": false,
  "elapsedMilliseconds": 60
}
```

`query_entity` reads an entity set through EF Core. For example, `{"name": "query_entity", "arguments": {"entityName": "Customer", "maxRows": 2, "orderBy": "Name"}}` returns:

```json
{
  "columns": ["Id", "Email", "Name"],
  "rows": [
    ["1", "ada@example.com", "Ada Lovelace"],
    ["2", "linus@example.com", "Linus Torvalds"]
  ],
  "rowCount": 2,
  "truncated": false,
  "elapsedMilliseconds": 332
}
```

`count_entity` with `{"entityName": "Order"}` returns the count as text: `3`.

### Tool reference

| Tool | Parameters | Notes |
| --- | --- | --- |
| `query_sql` | `sql` (required), `maxRows` (default 100, max 10000), `timeoutSeconds` (default 30, max 300) | Runs a single `SELECT` or `WITH ... SELECT`. Other statements are rejected. |
| `query_entity` | `entityName` (required), `maxRows` (default 100, max 10000), `skip`, `orderBy`, `orderDescending`, `filter`, `filterParameters`, `timeoutSeconds` | Returns scalar properties only. See the known issues for `filter`. |
| `count_entity` | `entityName` (required) | Returns the row count. |
| `explain_sql` | `sql` (required), `timeoutSeconds` | Returns an execution plan without running the query. |

The server also exposes model, migration, and analysis tools, such as `describe_model`, `describe_entity`, `migration_status`, and `validate_model`.

### Troubleshooting

- **`An error occurred invoking 'query_sql'.`** The client shows only a generic message. The reason is written to the server's stderr log. For example, `DELETE FROM Orders` fails with `Keyword 'delete' is not allowed in read-only mode.` Stdout is reserved for the MCP protocol, so read the logs on stderr.
- **`Multiple DbContext types found`** Add `--context <TypeName>` to choose one.

### Known issues

- **Dependencies of the target assembly are not resolved.** The server copies only the main DLL into a temporary folder and resolves dependencies only from that folder. An assembly that references an EF Core provider package, such as `Microsoft.EntityFrameworkCore.Sqlite`, fails with `FileNotFoundException`. Native libraries, such as SQLite's `e_sqlite3`, also fail to load. This means the sample above does not run end-to-end until the loader is fixed.
- **`query_entity` `filter` is validated but not applied.** Filter expressions are checked for allowed properties and operators, but the query runs without them. Do not rely on `filter` yet.

## ModelFinding

`ModelFinding` is a sealed record that represents a single observation discovered while analyzing or validating an EF Core model. Because it is a record, findings are immutable, compare by value, and provide a readable `ToString()` representation out of the box. This makes findings easy to log, deduplicate, and pattern-match when building reporting or diagnostics on top of the analysis services.

Example usage:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using EfCoreMcp.Core.Domain;

public static class FindingReporter
{
    // Distinct() relies on the record's built-in value equality,
    // so duplicate findings are collapsed automatically.
    public static void LogAll(IEnumerable<ModelFinding> findings)
    {
        foreach (ModelFinding finding in findings.Distinct())
        {
            // Records provide a readable ToString() out of the box.
            Console.WriteLine(finding);
        }
    }
}
```

## Blog

The `Blog` type is a simple entity used in the test suite to represent a blog that contains a collection of `Post` entities. It exposes an identifier, a title, and a navigation property to its posts, while each `Post` references its parent `Blog` via `BlogId` and a navigation property.

```csharp
using System;
using System.Collections.Generic;
using EfCoreMcp.Tests; // Namespace where Blog, Post, and ModelIntrospectorTests live
using Microsoft.EntityFrameworkCore;

public static class BlogDemo
{
    public static void Main()
    {
        // Create a blog with two posts
        var blog = new Blog
        {
            Id = 1,
            Title = "My Blog",
            Posts = new List<Post>
            {
                new Post { Id = 1, Body = "First post", BlogId = 1, Blog = null },
                new Post { Id = 2, Body = "Second post", BlogId = 1, Blog = null }
            }
        };

        // Resolve navigation back‑references
        foreach (var post in blog.Posts)
        {
            post.Blog = blog;
        }

        // Example of using the test helper to get context information
        using var testHelper = new ModelIntrospectorTests();
        ContextInfo info = testHelper.GetContextInfo();
        Console.WriteLine($"Context: {info.ContextName}, Provider: {info.ProviderName}");

        // Clean up
        testHelper.Dispose();
    }
}
```

## Store

`Store` is a test entity that represents a retail location. It holds basic identifying information (`Id`, `Name`) and a collection of related `Sale` records. The type also participates in the test harness that can spin up an in‑memory `DbContext`, expose model metadata, and clean up resources.

```csharp
using System;
using System.Collections.Generic;
using EfCoreMcp.Tests; // Namespace where Store, Sale, Customer, and ModelAnalyzerTests live
using Microsoft.EntityFrameworkCore;

public static class StoreDemo
{
    public static void Main()
    {
        // Create a store with a couple of sales
        var store = new Store
        {
            Id = 1,
            Name = "Main Street Store",
            Sales = new List<Sale>()
        };

        var customer = new Customer { Id = 1, Name = "Alice" };

        var sale1 = new Sale
        {
            Id = 1,
            Amount = 99.95m,
            StoreId = store.Id,
            Store = store,
            CustomerId = customer.Id,
            Customer = customer
        };

        var sale2 = new Sale
        {
            Id = 2,
            Amount = 45.00m,
            StoreId = store.Id,
            Store = store,
            CustomerId = customer.Id,
            Customer = customer,
            Notes = "First purchase"
        };

        store.Sales.Add(sale1);
        store.Sales.Add(sale2);

        // Use the test harness to obtain a context and model information
        using var analyzer = new ModelAnalyzerTests();

        // Get the underlying DbContext (in‑memory for tests)
        DbContext ctx = analyzer.GetContext();

        // Retrieve high‑level context information
        ContextInfo ctxInfo = analyzer.GetContextInfo();
        Console.WriteLine($"Context: {ctxInfo.ContextName}, Provider: {ctxInfo.ProviderName}");

        // Describe the whole model
        var modelDescriptor = analyzer.DescribeModel();
        Console.WriteLine($"Model has {modelDescriptor.EntityCount} entities.");

        // List entity names in the model
        IReadOnlyList<string> entityNames = analyzer.ListEntityNames();
        Console.WriteLine("Entities in model: " + string.Join(", ", entityNames));

        // Attempt to describe a specific entity (Store)
        var storeDescriptor = analyzer.DescribeEntity?.Invoke(typeof(Store));
        if (storeDescriptor != null)
        {
            Console.WriteLine($"Store entity has {storeDescriptor.PropertyCount} properties.");
        }
        else
        {
            Console.WriteLine(analyzer.EntityNotFoundMessage);
        }

        // Clean up test resources
        analyzer.Dispose();
    }
}
```

The example demonstrates creating a `Store`, adding related `Sale` objects, and using the `ModelAnalyzerTests` helper to access the in‑memory `DbContext`, retrieve model metadata, list entity names, and safely dispose of resources. All members used (`Id`, `Name`, `Sales`, `Amount`, `StoreId`, `Store`, `CustomerId`, `Customer`, `Notes`, `GetContext`, `GetContextInfo`, `Dispose`, `DescribeModel`, `DescribeEntity`, `ListEntityNames`, `EntityNotFoundMessage`, `ModelAnalyzerTests`) are part of the public API.
