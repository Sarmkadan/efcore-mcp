# Model validation rules

`validate_model` (and `ModelAnalyzer.ValidateModel`) runs every rule below against the EF Core model. Each finding carries its rule ID in `code`, so findings can be filtered, documented and suppressed.

## Suppression

Pass rule IDs in the `suppress` argument of `validate_model`. Matching is case-insensitive, and an unknown ID is rejected with an error that lists the valid IDs:

```json
{ "suppress": ["EFMCP012", "EFMCP013"] }
```

## Rules

| ID | Severity | Title | What it detects |
|----|----------|-------|-----------------|
| EFMCP001 | warning | Keyless entity | Entity type has no primary key. |
| EFMCP002 | info | Unbounded string column | String property with no max length that is not part of any key or index. |
| EFMCP003 | warning | Decimal without precision | Decimal property with no explicit precision or column type. |
| EFMCP004 | warning | Optional relationship with cascade delete | Optional foreign key configured with `Cascade`. |
| EFMCP005 | info | Unindexed foreign key | Foreign key columns not led by any non-filtered index, primary key or alternate key. Column order inside a composite FK is ignored. |
| EFMCP006 | info | Collection navigation without inverse | Collection navigation with no inverse reference navigation. |
| EFMCP007 | info | Shadow foreign key | Foreign key that exists only as a shadow property, usually a mapping mistake. |
| EFMCP008 | warning | Multiple cascade paths | Entity is a cascade-delete target from more than one principal (rejected by SQL Server). |
| EFMCP009 | warning | Unbounded string in key or index | String key, foreign key or index column with no max length. SQL Server maps it to `nvarchar(max)`, which cannot be a key column. |
| EFMCP010 | info | Wide cascade-delete chain | Cascade delete reaches three or more dependent entity types transitively, so one delete can wipe many tables. |
| EFMCP011 | warning | Required navigation over optional foreign key | Non-nullable reference navigation whose foreign key is optional, so the navigation is null at runtime. Requires nullable reference annotations on the navigation property. |
| EFMCP012 | info | Missing concurrency token | Keyed root entity type with no concurrency token, so concurrent updates are last-writer-wins. Derived types are not checked. |
| EFMCP013 | info | Unindexed TPH discriminator | Table-per-hierarchy root whose discriminator is not the leading column of any index. TPT and TPC hierarchies have no discriminator and are not checked. |

## Fixes

- **EFMCP002 / EFMCP009:** call `HasMaxLength(n)`. For key columns, keep the total key width under the provider's index limit.
- **EFMCP003:** call `HasPrecision(p, s)` or `HasColumnType("decimal(p,s)")`.
- **EFMCP004:** use `DeleteBehavior.SetNull` or `ClientSetNull` unless deletion of dependents is intended.
- **EFMCP005:** add `HasIndex` over the foreign-key columns.
- **EFMCP008:** set one of the cascades to `Restrict` or `NoAction`, and handle that deletion in code.
- **EFMCP010:** switch intermediate links to `Restrict` or `SetNull` to limit what one delete removes.
- **EFMCP011:** make the foreign key required, or declare the navigation nullable.
- **EFMCP012:** add a `rowversion` (`[Timestamp]` or `IsRowVersion()`), or an `IsConcurrencyToken()` column.
- **EFMCP013:** `modelBuilder.Entity<Root>().HasIndex("Discriminator")`.
