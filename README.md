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
| `--max-rows <n>` | `EFCORE_MCP_MAX_ROWS` | Maximum rows a query tool may return (default: 10000, range 1-10000). |
| `--max-query-length <n>` | `EFCORE_MCP_MAX_QUERY_LENGTH` | Maximum length of a SQL statement in characters (default: 10000, range 1-1000000). |
| `--allowed-schemas <list>` | `EFCORE_MCP_ALLOWED_SCHEMAS` | Comma-separated list of schemas that query and relationship tools may touch (default: all schemas allowed). |
| `--max-analysis-depth <n>` | `EFCORE_MCP_MAX_ANALYSIS_DEPTH` | Maximum number of foreign-key hops explored by relationship analysis (default: 50, range 1-50). |

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
| `query_sql` | `sql` (required), `maxRows` (default 100, max limited by `--max-rows`), `timeoutSeconds` (default 30, max 300) | Runs a single `SELECT` or `WITH ... SELECT`. Other statements are rejected. The `maxRows` argument is capped by the server's `--max-rows` setting. The SQL length is limited by `--max-query-length`. Only tables in schemas allowed by `--allowed-schemas` may be accessed. |
| `query_entity` | `entityName` (required), `maxRows` (default 100, max limited by `--max-rows`), `skip`, `orderBy`, `orderDescending`, `filter`, `filterParameters`, `timeoutSeconds` (default 30, max 300) | Returns scalar properties only. The `maxRows` argument is capped by the server's `--max-rows` setting. Only entities in schemas allowed by `--allowed-schemas` may be accessed. See the known issues for `filter`. |
| `count_entity` | `entityName` (required) | Returns the row count. Only entities in schemas allowed by `--allowed-schemas` may be accessed. |
| `explain_sql` | `sql` (required), `timeoutSeconds` (default 30, max 300) | Returns an execution plan without running the query. The SQL length is limited by `--max-query-length`. Only tables in schemas allowed by `--allowed-schemas` may be accessed. |

The server also exposes model, migration, and analysis tools, such as `describe_model`, `describe_entity`, `migration_status`, and `validate_model`.

### Troubleshooting

- **`An error occurred invoking 'query_sql'.`** The client shows only a generic message. The reason is written to the server's stderr log. For example, `DELETE FROM Orders` fails with `Keyword 'delete' is not allowed in read-only mode.` Stdout is reserved for the MCP protocol, so read the logs on stderr.
- **`Multiple DbContext types found`** Add `--context <TypeName>` to choose one.

### Known issues

- **Dependencies of the target assembly are not resolved.** The server copies only the main DLL into a temporary folder and resolves dependencies only from that folder. An assembly that references an EF Core provider package, such as `Microsoft.EntityFrameworkCore.Sqlite`, fails with `FileNotFoundException`. Native libraries, such as SQLite's `e_sqlite3`, also fail to load. This means the sample above does not run end-to-end until the loader is fixed.
- **`query_entity` `filter` is validated but not applied.** Filter expressions are checked for allowed properties and operators, but the query runs without them. Do not rely on `filter` yet.

## Migrations and model inspection

The migration and model tools answer questions such as "which migrations are pending?", "what would my next migration change?", and "what does the `Order` entity look like?". They only read the loaded context. They do not change the database or your migration files.

The examples below use the shop sample from the worked example, with these migrations and model state:

- `InitialCreate` is applied to `shop.db`.
- `AddOrderStatus` exists in `Migrations/` but is not applied yet.
- `Customer.Phone` was added to the model after the last migration, so the model has drifted from the snapshot.

The server was started with `--assembly Shop.dll --connection "Data Source=/absolute/path/to/shop.db"`. Responses are pretty-printed here for readability. The server returns compact JSON. The outputs were captured with the provider assemblies visible to the loader, as described in Known issues.

### Migrations (`MigrationTools`)

