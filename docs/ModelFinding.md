# ModelFinding

Represents the result of analyzing an Entity Framework Core model, including validation findings, index suggestions, relationship paths, and dependency ordering information. Used to surface issues, optimization opportunities, and structural insights during model analysis.

## API

### `public sealed record ModelFinding`

The root record containing all findings from a model analysis.

- **Properties**:
  - `ValidationReport: ModelValidationReport?` – Overall validation report, if any issues were found.
  - `IndexSuggestions: IReadOnlyList<IndexSuggestion>` – List of suggested index additions or modifications.
  - `RelationshipPaths: IReadOnlyList<RelationshipPath>` – List of relationship traversals identified during analysis.
  - `DependencyOrder: IReadOnlyList<DependencyOrder>` – Recommended order for applying migrations or constructing the model.

---

### `public sealed record ModelValidationReport`

Contains validation findings and diagnostics for the model.

- **Properties**:
  - `Errors: IReadOnlyList<string>` – List of error-level validation messages.
  - `Warnings: IReadOnlyList<string>` – List of warning-level validation messages.
  - `Infos: IReadOnlyList<string>` – List of informational messages.

No parameters or return values beyond construction. Not intended to be instantiated directly outside analysis tools.

---

### `public sealed record IndexSuggestion`

Suggests the creation or modification of an index to improve query performance.

- **Properties**:
  - `EntityTypeName: string` – Name of the entity type the index applies to.
  - `PropertyNames: IReadOnlyList<string>` – Names of the properties included in the index.
  - `IsUnique: bool` – Indicates whether the index should be unique.
  - `Reason: string` – Explanation for why the index is recommended.

Constructed with required entity type name, property names, uniqueness flag, and reason. No runtime validation beyond null checks on strings.

---

### `public sealed record RelationshipHop`

Represents a single step in a relationship traversal path.

- **Properties**:
  - `SourceEntityName: string` – Name of the source entity.
  - `SourcePropertyName: string` – Name of the navigation or foreign key property on the source.
  - `TargetEntityName: string` – Name of the target entity.
  - `TargetPropertyName: string` – Name of the corresponding navigation or foreign key on the target.
  - `IsCollection: bool` – Indicates whether the relationship is a collection (e.g., `ICollection<T>`).

Used internally to build `RelationshipPath` records. Not intended for direct instantiation.

---
### `public sealed record RelationshipPath`

Represents a traversal path through multiple related entities.

- **Properties**:
  - `EntityNames: IReadOnlyList<string>` – Ordered list of entity names in the path.
  - `Hops: IReadOnlyList<RelationshipHop>` – Ordered list of hops between entities.
  - `IsBidirectional: bool` – Indicates whether the path can be traversed in both directions.

Constructed via analysis tools; no public constructor exposed.

---
### `public sealed record DependencyOrder`

Represents a recommended ordering constraint between entity types based on dependencies.

- **Properties**:
  - `DependentEntityName: string` – Name of the entity that depends on another.
  - `RequiredEntityName: string` – Name of the entity it depends on.
  - `Reason: string` – Explanation for the dependency.

Used to guide migration ordering or model construction. Not intended for direct instantiation.

## Usage
