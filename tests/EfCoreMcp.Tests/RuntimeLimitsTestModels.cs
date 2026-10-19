using EfCoreMcp.Core.Domain;

namespace EfCoreMcp.Tests;

/// <summary>
/// Builds small in-memory models for the runtime limit tests.
/// </summary>
internal static class RuntimeLimitsTestModels
{
    /// <summary>
    /// Creates a model from the given entities.
    /// </summary>
    public static ModelDescriptor Model(params EntityDescriptor[] entities) =>
        new("LimitsContext", "TestProvider", entities);

    /// <summary>
    /// Creates an entity with no properties, keys or navigations.
    /// </summary>
    public static EntityDescriptor Entity(string name, string? schema = null, params ForeignKeyDescriptor[] foreignKeys) =>
        new(name, name, name, schema, false, null, [], null, [], foreignKeys, [], []);

    /// <summary>
    /// Creates a required foreign key in which <paramref name="dependent"/> depends on <paramref name="principal"/>.
    /// </summary>
    public static ForeignKeyDescriptor ForeignKey(string principal, string dependent) =>
        new(null, principal, dependent, [$"{principal}Id"], ["Id"], "Cascade", true, false);

    /// <summary>
    /// Creates the chain A &lt;- B &lt;- C &lt;- D, where each entity depends on the one before it.
    /// The last entity is placed in <paramref name="lastSchema"/>.
    /// </summary>
    public static ModelDescriptor Chain(string? lastSchema = null) =>
        Model(
            Entity("A"),
            Entity("B", null, ForeignKey("A", "B")),
            Entity("C", null, ForeignKey("B", "C")),
            Entity("D", lastSchema, ForeignKey("C", "D")));
}
