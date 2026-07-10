using MobmekApi.Entities;

namespace MobmekApi.LegacyImport.Mappers;

/// <summary>
/// Builds the auto-created Job carrying a first-generation Invoice/Quotation (design §3.5):
/// those documents pointed straight at a Car, but the new system requires every document to
/// belong to a Job. The job gets no children — the document's line items carry the detail —
/// so its totals stay 0; the backend recomputes them only if the job is ever edited.
/// </summary>
public static class SyntheticJobBuilder
{
    private const int TitleMaxLength = 200;

    public static Job Build(
        Guid customerId,
        Guid carId,
        string issueName,
        string documentType,
        int legacyDocumentId,
        DateTime? dateAddedNz,
        DateTime? dateEditedNz)
    {
        var documentLabel = documentType.ToLowerInvariant();
        var title = issueName.Trim();
        if (title.Length == 0)
        {
            title = $"Legacy {documentLabel} #{legacyDocumentId}";
        }

        return new Job
        {
            CustomerId = customerId,
            CarId = carId,
            Title = title.Length <= TitleMaxLength ? title : title[..TitleMaxLength],
            Status = documentType == "Invoice" ? JobStatus.Invoiced : JobStatus.Completed,
            Odometer = 0,
            JobNotes = $"Auto-created during legacy import for {documentLabel} #{legacyDocumentId}",
            InvoiceNotes = null,
            DiscountType = DiscountType.None,
            DiscountValue = 0,
            TotalJobPrice = 0,
            TotalJobProfit = 0,
            CreatedAtUtc = dateAddedNz is null ? DateTime.UtcNow : NzTime.ToUtc(dateAddedNz.Value),
            UpdatedAtUtc = NzTime.ToUtc(dateEditedNz),
        };
    }
}
