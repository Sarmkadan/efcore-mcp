using EfCoreMcp;
using EfCoreMcp.Core.Domain;
using Xunit;

namespace EfCoreMcp.Tests;

/// <summary>
/// Tests that the runtime limit flags and environment fallbacks are parsed and validated at startup.
/// </summary>
[Collection(nameof(CliOptionsCollection))]
public class RuntimeLimitsCliTests
{
    private static readonly string[] LimitVariables =
    [
        "EFCORE_MCP_MAX_ROWS",
        "EFCORE_MCP_MAX_QUERY_LENGTH",
        "EFCORE_MCP_ALLOWED_SCHEMAS",
        "EFCORE_MCP_MAX_ANALYSIS_DEPTH"
    ];

    [Fact]
    public void Parse_NoLimitOptions_UsesDefaults()
    {
        WithEnvironment(new Dictionary<string, string?>(), () =>
        {
            var limits = CliOptions.Parse(["--assembly", "app.dll"]).Limits;

            Assert.Equal(RuntimeLimitsOptions.DefaultMaxRows, limits.MaxRows);
            Assert.Equal(RuntimeLimitsOptions.DefaultMaxQueryLength, limits.MaxQueryLength);
            Assert.Equal(RuntimeLimitsOptions.DefaultMaxAnalysisDepth, limits.MaxAnalysisDepth);
            Assert.Null(limits.AllowedSchemas);
        });
    }

    [Theory]
    [InlineData("1")]
    [InlineData("10000")]
    public void Parse_MaxRowsAtBoundary_IsAccepted(string value)
    {
        WithEnvironment(new Dictionary<string, string?>(), () =>
        {
            var limits = CliOptions.Parse(["--assembly", "app.dll", "--max-rows", value]).Limits;

            Assert.Equal(int.Parse(value), limits.MaxRows);
        });
    }

