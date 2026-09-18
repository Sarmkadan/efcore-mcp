# EfCoreMcp

MCP server (.NET 10, stdio transport) that exposes EF Core model introspection, guarded read-only SQL/entity queries, schema explanation, model analysis and migration diffing for any user-supplied `DbContext` assembly. Published as a dotnet tool `efcore-mcp`.

## Build

```bash
dotnet restore
dotnet build -c Release            # or ./build.sh (restore + Release build)
dotnet pack src/EfCoreMcp/EfCoreMcp.csproj -c Release -o artifacts   # nupkg
dotnet run --project src/EfCoreMcp -- --assembly <path.dll> [--context <Name>] [--connection <cs>] [--provider sqlite|auto]
```

Env fallbacks for CLI flags: `EFCORE_MCP_ASSEMBLY`, `EFCORE_MCP_CONTEXT`, `EFCORE_MCP_CONNECTION`.

## Test

```bash
dotnet test                                    # all (xunit 2.9, SQLite in-memory)
dotnet test -c Release --no-build              # what CI runs
dotnet test --filter "FullyQualifiedName~SqlGuardTests"
```

CI: `.github/workflows/ci.yml` (build, test, pack on push/PR).

## Lint / format

No analyzers, `.editorconfig` or format step configured. `TreatWarningsAsErrors=false`. Use `dotnet format` manually if needed; do not introduce new warnings.

## Layout

- `EfCoreMcp.slnx` - solution (XML format).
- `src/EfCoreMcp.Core/` - library, no MCP dependency.
  - `Abstractions/Interfaces.cs` - all service interfaces (`IDbContextProvider`, `IModelIntrospector`, `ISqlQueryExecutor`, `IEntityQueryExecutor`, `IMigrationInspector`, `ISchemaExplainer`, `IModelAnalyzer`, `IRelationshipAnalyzer`).
  - `Domain/` - request/result records (`ModelDescriptors`, `QueryModels`, `MigrationModels`, `AnalysisModels`, `ContextConnection`).
  - `Services/` - implementations; `DbContextProvider` loads the target assembly and constructs the context (via `IDesignTimeDbContextFactory<T>` or parameterless ctor); `SqlGuard` rejects non-read-only SQL.
- `src/EfCoreMcp/` - host executable.
  - `Program.cs` - entry point: parses `CliOptions`, registers Core services as singletons, `AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()`. Logging goes to stderr (stdout is the MCP channel).
  - `CliOptions.cs` - argument/env parsing, `--help` throws `HelpRequestedException`.
  - `Tools/` - MCP tool classes: `ModelTools`, `QueryTools`, `MigrationTools`, `AnalysisTools`, each with an `I*Tools` interface and a `*Constants` file holding tool names/descriptions.
- `tests/EfCoreMcp.Tests/` - xunit tests; fixtures are `Blog`/`Post` and `Store`/`Sale`/`Customer` models on SQLite.
- `examples/` - MCP client config (`mcp-config.json`) and minimal DbContext example.
- `docs/` - per-type notes.
- `build/` - committed build output (do not edit by hand).

## Conventions

- C# `latest`, `Nullable` and `ImplicitUsings` enabled, file-scoped namespaces.
- Tool classes: `[McpServerToolType]` sealed class with primary-constructor DI; each method `[McpServerTool(Name = XConstants.FooName), Description(XConstants.FooDescription)]`; parameter descriptions also come from constants. Tools delegate to Core services and never contain logic themselves.
- Core services are stateless singletons behind interfaces in `Abstractions/Interfaces.cs`; add a new interface there and register it in `Program.cs`.
- Domain types are immutable `sealed record`s; JSON helpers live in `*JsonExtensions.cs`, validation in `*Validation.cs`, string constants in `*Constants.cs` alongside the type they serve.
- Queries: everything through `SqlGuard` first; only read-only SQL is executed. Connection strings are masked via `ConnectionStringSanitizer` before being returned.
- Tests: one class per service/tool named `<Type>Tests`, `[Fact]`/`[Theory]` with `InlineData`, builders (`BlogBuilder`, `StoreBuilder`) for fixtures, XML doc comments on public members.
- Commit messages: conventional prefix (`feat:`, `fix:`, `docs:`, `chore:`).