The server does not apply migrations. It reports what has been applied and previews what a new migration would contain. To apply migrations, run `dotnet ef database update` or call `context.Database.Migrate()` in your application, then ask the client to check `migration_status` again.

Example prompt: *"Which migrations have not been applied yet, and does the model need a new one?"*

`migration_status` takes no parameters:

```json
{"name": "migration_status", "arguments": {}}
```

```json
{
  "applied": [
    "20261019054751_InitialCreate"
  ],
  "pending": [
    "20261019054804_AddOrderStatus"
  ],
  "hasPendingModelChanges": true
}
```

`applied` and `pending` are migration IDs as they appear in the `Migrations/` folder. `hasPendingModelChanges` is `true` when the current model differs from the last snapshot.

Example prompt: *"What would my next migration change? Flag anything that could lose data."*

`diff_pending_changes` takes no parameters:

```json
{"name": "diff_pending_changes", "arguments": {}}
```

```json
{
  "hasDifferences": true,
  "operations": [
    {
      "operationType": "AddColumn",
      "table": "Customers",
      "name": "Phone",
      "description": "AddColumn: Phone on Customers",
      "isDestructive": false
    }
  ]
}
```

`isDestructive` is `true` for `DropTable`, `DropColumn`, `DropIndex`, `DropForeignKey`, `DropPrimaryKey`, `AlterColumn`, `RenameTable`, and `RenameColumn` operations.

### Model inspection (`ModelTools`)

| Tool | Parameters | Returns |
| --- | --- | --- |
| `context_info` | none | JSON with the context type, assembly, provider, database name, and `canConnect`. |
| `list_contexts` | none | JSON array of DbContext types in the assembly. |
| `list_entities` | none | JSON array of entity names. |
| `describe_model` | none | JSON with every entity: properties, keys, foreign keys, navigations, and indexes. |
| `describe_entity` | `entityName` (required) | JSON for one entity. The name can be the CLR name, the short name, or the table name. |
| `explain_schema` | none | Markdown explanation of the whole model. |
| `explain_entity` | `entityName` (required) | Markdown explanation of one entity. |
| `relationship_graph` | none | Mermaid `erDiagram` of the entity relationships. |

Example prompt: *"Which entities are in the model, and how are they related?"*

```json
{"name": "list_entities", "arguments": {}}
```

```json
["Customer", "Order"]
```

```json
{"name": "relationship_graph", "arguments": {}}
```

```
erDiagram
    Customer {
        Int32 Id PK
        String Email
        String Name
        String Phone
    }
    Order {
        Int32 Id PK
        Int32 CustomerId FK
        DateTime PlacedAt
        String Status
        Decimal Total
    }
    Customer ||--o{ Order : "CustomerId"
```

Example prompt: *"Describe the Order entity, including its foreign keys."*

```json
{"name": "describe_entity", "arguments": {"entityName": "Order"}}
```

The response is shown compact, as the server returns it. Properties and foreign keys are abbreviated with `…`:

```json
{"name":"Order","clrType":"Shop.Order","tableName":"Orders","isOwned":false,"properties":[{"name":"Id","clrType":"Int32","columnName":"Id","columnType":"INTEGER","isNullable":false,"isPrimaryKey":true,"isForeignKey":false,"isShadow":false,"valueGenerated":"OnAdd","isConcurrencyToken":false},…,{"name":"Status","clrType":"String","columnName":"Status","columnType":"TEXT","isNullable":false,"isPrimaryKey":false,"isForeignKey":false,"isShadow":false,"valueGenerated":"Never","isConcurrencyToken":false},…],"primaryKey":{"name":"PK_Orders","properties":["Id"],"isPrimary":true},"alternateKeys":[],"foreignKeys":[{"constraintName":"FK_Orders_Customers_CustomerId","principalEntity":"Customer","dependentEntity":"Order","properties":["CustomerId"],"principalProperties":["Id"],"deleteBehavior":"Cascade","isRequired":true,"isUnique":false}],"navigations":[{"name":"Customer","targetEntity":"Customer","isCollection":false,"isOnDependent":true,"inverseName":"Orders"}],"indexes":[{"name":"IX_Orders_CustomerId","properties":["CustomerId"],"isUnique":false}]}
```

