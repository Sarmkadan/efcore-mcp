# ModelDescriptor

The `ModelDescriptor` type and its associated descriptor records provide a structured, read-only representation of an Entity Framework Core model, enabling introspection and analysis of entity schemas, relationships, and constraints in a format suitable for the Model Context Protocol (MCP).

## API

### ModelDescriptor
Represents the root of the EF Core model metadata.
*   **Entities**: A read-only collection of `EntityDescriptor` objects defining the entities within the model.

### EntityDescriptor
Describes an entity type within the model.
*   **Name**: The CLR type name of the entity.
*   **TableName**: The database table name associated with the entity.
*   **Properties**: A read-only collection of `PropertyDescriptor` objects representing the entity's scalar properties.
*   **Keys**: A read-only collection of `KeyDescriptor` objects representing the entity's primary and alternate keys.
*   **Indexes**: A read-only collection of `IndexDescriptor` objects defined on the entity.
*   **Navigations**: A read-only collection of `NavigationDescriptor` objects representing the entity's relationships.

### PropertyDescriptor
Defines a scalar property of an entity.
*   **Name**: The name of the property.
*   **PropertyType**: The CLR type of the property.
*   **IsNullable**: A boolean indicating if the property allows null values.

### KeyDescriptor
Represents a primary or alternate key constraint.
*   **Name**: The name of the key constraint.
*   **Properties**: A read-only collection of `PropertyDescriptor` objects that constitute the key.

### ForeignKeyDescriptor
Describes a foreign key relationship between entities.
*   **Name**: The name of the foreign key constraint.
*   **PrincipalEntity**: The `EntityDescriptor` of the principal entity.
*   **PrincipalProperties**: A read-only collection of `PropertyDescriptor` objects in the principal entity.
*   **DependentProperties**: A read-only collection of `PropertyDescriptor` objects in the dependent entity.

### NavigationDescriptor
Represents a navigation property, defining a relationship link.
*   **Name**: The name of the navigation property.
*   **TargetEntity**: The `EntityDescriptor` of the target entity.
*   **IsCollection**: A boolean indicating if the navigation property represents a collection (e.g., one-to-many).

### IndexDescriptor
Represents a database index constraint.
*   **Name**: The name of the index.
*   **Properties**: A read-only collection of `PropertyDescriptor` objects included in the index.
*   **IsUnique**: A boolean indicating if the index enforces uniqueness.

## Usage

### Introspecting Entity Properties
```csharp
public void LogEntityDetails(ModelDescriptor model)
{
    foreach (var entity in model.Entities)
    {
        Console.WriteLine($"Entity: {entity.Name} (Table: {entity.TableName})");
        foreach (var prop in entity.Properties)
        {
            Console.WriteLine($"  - Property: {prop.Name} ({prop.PropertyType}, Nullable: {prop.IsNullable})");
        }
    }
}
```

### Analyzing Foreign Key Relationships
```csharp
public void AnalyzeRelationships(EntityDescriptor entity)
{
    foreach (var nav in entity.Navigations)
    {
        string relationshipType = nav.IsCollection ? "Collection" : "Reference";
        Console.WriteLine($"Navigation: {nav.Name} to {nav.TargetEntity.Name} ({relationshipType})");
    }
}
```

## Notes

*   **Immutability**: All descriptor types are defined as `sealed record` and are intended to be immutable snapshots of the model state. They do not support modification after initialization.
*   **Thread-Safety**: Due to their immutable nature, instances of these records are inherently thread-safe for concurrent read operations.
*   **Edge Cases**:
    *   `NavigationDescriptor` may refer to entities outside the current `ModelDescriptor.Entities` collection if the model snapshot is partial.
    *   `ForeignKeyDescriptor` properties collections may be empty if the foreign key is not correctly mapped or if the model is derived from an incomplete schema.
    *   `KeyDescriptor` collections are guaranteed to have at least one property for valid keys, but may be empty in scenarios of malformed model snapshots.
