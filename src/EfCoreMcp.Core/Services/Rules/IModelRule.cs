using EfCoreMcp.Core.Domain;

namespace EfCoreMcp.Core.Services.Rules;

/// <summary>
/// A single model validation check, identified by a stable rule ID.
/// </summary>
internal interface IModelRule
{
    /// <summary>
    /// Gets the rule's identity: stable ID, finding severity, title and description.
    /// </summary>
    ModelRuleDescriptor Descriptor { get; }

    /// <summary>
    /// Evaluates the rule against every entity in the model.
    /// </summary>
    /// <param name="model">The model descriptor to inspect.</param>
    /// <returns>The findings produced by this rule; empty when the model is clean for this rule.</returns>
    IEnumerable<ModelFinding> Evaluate(ModelDescriptor model);
}
