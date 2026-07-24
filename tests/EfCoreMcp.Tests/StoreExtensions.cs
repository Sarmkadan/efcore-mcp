using System;
using System.Collections.Generic;
using System.Linq;

namespace EfCoreMcp.Tests;

/// <summary>
/// Extension methods for the <see cref="Store"/> test entity.
/// </summary>
public static class StoreExtensions
{
    /// <summary>
    /// Calculates the total amount of all sales belonging to the store.
    /// </summary>
    /// <param name="store">The store whose sales are summed.</param>
    /// <returns>The sum of <see cref="Sale.Amount"/> for all sales in <see cref="Store.Sales"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is <c>null</c>.</exception>
    public static decimal GetTotalSalesAmount(this Store store)
    {
        ArgumentNullException.ThrowIfNull(store);
        return store.Sales?.Sum(s => s.Amount) ?? 0m;
    }

    /// <summary>
    /// Retrieves a read‑only list of distinct customers that have made purchases at the store.
    /// </summary>
    /// <param name="store">The store whose customers are queried.</param>
    /// <returns>An <see cref="IReadOnlyList{Customer}"/> containing each unique <see cref="Customer"/> referenced by the store's sales.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is <c>null</c>.</exception>
    public static IReadOnlyList<Customer> GetDistinctCustomers(this Store store)
    {
        ArgumentNullException.ThrowIfNull(store);
        var customers = store.Sales?
            .Select(s => s.Customer)
            .Where(c => c != null)
            .Distinct()
            .ToList() ?? new List<Customer>();
        return customers.AsReadOnly();
    }

    /// <summary>
    /// Returns the number of sales recorded for the store.
    /// </summary>
    /// <param name="store">The store whose sales count is required.</param>
    /// <returns>The count of items in <see cref="Store.Sales"/>; zero if the collection is <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is <c>null</c>.</exception>
    public static int GetSalesCount(this Store store)
    {
        ArgumentNullException.ThrowIfNull(store);
        return store.Sales?.Count ?? 0;
    }

    /// <summary>
    /// Retrieves all sales for a specific customer within the store.
    /// </summary>
    /// <param name="store">The store to search.</param>
    /// <param name="customerId">The identifier of the customer whose sales are required.</param>
    /// <returns>An <see cref="IReadOnlyList{Sale}"/> of matching sales; an empty list if none are found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="customerId"/> is less than or equal to zero.</exception>
    public static IReadOnlyList<Sale> GetSalesByCustomer(this Store store, int customerId)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrEmpty(customerId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var sales = store.Sales?
            .Where(s => s.CustomerId == customerId)
            .ToList() ?? new List<Sale>();
        return sales.AsReadOnly();
    }
}
