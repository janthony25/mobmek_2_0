using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MobmekApi.Entities;
using MobmekApi.LegacyImport.Legacy;
using MobmekApi.LegacyImport.Pipeline;

namespace MobmekApi.LegacyImport.Phases;

/// <summary>
/// Read-only final phase computing the §6 reconciliation table: legacy counts and money
/// sums against the imported rows only (identified via the map), so pre-existing dev data
/// never pollutes the comparison. Every row must match for sign-off.
/// </summary>
public sealed class ReconciliationPhase : ImportPhase
{
    public override string Name => "reconcile";

    public override async Task RunAsync(ImportContext context, LegacyDbReader source, CancellationToken ct)
    {
        var legacyInvoices = await source.InvoicesAsync(ct);
        var legacyQuotations = await source.QuotationsAsync(ct);
        var newInvoices = await source.NewInvoicesAsync(ct);
        var newQuotations = await source.NewQuotationsAsync(ct);

        var importedByType = new Dictionary<string, List<Invoice>>();
        var allDocs = await context.Db.Invoices.AsNoTracking().ToListAsync(ct);
        foreach (var entityType in new[]
                 {
                     LegacyInvoiceImportPhase.EntityType, LegacyQuotationImportPhase.EntityType,
                     NewInvoiceImportPhase.EntityType, NewQuotationImportPhase.EntityType,
                 })
        {
            var ids = context.Map.NewIdsFor(entityType).ToHashSet();
            importedByType[entityType] = [.. allDocs.Where(d => ids.Contains(d.Id))];
        }

        CompareDocuments(context, "Legacy Invoices", importedByType[LegacyInvoiceImportPhase.EntityType],
            legacyInvoices.Count,
            legacyInvoices.Sum(i => i.TotalAmount ?? 0),
            legacyInvoices.Sum(i => i.AmountPaid ?? 0),
            legacyInvoices.Count(i => i.IsPaid));
        CompareDocuments(context, "Legacy Quotations", importedByType[LegacyQuotationImportPhase.EntityType],
            legacyQuotations.Count,
            legacyQuotations.Sum(q => q.TotalAmount ?? 0),
            amountPaid: null,
            paidCount: null);
        CompareDocuments(context, "NewInvoices", importedByType[NewInvoiceImportPhase.EntityType],
            newInvoices.Count,
            newInvoices.Sum(i => i.TotalAmount ?? 0),
            newInvoices.Sum(i => i.AmountPaid ?? 0),
            newInvoices.Count(i => i.IsPaid));
        CompareDocuments(context, "NewQuotations", importedByType[NewQuotationImportPhase.EntityType],
            newQuotations.Count,
            newQuotations.Sum(q => q.TotalAmount ?? 0),
            amountPaid: null,
            paidCount: null);

        // Combined per DocumentType — what the new system's screens will show.
        var importedInvoiceDocs = importedByType[LegacyInvoiceImportPhase.EntityType]
            .Concat(importedByType[NewInvoiceImportPhase.EntityType]).ToList();
        var importedQuotationDocs = importedByType[LegacyQuotationImportPhase.EntityType]
            .Concat(importedByType[NewQuotationImportPhase.EntityType]).ToList();
        Row(context, "All Invoice docs — SUM(TotalAmount)",
            Money(legacyInvoices.Sum(i => i.TotalAmount ?? 0) + newInvoices.Sum(i => i.TotalAmount ?? 0)),
            Money(importedInvoiceDocs.Sum(d => d.TotalAmount)));
        Row(context, "All Quotation docs — SUM(TotalAmount)",
            Money(legacyQuotations.Sum(q => q.TotalAmount ?? 0) + newQuotations.Sum(q => q.TotalAmount ?? 0)),
            Money(importedQuotationDocs.Sum(d => d.TotalAmount)));

        // Job count: mapped legacy jobs + one synthetic job per legacy Invoice/Quotation.
        var legacyJobCount = (await source.JobsAsync(ct)).Count;
        var mappedJobCount = context.Map.NewIdsFor(JobImportPhase.EntityType).Count;
        var syntheticJobCount = await context.Db.Jobs.AsNoTracking()
            .CountAsync(j => j.JobNotes != null && j.JobNotes.StartsWith("Auto-created during legacy import"), ct);
        Row(context, "Jobs mapped from legacy Jobs", legacyJobCount.ToString(), mappedJobCount.ToString());
        Row(context, "Synthetic jobs (legacy Invoices+Quotations)",
            (legacyInvoices.Count + legacyQuotations.Count).ToString(), syntheticJobCount.ToString());

        // Item lines across all four legacy item tables vs imported InvoiceItems.
        var legacyItemCount =
            (await source.InvoiceItemsAsync(ct)).Count +
            (await source.QuotationItemsAsync(ct)).Count +
            (await source.NewInvoiceItemsAsync(ct)).Count +
            (await source.NewQuotationItemsAsync(ct)).Count;
        var importedDocIds = importedByType.Values.SelectMany(v => v).Select(d => d.Id).ToHashSet();
        var importedItemCount = (await context.Db.InvoiceItems.AsNoTracking().Select(i => i.InvoiceId).ToListAsync(ct))
            .Count(id => importedDocIds.Contains(id));
        Row(context, "Document item lines", legacyItemCount.ToString(), importedItemCount.ToString());

        var legacyAppointmentCount = (await source.AppointmentsAsync(ct)).Count;
        var importedAppointmentCount = context.Map.NewIdsFor(AppointmentImportPhase.EntityType).Count;
        Row(context, "Appointments", legacyAppointmentCount.ToString(), importedAppointmentCount.ToString());
    }

    private static void CompareDocuments(
        ImportContext context,
        string label,
        List<Invoice> imported,
        int legacyCount,
        decimal legacyTotal,
        decimal? amountPaid,
        int? paidCount)
    {
        Row(context, $"{label} — count", legacyCount.ToString(), imported.Count.ToString());
        Row(context, $"{label} — SUM(TotalAmount)", Money(legacyTotal), Money(imported.Sum(d => d.TotalAmount)));
        if (amountPaid is not null)
        {
            Row(context, $"{label} — SUM(AmountPaid)", Money(amountPaid.Value), Money(imported.Sum(d => d.AmountPaid ?? 0)));
        }

        if (paidCount is not null)
        {
            Row(context, $"{label} — paid count", paidCount.Value.ToString(), imported.Count(d => d.IsPaid).ToString());
        }
    }

    private static void Row(ImportContext context, string metric, string legacy, string imported) =>
        context.Reconciliation.Add(new ReconciliationRow(metric, legacy, imported, legacy == imported));

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
