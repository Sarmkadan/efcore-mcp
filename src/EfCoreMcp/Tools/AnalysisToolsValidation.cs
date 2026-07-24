using System.ComponentModel;
using EfCoreMcp.Core.Domain;

namespace EfCoreMcp.Tools;

/// <summary>
/// Provides validation helpers for <see cref="AnalysisTools"/> instances.
/// </summary>
public static class AnalysisToolsValidation
{
    /// <summary>
    /// Validates an <see cref="AnalysisTools"/> instance, returning a list of human-readable problems.
    /// </summary>
    /// <param name="value">The <see cref="AnalysisTools"/> instance to validate.</param>
    /// <returns>An empty list if the instance is valid; otherwise, a list of problem descriptions.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<string> Validate(this AnalysisTools value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = new List<string>();

        // ValidateModel should not return null
        var modelReport = value.ValidateModel();
        if (modelReport is null)
        {
            problems.Add("ValidateModel() returned null");
        }

        // SuggestIndexes should not return null
        var indexSuggestions = value.SuggestIndexes();
        if (indexSuggestions is null)
        {
            problems.Add("SuggestIndexes() returned null");
        }

        // ExplainRelationship with valid entity names should not return null
        try
        {
            var relationshipPath = value.ExplainRelationship("Entity1", "Entity2");
            if (relationshipPath is null)
            {
                problems.Add("ExplainRelationship() returned null for valid entity names");
            }
        }
        catch
        {
            // Ignore - entities may not exist in the model
        }

        // DependencyOrder should not return null
        var dependencyOrder = value.DependencyOrder();
        if (dependencyOrder is null)
        {
            problems.Add("DependencyOrder() returned null");
        }

        return problems.AsReadOnly();
    }

    /// <summary>
    /// Determines whether an <see cref="AnalysisTools"/> instance is valid.
    /// </summary>
    /// <param name="value">The <see cref="AnalysisTools"/> instance to check.</param>
    /// <returns><see langword="true"/> if the instance is valid; otherwise, <see langword="false"/>.</returns>
    public static bool IsValid(this AnalysisTools value)
    {
        return Validate(value).Count == 0;
    }

    /// <summary>
    /// Ensures that an <see cref="AnalysisTools"/> instance is valid, throwing an <see cref="ArgumentException"/> if it is not.
    /// </summary>
    /// <param name="value">The <see cref="AnalysisTools"/> instance to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="value"/> is not valid, containing a list of problems.</exception>
    public static void EnsureValid(this AnalysisTools value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = Validate(value);
        if (problems.Count > 0)
        {
            throw new ArgumentException(
                $"AnalysisTools instance is not valid. Problems: {string.Join("; ", problems)}");
        }
    }
}