`describe_model` returns the same entity shape for every entity, wrapped in `contextName`, `providerName`, and `entities`. The sample is too large to reproduce here.

For a readable version of one entity, use `explain_entity`:

```json
{"name": "explain_entity", "arguments": {"entityName": "Order"}}
```

```markdown
# Order
Table: Orders

| Property | Type | Column | Nullable | Notes |
|---|---|---|---|---|
| Id | Int32 | Id (INTEGER) | no | PK, generated: OnAdd |
| CustomerId | Int32 | CustomerId (INTEGER) | no | FK |
| PlacedAt | DateTime | PlacedAt (TEXT) | no |  |
| Status | String | Status (TEXT) | no |  |
| Total | Decimal | Total (TEXT) | no |  |

Relationships:
- Order.(CustomerId) -> Customer.(Id) [Cascade, required]

Indexes:
- IX_Orders_CustomerId: (CustomerId)
```

`context_info` and `list_contexts` are useful when the assembly has more than one context. In the sample, which has one context, they return:

```json
{"name": "context_info", "arguments": {}}
```

```json
{
  "contextType": "Shop.ShopDbContext",
  "assemblyName": "Shop",
  "providerName": "Microsoft.EntityFrameworkCore.Sqlite",
  "database": "main",
  "canConnect": true
}
```

### Troubleshooting context loading

The server loads the context when a tool first needs it. A failure is returned to the client only as `An error occurred invoking '<tool>'.` The reason is written to the server's stderr log, as described in [Troubleshooting](#troubleshooting). The messages below come from `DbContextProvider`.

- **`DbContext 'Missing' not found. Available: ShopDbContext`**. The value of `--context` or `EFCORE_MCP_CONTEXT` does not match any concrete `DbContext` in the assembly. The match is case-sensitive and accepts the full name or the short name. Use one of the names in the `Available` list.
- **`No DbContext types found in the assembly.`**. The assembly has no concrete `DbContext`. This often means `--assembly` points at a library that references the context instead of the project that defines it. Use the built output of the project that contains the context.
- **`Cannot instantiate 'X': no parameterless constructor.`**. The context has no parameterless constructor, and the assembly has no `IDesignTimeDbContextFactory<X>`. Add one of the two.
- **`Multiple DbContext types found`**. Add `--context <TypeName>`. See [Troubleshooting](#troubleshooting).
- **Missing connection string.** Neither `--connection` nor `EFCORE_MCP_CONNECTION` is set, and the context does not configure a provider in `OnConfiguring`. The error is `No database provider has been configured for this DbContext.` `--connection` supplies only the connection string. It does not select a provider, so the same error appears even when `--connection` is set. Configure the provider in `OnConfiguring` or in an `IDesignTimeDbContextFactory<T>`.
- **Assembly path wrong.** The server copies the assembly to a temporary folder before loading it. A wrong path fails with a `System.IO` exception that names the path, such as `Could not find a part of the path '/tmp/nope/Missing.dll'.`
- **Assembly dependency not found.** A message such as `Could not load file or assembly 'Microsoft.EntityFrameworkCore.Sqlite, Version=…'` or `Unable to load shared library 'e_sqlite3'` means a provider assembly or its native library is not available to the loader. This is the limitation described in [Known issues](#known-issues).
- **Reload failure.** When the assembly changes on disk, the server reloads it. If the reload fails, it writes `Failed to reload assembly: …` to stderr and keeps using the previous context.

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

## License

Released under the [MIT License](LICENSE). Copyright (c) 2026 Vladyslav Zaiets.
