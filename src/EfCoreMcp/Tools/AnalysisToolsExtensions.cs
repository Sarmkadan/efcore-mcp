using System.Globalization;
using EfCoreMcp.Core.Domain;

namespace EfCoreMcp.Tools;

public static class AnalysisToolsExtensions
{
    /// <summary>
    /// Gets all findings from model validation that indicate potential issues.
    /// </summary>
    /// <param name="tools">The analysis tools instance.</param>
    /// <returns>An enumerable of model findings.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tools"/> is <see langword="null"/>.</exception>
    public static IEnumerable<ModelFinding> GetValidationFindings(this AnalysisTools tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var report = tools.ValidateModel();
        return report.Findings;
    }

    /// <summary>
    /// Gets all suggested indexes that are based on foreign keys.
    /// </summary>
    /// <param name="tools">The analysis tools instance.</param>
    /// <returns>An enumerable of index suggestions based on foreign keys.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tools"/> is <see langword="null"/>.</exception>
    public static IEnumerable<IndexSuggestion> GetForeignKeyIndexes(this AnalysisTools tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var suggestions = tools.SuggestIndexes();
        return suggestions.Where(s => s.Reason.Contains("foreign key", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets all suggested indexes that are based on navigation properties.
    /// </summary>
    /// <param name="tools">The analysis tools instance.</param>
    /// <returns>An enumerable of index suggestions based on navigation properties.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tools"/> is <see langword="null"/>.</exception>
    public static IEnumerable<IndexSuggestion> GetNavigationIndexes(this AnalysisTools tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var suggestions = tools.SuggestIndexes();
        return suggestions.Where(s => s.Reason.Contains("navigation", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the dependency order for safe deletion of entities in the model.
    /// </summary>
    /// <param name="tools">The analysis tools instance.</param>
    /// <returns>A sequence of entity names in the order they should be deleted.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tools"/> is <see langword="null"/>.</exception>
    public static IEnumerable<string> GetSafeDeleteOrder(this AnalysisTools tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var order = tools.DependencyOrder();
        return order.DeleteOrder;
    }
}