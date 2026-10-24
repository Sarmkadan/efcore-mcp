---
name: Bug report
about: Report a problem with efcore-mcp
title: "[Bug]: "
labels: ["bug"]
---

## Description

A clear and concise description of the bug.

## Steps to reproduce

1. Build the target assembly (for example `dotnet build -c Release`)
2. Start the server with `dotnet run --project src/EfCoreMcp -- --assembly <path.dll> ...`
3. Call the tool `...` with arguments `...`
4. See the error

If possible, include a minimal `DbContext` or a sample model that reproduces the problem.

## Expected behaviour

What you expected to happen.

## Actual behaviour

What actually happened. Include the full error message or the tool response if there was one.

## Environment

- efcore-mcp version (or commit):
- .NET SDK / runtime version (`dotnet --version`):
- EF Core version (for example `10.0.1`):
- Database provider and version (for example `Microsoft.EntityFrameworkCore.Sqlite 10.0.1`, SQL Server 2022):
- Operating system (name and version):
- MCP client (for example Claude Code, VS Code, Claude Desktop):

## Logs

Paste the relevant output. The server writes logs to stderr. Remove connection strings and any other secrets before posting.

```text
<logs here>
```
