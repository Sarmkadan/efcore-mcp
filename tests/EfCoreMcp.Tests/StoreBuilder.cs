using System;
using System.Collections.Generic;
using System.Linq;

namespace EfCoreMcp.Tests;

/// <summary>
/// Provides a fluent builder for creating <see cref="Store"/> instances in tests.
/// </summary>
public class StoreBuilder
{
    private int? _id;
    private string? _name;
    private List<Sale> _sales = new();

    /// <summary>
    /// Creates a new <see cref="StoreBuilder"/> pre‑populated with the values from the specified <paramref name="template"/>.
    /// </summary>
    /// <param name="template">The source <see cref="Store"/> to copy values from.</param>
    /// <returns>A new <see cref="StoreBuilder"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="template"/> is <c>null</c>.</exception>
    public static StoreBuilder From(Store template)
    {
        ArgumentNullException.ThrowIfNull(template);

        return new StoreBuilder()
            .WithId(template.Id)
            .WithName(template.Name)
            .WithSales(template.Sales);
    }

    /// <summary>
    /// Sets the <see cref="Store.Id"/> property.
    /// </summary>
    /// <param name="id">The identifier value.</param>
    /// <returns>The current <see cref="StoreBuilder"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is less than or equal to zero.</exception>
    public StoreBuilder WithId(int id)
    {
        if (id <= 0)
            throw new ArgumentException("Id must be greater than zero.", nameof(id));

        _id = id;
        return this;
    }

    /// <summary>
    /// Sets the <see cref="Store.Name"/> property.
    /// </summary>
    /// <param name="name">The store name.</param>
    /// <returns>The current <see cref="StoreBuilder"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is <c>null</c>, empty or whitespace.</exception>
    public StoreBuilder WithName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null, empty, or whitespace.", nameof(name));

        _name = name;
        return this;
    }

    /// <summary>
    /// Replaces the collection of <see cref="Store.Sales"/> with the supplied sequence.
    /// </summary>
    /// <param name="sales">A sequence of <see cref="Sale"/> objects.</param>
    /// <returns>The current <see cref="StoreBuilder"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sales"/> is <c>null</c>.</exception>
    public StoreBuilder WithSales(IEnumerable<Sale> sales)
    {
        ArgumentNullException.ThrowIfNull(sales);
        _sales = sales.ToList();
        return this;
    }

    /// <summary>
    /// Adds a single <see cref="Sale"/> to the <see cref="Store.Sales"/> collection.
    /// </summary>
    /// <param name="sale">The <see cref="Sale"/> to add.</param>
    /// <returns>The current <see cref="StoreBuilder"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sale"/> is <c>null</c>.</exception>
    public StoreBuilder AddSale(Sale sale)
    {
        ArgumentNullException.ThrowIfNull(sale);
        _sales.Add(sale);
        return this;
    }

    /// <summary>
    /// Builds a <see cref="Store"/> instance using the configured values.
    /// </summary>
    /// <returns>A fully configured <see cref="Store"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when required properties (<c>Id</c> or <c>Name</c>) have not been supplied.
    /// </exception>
    public Store Build()
    {
        if (!_id.HasValue)
            throw new ArgumentException("Id must be set before building a Store.", nameof(_id));

        if (string.IsNullOrWhiteSpace(_name))
            throw new ArgumentException("Name must be set before building a Store.", nameof(_name));

        return new Store
        {
            Id = _id.Value,
            Name = _name,
            Sales = _sales
        };
    }
}