    [Theory]
    [InlineData("0", "--max-rows")]
    [InlineData("-3", "--max-rows")]
    [InlineData("10001", "--max-rows")]
    [InlineData("abc", "--max-rows must be an integer")]
    public void Parse_MaxRowsOutOfRange_FailsAtStartup(string value, string expected)
    {
        WithEnvironment(new Dictionary<string, string?>(), () =>
        {
            var ex = Assert.ThrowsAny<ArgumentException>(() => CliOptions.Parse(["--assembly", "app.dll", "--max-rows", value]));
            Assert.Contains(expected, ex.Message, StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData("1")]
    [InlineData("1000000")]
    public void Parse_MaxQueryLengthAtBoundary_IsAccepted(string value)
    {
        WithEnvironment(new Dictionary<string, string?>(), () =>
        {
            var limits = CliOptions.Parse(["--assembly", "app.dll", "--max-query-length", value]).Limits;

            Assert.Equal(int.Parse(value), limits.MaxQueryLength);
        });
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1000001")]
    public void Parse_MaxQueryLengthOutOfRange_FailsAtStartup(string value)
    {
        WithEnvironment(new Dictionary<string, string?>(), () =>
        {
            var ex = Assert.ThrowsAny<ArgumentException>(() => CliOptions.Parse(["--assembly", "app.dll", "--max-query-length", value]));
            Assert.Contains("--max-query-length", ex.Message, StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData("1")]
    [InlineData("50")]
    public void Parse_MaxAnalysisDepthAtBoundary_IsAccepted(string value)
    {
        WithEnvironment(new Dictionary<string, string?>(), () =>
        {
            var limits = CliOptions.Parse(["--assembly", "app.dll", "--max-analysis-depth", value]).Limits;

            Assert.Equal(int.Parse(value), limits.MaxAnalysisDepth);
        });
    }

    [Theory]
    [InlineData("0")]
    [InlineData("51")]
    public void Parse_MaxAnalysisDepthOutOfRange_FailsAtStartup(string value)
    {
        WithEnvironment(new Dictionary<string, string?>(), () =>
        {
            var ex = Assert.ThrowsAny<ArgumentException>(() => CliOptions.Parse(["--assembly", "app.dll", "--max-analysis-depth", value]));
            Assert.Contains("--max-analysis-depth", ex.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Parse_AllowedSchemas_SplitsAndTrimsEntries()
    {
        WithEnvironment(new Dictionary<string, string?>(), () =>
        {
            var limits = CliOptions.Parse(["--assembly", "app.dll", "--allowed-schemas", "dbo, audit"]).Limits;

            Assert.Equal(["dbo", "audit"], limits.AllowedSchemas);
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("dbo,,audit")]
    [InlineData("dbo,bad name")]
    public void Parse_AllowedSchemasInvalid_FailsAtStartup(string value)
    {
        WithEnvironment(new Dictionary<string, string?>(), () =>
        {
            Assert.ThrowsAny<ArgumentException>(() => CliOptions.Parse(["--assembly", "app.dll", "--allowed-schemas", value]));
        });
    }

    [Fact]
    public void Parse_EnvironmentFallbacks_ApplyWhenFlagsAbsent()
    {
        WithEnvironment(
            new Dictionary<string, string?>
            {
                ["EFCORE_MCP_MAX_ROWS"] = "500",
                ["EFCORE_MCP_MAX_QUERY_LENGTH"] = "2000",
                ["EFCORE_MCP_ALLOWED_SCHEMAS"] = "sales",
                ["EFCORE_MCP_MAX_ANALYSIS_DEPTH"] = "7"
            },
            () =>
            {
                var limits = CliOptions.Parse(["--assembly", "app.dll"]).Limits;

                Assert.Equal(500, limits.MaxRows);
                Assert.Equal(2000, limits.MaxQueryLength);
                Assert.Equal(["sales"], limits.AllowedSchemas);
                Assert.Equal(7, limits.MaxAnalysisDepth);
            });
    }

    [Fact]
    public void Parse_FlagOverridesEnvironment()
    {
        WithEnvironment(
            new Dictionary<string, string?> { ["EFCORE_MCP_MAX_ROWS"] = "500" },
            () =>
            {
                var limits = CliOptions.Parse(["--assembly", "app.dll", "--max-rows", "20"]).Limits;

                Assert.Equal(20, limits.MaxRows);
            });
    }

    [Fact]
    public void Parse_EmptyEnvironmentVariable_IsTreatedAsUnset()
    {
        WithEnvironment(
            new Dictionary<string, string?> { ["EFCORE_MCP_MAX_ROWS"] = "" },
            () =>
            {
                var limits = CliOptions.Parse(["--assembly", "app.dll"]).Limits;

                Assert.Equal(RuntimeLimitsOptions.DefaultMaxRows, limits.MaxRows);
            });
    }

    [Fact]
    public void Parse_InvalidEnvironmentValue_FailsAtStartup()
    {
        WithEnvironment(
            new Dictionary<string, string?> { ["EFCORE_MCP_MAX_ANALYSIS_DEPTH"] = "0" },
            () =>
            {
                var ex = Assert.ThrowsAny<ArgumentException>(() => CliOptions.Parse(["--assembly", "app.dll"]));
                Assert.Contains("--max-analysis-depth", ex.Message, StringComparison.Ordinal);
            });
    }

    /// <summary>
    /// Sets the runtime-limit environment variables for the duration of <paramref name="action"/>, clearing the others,
    /// and restores the original values afterwards.
    /// </summary>
    private static void WithEnvironment(IReadOnlyDictionary<string, string?> values, Action action)
    {
        var original = LimitVariables.ToDictionary(name => name, name => Environment.GetEnvironmentVariable(name));

        try
        {
            foreach (var name in LimitVariables)
            {
                Environment.SetEnvironmentVariable(name, values.GetValueOrDefault(name));
            }

            action();
        }
        finally
        {
            foreach (var (name, value) in original)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }
}
