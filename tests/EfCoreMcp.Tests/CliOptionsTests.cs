using System;
using Xunit;

namespace EfCoreMcp.Tests;

[CollectionDefinition(nameof(CliOptionsCollection), DisableParallelization = true)]
public sealed class CliOptionsCollection;

[Collection(nameof(CliOptionsCollection))]
public class CliOptionsTests
{
    [Fact]
    public void Parse_LongOptions_PopulatesConnectionOptions()
    {
        var options = CliOptions.Parse([
            "--assembly", "app.dll",
            "--context", "AppContext",
            "--connection", "Server=localhost;Database=app",
            "--provider", "sqlserver"
        ]);

        Assert.Equal("app.dll", options.Connection.AssemblyPath);
        Assert.Equal("AppContext", options.Connection.ContextTypeName);
        Assert.Equal("Server=localhost;Database=app", options.Connection.ConnectionString);
        Assert.Equal("sqlserver", options.Connection.Provider);
    }

    [Fact]
    public void Parse_ShortOptions_AreEquivalentToLongOptions()
    {
        var longOptions = CliOptions.Parse(["--assembly", "app.dll", "--context", "AppContext"]);
        var shortOptions = CliOptions.Parse(["-a", "app.dll", "-c", "AppContext"]);

        Assert.Equal(longOptions.Connection, shortOptions.Connection);
    }

    [Fact]
    public void Parse_WithoutProvider_DefaultsToAuto()
    {
        var options = CliOptions.Parse(["--assembly", "app.dll"]);

        Assert.Equal("auto", options.Connection.Provider);
    }

    [Fact]
    public void Parse_WithoutAssembly_ThrowsHelpfulArgumentException()
    {
        WithEnvironmentVariables(null, null, null, () =>
        {
            var exception = Assert.Throws<ArgumentException>(() => CliOptions.Parse([]));

            Assert.Contains("--assembly", exception.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Parse_FlagWithoutValueAtEnd_IsIgnored()
    {
        WithEnvironmentVariables("environment.dll", "EnvironmentContext", null, () =>
        {
            var options = CliOptions.Parse(["--context"]);

            Assert.Equal("environment.dll", options.Connection.AssemblyPath);
            Assert.Equal("EnvironmentContext", options.Connection.ContextTypeName);
        });
    }

    [Fact]
    public void Parse_UsesEnvironmentVariableFallbacks()
    {
        WithEnvironmentVariables(
            "environment.dll",
            "EnvironmentContext",
            "Host=localhost;Database=environment",
            () =>
            {
                var options = CliOptions.Parse([]);

                Assert.Equal("environment.dll", options.Connection.AssemblyPath);
                Assert.Equal("EnvironmentContext", options.Connection.ContextTypeName);
                Assert.Equal("Host=localhost;Database=environment", options.Connection.ConnectionString);
            });
    }

    [Theory]
    [InlineData("not-a-connection-string", "valid format")]
    [InlineData("Server=localhost;drop table Users", "suspicious pattern")]
    public void Parse_WithInvalidConnection_ThrowsArgumentException(string connection, string expectedMessage)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CliOptions.Parse(["--assembly", "app.dll", "--connection", connection]));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static void WithEnvironmentVariables(
        string? assembly,
        string? context,
        string? connection,
        Action action)
    {
        const string assemblyVariable = "EFCORE_MCP_ASSEMBLY";
        const string contextVariable = "EFCORE_MCP_CONTEXT";
        const string connectionVariable = "EFCORE_MCP_CONNECTION";
        var originalAssembly = Environment.GetEnvironmentVariable(assemblyVariable);
        var originalContext = Environment.GetEnvironmentVariable(contextVariable);
        var originalConnection = Environment.GetEnvironmentVariable(connectionVariable);

        try
        {
            Environment.SetEnvironmentVariable(assemblyVariable, assembly);
            Environment.SetEnvironmentVariable(contextVariable, context);
            Environment.SetEnvironmentVariable(connectionVariable, connection);
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(assemblyVariable, originalAssembly);
            Environment.SetEnvironmentVariable(contextVariable, originalContext);
            Environment.SetEnvironmentVariable(connectionVariable, originalConnection);
        }
    }
}
