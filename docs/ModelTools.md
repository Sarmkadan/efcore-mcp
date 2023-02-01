# ModelTools

`ModelTools` provides a comprehensive interface for introspecting and analyzing Entity Framework Core model configurations within the `efcore-mcp` ecosystem. It enables developers to retrieve structural metadata, generate human-readable schema explanations, and derive relationship graphs from a configured `DbContext`, facilitating diagnostics, documentation generation, and model understanding.

## API

### ContextInfo
`public ContextInfo ContextInfo { get; }`
Provides metadata about the underlying `DbContext`, including connection details and model status.

### ListEntities
`public IReadOnlyList<string> ListEntities { get; }`
Retrieves a read-only list of the fully qualified names of all entity types currently defined in the model.

### DescribeModel
`public ModelDescriptor DescribeModel()`
Generates and returns a `ModelDescriptor` object containing the complete structural definition of the model, including entities, relationships, and configurations.

### DescribeEntity
`public EntityDescriptor DescribeEntity(string entityName)`
Returns an `EntityDescriptor` for the specified entity, containing detailed metadata about its properties, keys, and navigation properties. 
*Throws:* `ArgumentException` if the `entityName` does not exist in the model.

### ExplainSchema
`public string ExplainSchema()`
Generates a human-readable text explanation of the database schema derived from the EF Core model, outlining tables and primary constraints.

### ExplainEntity
`public string ExplainEntity(string entityName)`
Generates a detailed human-readable explanation of the schema and configuration for a specific entity.
*Throws:* `ArgumentException` if the `entityName` does not exist in the model.

### RelationshipGraph
`public string RelationshipGraph { get; }`
Returns a string representation of the model's relationship graph, formatted for visualization tools such as Mermaid.js.

## Usage

### Introspecting Model Entities
```csharp
var context = new MyDbContext();
var modelTools = new ModelTools(context);

Console.WriteLine($"Analyzing model with {modelTools.ListEntities.Count} entities.");

foreach (var entityName in modelTools.ListEntities)
{
    var descriptor = modelTools.DescribeEntity(entityName);
    Console.WriteLine($"Entity: {descriptor.Name}, Properties: {descriptor.Properties.Count}");
}
```

### Exporting Schema Documentation
```csharp
var context = new MyDbContext();
var modelTools = new ModelTools(context);

// Generate text explanation
string schemaSummary = modelTools.ExplainSchema();
File.WriteAllText("schema_summary.txt", schemaSummary);

// Generate visualization graph
string mermaidGraph = modelTools.RelationshipGraph;
File.WriteAllText("model_graph.mmd", mermaidGraph);
```

## Notes

*   **Thread Safety:** `ModelTools` instances are not thread-safe. As they rely on an underlying `DbContext`, they must adhere to the same thread-safety constraints as the context instance they wrap. It is recommended to use `ModelTools` within the same scope/lifetime as the `DbContext`.
*   **Model Initialization:** `ModelTools` requires a fully configured and initialized model. If the `DbContext` model has not been fully built (e.g., accessed before `OnModelCreating` has been processed or the model is not materialized), behavior may be undefined or result in empty descriptor sets.
*   **Entity Names:** Methods accepting `entityName` expect the fully qualified name or the specific type name as identified within the EF Core model metadata. If provided names do not match the internal model registry, an `ArgumentException` will be thrown.
