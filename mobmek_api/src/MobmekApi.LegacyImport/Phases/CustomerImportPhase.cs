using MobmekApi.LegacyImport.Legacy;
using MobmekApi.LegacyImport.Mappers;
using MobmekApi.LegacyImport.Pipeline;

namespace MobmekApi.LegacyImport.Phases;

/// <summary>Imports legacy Customers (design §3.1). Duplicates are imported as-is and flagged for manual review.</summary>
public sealed class CustomerImportPhase : ImportPhase
{
    public const string EntityType = "Customer";

    public override string Name => "customers";

    public override async Task RunAsync(ImportContext context, LegacyDbReader source, CancellationToken ct)
    {
        var stats = context.StatsFor(Name);
        var legacyCustomers = await source.CustomersAsync(ct);

        FlagSuspectedDuplicates(context, legacyCustomers);

        foreach (var legacy in legacyCustomers)
        {
            if (context.Map.Contains(EntityType, legacy.CustomerId))
            {
                stats.Skipped++;
                continue;
            }

            var (customer, singleWordName, missingPhone) = CustomerMapper.Map(legacy);
            context.Db.Customers.Add(customer);
            await context.Map.AddAsync(EntityType, legacy.CustomerId, customer.Id, ct);

            if (singleWordName)
            {
                context.Flag(
                    "single-word-name",
                    $"Customer #{legacy.CustomerId}",
                    $"'{legacy.CustomerName.Trim()}' imported as FirstName '{customer.FirstName}', LastName '{customer.LastName}' — tidy manually");
            }

            if (missingPhone)
            {
                context.Flag(
                    "placeholder-phone",
                    $"Customer #{legacy.CustomerId}",
                    $"'{legacy.CustomerName.Trim()}' has no phone number — set to '{CustomerMapper.PlaceholderPhone}'");
            }

            stats.Imported++;
        }
    }

    private static void FlagSuspectedDuplicates(ImportContext context, List<LegacyCustomer> customers)
    {
        var byName = customers
            .GroupBy(c => c.CustomerName.Trim().ToUpperInvariant())
            .Where(g => g.Count() > 1);
        foreach (var group in byName)
        {
            context.Flag(
                "suspected-duplicate-customer",
                string.Join(", ", group.Select(c => $"#{c.CustomerId}")),
                $"Same name '{group.First().CustomerName.Trim()}' — review and merge manually if they are the same person");
        }

        var byPhone = customers
            .Where(c => !string.IsNullOrWhiteSpace(c.CustomerNumber))
            .GroupBy(c => NormalizePhone(c.CustomerNumber!))
            .Where(g => g.Key.Length > 0 && g.Select(c => c.CustomerName.Trim().ToUpperInvariant()).Distinct().Count() > 1);
        foreach (var group in byPhone)
        {
            context.Flag(
                "suspected-duplicate-customer",
                string.Join(", ", group.Select(c => $"#{c.CustomerId}")),
                $"Different names share phone '{group.First().CustomerNumber!.Trim()}': {string.Join(" / ", group.Select(c => c.CustomerName.Trim()).Distinct())}");
        }
    }

    private static string NormalizePhone(string phone) =>
        new([.. phone.Where(char.IsDigit)]);
}
