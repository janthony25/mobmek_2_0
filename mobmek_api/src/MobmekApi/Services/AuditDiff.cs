using System.Globalization;
using MobmekApi.DTOs;

namespace MobmekApi.Services;

/// <summary>
/// Builds the field-level before/after list that services hand to
/// <see cref="ICashFlowAuditService.Record"/> for updates.
/// </summary>
public static class AuditDiff
{
    /// <summary>Adds a change entry only when the value actually changed.</summary>
    public static void Add(List<AuditFieldChange> changes, string field, object? oldValue, object? newValue)
    {
        var oldText = Format(oldValue);
        var newText = Format(newValue);
        if (oldText != newText)
        {
            changes.Add(new AuditFieldChange(field, oldText, newText));
        }
    }

    /// <summary>"Amount 120.00 → 150.00; Category Fuel → Parts" — the human line for history views.</summary>
    public static string Summarize(IReadOnlyList<AuditFieldChange> changes) =>
        string.Join("; ", changes.Select(c => $"{c.Field} {c.Old ?? "—"} → {c.New ?? "—"}"));

    private static string? Format(object? value) => value switch
    {
        null => null,
        decimal d => d.ToString("0.00", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        bool b => b ? "yes" : "no",
        _ => value.ToString(),
    };
}
