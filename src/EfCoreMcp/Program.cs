using EfCoreMcp;
using EfCoreMcp.Core.Abstractions;
using EfCoreMcp.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var cli = CliOptions.Parse(args);
var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Services.AddSingleton(cli.Connection);
builder.Services.AddSingleton<IDbContextProvider, DbContextProvider>();
builder.Services.AddSingleton<IModelIntrospector, ModelIntrospector>();
builder.Services.AddSingleton<ISqlQueryExecutor, SqlQueryExecutor>();
builder.Services.AddSingleton<IEntityQueryExecutor, EntityQueryExecutor>();
builder.Services.AddSingleton<IMigrationInspector, MigrationInspector>();
builder.Services.AddSingleton<ISchemaExplainer, SchemaExplainer>();
builder.Services.AddSingleton<IModelAnalyzer, ModelAnalyzer>();
builder.Services.AddSingleton<IRelationshipAnalyzer, RelationshipAnalyzer>();
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();
await builder.Build().RunAsync();
