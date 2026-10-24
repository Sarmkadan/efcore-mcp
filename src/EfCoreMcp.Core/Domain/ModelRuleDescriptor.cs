namespace EfCoreMcp.Core.Domain;

/// <summary>
/// Identity of a model validation rule: its stable ID, the severity of the findings it produces, and what it checks.
/// </summary>
public sealed record ModelRuleDescriptor
{
    /// <summary>
    /// Gets the stable rule identifier, for example <c>EFMCP003</c>. Findings carry this value in their <see cref="ModelFinding.Code"/>.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the severity shared by every finding the rule produces.
    /// </summary>
    public string Severity { get; }

    /// <summary>
    /// Gets a short, human-readable name for the rule.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets a description of what the rule detects and why it matters.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Initializes a new <see cref="ModelRuleDescriptor"/>.
    /// </summary>
    /// <param name="id">The stable rule identifier.</param>
    /// <param name="severity">The severity of the rule's findings.</param>
    /// <param name="title">A short name for the rule.</param>
    /// <param name="description">A description of what the rule checks.</param>
    /// <exception cref="ArgumentException">Thrown when any argument is <see langword="null"/> or empty.</exception>
    public ModelRuleDescriptor(string id, string severity, string title, string description)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentException.ThrowIfNullOrEmpty(severity);
        ArgumentException.ThrowIfNullOrEmpty(title);
        ArgumentException.ThrowIfNullOrEmpty(description);
        Id = id;
        Severity = severity;
        Title = title;
        Description = description;
    }
}
