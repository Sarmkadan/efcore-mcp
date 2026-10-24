# Contributing to EfCoreMcp

Thanks for helping out. This guide covers building, testing, and submitting changes. For what the server does and how to configure it, see [README.md](README.md).

## Prerequisites

- .NET 10 SDK.
- Git.

## Clone and build

```bash
git clone <repository-url> efcore-mcp
cd efcore-mcp
dotnet restore
dotnet build -c Release     # or ./build.sh, which runs restore and a Release build
```

The solution file is `EfCoreMcp.slnx`. The library is in `src/EfCoreMcp.Core/` (no MCP dependency) and the host executable is in `src/EfCoreMcp/`.

Do not hand-edit `build/`. It holds committed build output.

## Run the tests

```bash
dotnet test                                    # all tests (xunit, SQLite in-memory)
dotnet test --filter "FullyQualifiedName~SqlGuardTests"   # a single test class
```

CI runs `dotnet test -c Release --no-build`, so run a Release build before pushing if you want to match it exactly.

## Coding conventions

- C# `latest`, with `Nullable` and `ImplicitUsings` enabled in every project.
- Use file-scoped namespaces and 4-space indentation.
- There is no `.editorconfig` or analyzer configuration in the repository yet. Match the style of the surrounding code, and run `dotnet format` manually on the files you change. Do not introduce new compiler warnings.
- Core services are stateless singletons behind interfaces in `src/EfCoreMcp.Core/Abstractions/Interfaces.cs`. Register new services in `src/EfCoreMcp/Program.cs`.
- Domain types are immutable `sealed record`s. JSON helpers go in `*JsonExtensions.cs`, validation in `*Validation.cs`, and string constants in `*Constants.cs`.
- MCP tool classes (`src/EfCoreMcp/Tools/`) only delegate to Core services. Put logic in Core.
- Every query goes through `SqlGuard` first. Only read-only SQL may run, and connection strings must be masked with `ConnectionStringSanitizer` before they are returned.
- Tests: one `<Type>Tests` class per service or tool, `[Fact]`/`[Theory]` with `InlineData`, builders such as `BlogBuilder` and `StoreBuilder` for fixtures, and XML doc comments on public members.
- Stdout is the MCP channel. Write logs to stderr only.

## Branches and commit messages

- Branch from `main`. Use short, descriptive branch names such as `feat/owned-type-tests` or `fix/sql-guard-cte`.
- Use a conventional commit prefix: `feat:`, `fix:`, `docs:`, `test:`, `ci:`, or `chore:`.
- Keep the subject line short and imperative, for example `fix: reject CTEs that contain writes`. Put the reasoning in the body when it is not obvious from the diff.
- Keep each commit focused. Unrelated changes belong in separate commits or PRs.

## Pull requests

1. Push your branch and open a pull request against `main`.
2. CI runs on every pull request to `main`. It restores, builds in Release, and runs the test suite. The PR must pass before it can be merged.
3. In the description, state what changed and why, list any behavior changes or new options, and say how you tested it. Link related issues.
4. Add or update tests for behavior changes. If you change a tool's name or description, update the matching `*Constants.cs` file and the README.
5. Keep the PR to one logical change where possible. Expect review comments and respond to them in the PR.
