using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Data;

/// <summary>
/// Seeds the starter set of system transaction categories (NZ small-workshop flavoured).
/// Idempotent: each category is created only if a category with that name doesn't already
/// exist, so it is safe to run on every Development startup.
/// </summary>
public static class CashFlowSeeder
{
    /// <summary>System category that invoice payments auto-post into.</summary>
    public const string WorkshopSalesCategory = "Workshop Sales";

    /// <summary>System category stamped on both legs of an account-to-account transfer.</summary>
    public const string TransferCategory = "Transfer";

    // Name, Direction, Group, DefaultGstTreatment, ExcludeFromOperatingExpense.
    // GST treatments follow the usual NZ defaults: wages, bank fees, interest, IRD
    // remittances and financing movements carry no GST; most trading does.
    private static readonly (string Name, string Direction, string Group, string Gst, bool Exclude)[] Starter =
    [
        // Inflows
        (WorkshopSalesCategory, "In", "Sales", "Taxable", false),
        ("Parts Sales", "In", "Sales", "Taxable", false),
        ("Other Income", "In", "Other Income", "Taxable", false),
        ("Interest Received", "In", "Other Income", "Exempt", false),
        ("Grant Received", "In", "Other Income", "Taxable", false),
        ("Capital Introduced", "In", "Financing", "Exempt", true),
        ("Loan Received", "In", "Financing", "Exempt", true),
        ("GST Refund", "In", "Taxes", "Exempt", true),

        // Outflows
        ("Parts & Materials", "Out", "Operating", "Taxable", false),
        ("Subcontractors", "Out", "Operating", "Taxable", false),
        ("Rent", "Out", "Operating", "Taxable", false),
        ("Power & Water", "Out", "Operating", "Taxable", false),
        ("Insurance", "Out", "Operating", "Taxable", false),
        ("Vehicle & Fuel", "Out", "Operating", "Taxable", false),
        ("Tools & Equipment", "Out", "Operating", "Taxable", false),
        ("Software Subscriptions", "Out", "Operating", "Taxable", false),
        ("Phone & Internet", "Out", "Operating", "Taxable", false),
        ("Marketing", "Out", "Operating", "Taxable", false),
        ("Bank Fees", "Out", "Operating", "Exempt", false),
        ("Wages & Salaries", "Out", "Payroll", "Exempt", false),
        ("KiwiSaver Employer", "Out", "Payroll", "Exempt", false),
        ("PAYE to IRD", "Out", "Taxes", "Exempt", true),
        ("GST Payment", "Out", "Taxes", "Exempt", true),
        ("Provisional/Income Tax", "Out", "Taxes", "Exempt", true),
        ("ACC Levies", "Out", "Taxes", "Taxable", false),
        ("Loan Repayment", "Out", "Financing", "Exempt", true),
        ("Owner Drawings", "Out", "Financing", "Exempt", true),

        // Both legs of a transfer; moves balances but never counts as income or spend.
        (TransferCategory, "Either", "Transfer", "Exempt", true),
    ];

    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var existing = await db.TransactionCategories
            .Select(c => c.Name)
            .ToListAsync(cancellationToken);

        foreach (var row in Starter.Where(s => !existing.Contains(s.Name)))
        {
            db.TransactionCategories.Add(ToEntity(row));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Returns the system category with <paramref name="name"/>, creating it from the starter
    /// definition if the database was never seeded. Lets invoice posting and transfers rely on
    /// their categories unconditionally.
    /// </summary>
    public static async Task<TransactionCategory> EnsureSystemCategoryAsync(
        AppDbContext db, string name, CancellationToken cancellationToken = default)
    {
        var category = await db.TransactionCategories.FirstOrDefaultAsync(c => c.Name == name, cancellationToken);
        if (category is null)
        {
            category = ToEntity(Starter.Single(s => s.Name == name));
            db.TransactionCategories.Add(category);
            await db.SaveChangesAsync(cancellationToken);
        }

        return category;
    }

    private static TransactionCategory ToEntity((string Name, string Direction, string Group, string Gst, bool Exclude) row) =>
        new()
        {
            Name = row.Name,
            Direction = row.Direction,
            Group = row.Group,
            DefaultGstTreatment = row.Gst,
            ExcludeFromOperatingExpense = row.Exclude,
            IsSystem = true,
        };
}
