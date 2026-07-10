using MobmekApi.Entities;

namespace MobmekApi.LegacyImport.Mappers;

/// <summary>
/// Computes printed sequence numbers for imported documents (design §3.5). Within each
/// DocumentType the unnumbered invoices are ordered by original DateAdded (preserved in
/// CreatedAtUtc) and numbered from the current max + 1, so pre-existing documents keep
/// their numbers and anything created afterwards continues the sequence.
/// </summary>
public static class SequenceNumberAssigner
{
    public static List<(Invoice Invoice, int Number)> Assign(
        IReadOnlyList<Invoice> unnumbered,
        Func<string, int> currentMaxForType)
    {
        var assignments = new List<(Invoice, int)>(unnumbered.Count);
        foreach (var group in unnumbered.GroupBy(i => i.DocumentType).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var next = currentMaxForType(group.Key);
            foreach (var invoice in group.OrderBy(i => i.CreatedAtUtc).ThenBy(i => i.Id))
            {
                assignments.Add((invoice, ++next));
            }
        }

        return assignments;
    }
}
