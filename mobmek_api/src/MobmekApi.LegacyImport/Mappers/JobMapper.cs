using System.Text;
using MobmekApi.Entities;
using MobmekApi.LegacyImport.Legacy;

namespace MobmekApi.LegacyImport.Mappers;

/// <summary>
/// Result of mapping a legacy job. <paramref name="StatusFlagRaw"/> is non-null when the
/// legacy status had no clean equivalent and the mapping should be flagged in the report.
/// </summary>
public sealed record JobMapResult(Job Job, string? StatusFlagRaw);

/// <summary>
/// Legacy Job → new Job (design §3.4). Structured legacy fields the new schema has no home
/// for (Issue, start/finish dates, labour names, mechanic name) are preserved as an
/// "Imported details" block in JobNotes rather than lost. Totals are copied, not recomputed.
/// </summary>
public static class JobMapper
{
    private const int JobNotesMaxLength = 4000;

    public static JobMapResult Map(
        LegacyJob legacy,
        Guid customerId,
        Guid carId,
        IReadOnlyList<string> labourNames,
        string? mechanicName,
        bool hasActiveNewInvoice)
    {
        var (status, flagRaw) = MapStatus(legacy.Status, hasActiveNewInvoice);

        var job = new Job
        {
            CustomerId = customerId,
            CarId = carId,
            Title = legacy.Title.Trim(),
            Status = status,
            Odometer = legacy.Odometer ?? 0,
            JobNotes = BuildImportedNotes(legacy, labourNames, mechanicName),
            InvoiceNotes = null,
            DiscountType = DiscountType.None,
            DiscountValue = 0,
            // Snapshots from the old system; the new backend recomputes only when the job is edited.
            TotalJobPrice = legacy.TotalJobPrice ?? 0,
            TotalJobProfit = legacy.TotalJobProfit ?? 0,
            CreatedAtUtc = NzTime.ToUtc(legacy.DateAdded),
            UpdatedAtUtc = NzTime.ToUtc(legacy.DateEdited),
        };

        return new JobMapResult(job, flagRaw);
    }

    /// <summary>
    /// Status table finalized from real data (design §3.4). An Active NewInvoice overrides
    /// everything. "Waiting Customer" and unknown values map with a flag.
    /// </summary>
    public static (JobStatus Status, string? FlagRaw) MapStatus(string? raw, bool hasActiveNewInvoice)
    {
        if (hasActiveNewInvoice)
        {
            return (JobStatus.Invoiced, null);
        }

        var value = raw?.Trim() ?? string.Empty;
        return value.ToUpperInvariant() switch
        {
            "DONE" => (JobStatus.Completed, null),
            "IN PROGRESS" => (JobStatus.InProgress, null),
            "SCHEDULED" => (JobStatus.Open, null),
            "WAITING FOR PARTS" => (JobStatus.AwaitingParts, null),
            "WAITING CUSTOMER" => (JobStatus.InProgress, value),
            _ => (JobStatus.Completed, value.Length == 0 ? "(none)" : value),
        };
    }

    private static string BuildImportedNotes(LegacyJob legacy, IReadOnlyList<string> labourNames, string? mechanicName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Imported from legacy system (Job #{legacy.JobId})");
        sb.AppendLine($"Issue: {legacy.Issue.Trim()}");

        if (legacy.DateStarted is not null || legacy.DateFinished is not null)
        {
            sb.AppendLine($"Started: {legacy.DateStarted?.ToString("yyyy-MM-dd") ?? "?"} · Finished: {legacy.DateFinished?.ToString("yyyy-MM-dd") ?? "?"}");
        }

        if (!string.IsNullOrWhiteSpace(mechanicName))
        {
            sb.AppendLine($"Mechanic: {mechanicName.Trim()}");
        }

        var names = labourNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (names.Count > 0)
        {
            sb.AppendLine($"Labour: {string.Join("; ", names)}");
        }

        if (!string.IsNullOrWhiteSpace(legacy.Notes))
        {
            sb.AppendLine();
            sb.AppendLine(legacy.Notes.Trim());
        }

        var notes = sb.ToString().TrimEnd();
        return notes.Length <= JobNotesMaxLength ? notes : notes[..JobNotesMaxLength];
    }
}
