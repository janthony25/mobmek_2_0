using MobmekApi.Entities;
using MobmekApi.LegacyImport.Mappers;

namespace MobmekApi.LegacyImport.Phases;

/// <summary>
/// Shared item-line insertion for the four document phases. Names longer than the new
/// column's 255 chars are truncated with a flag carrying the full original text, so the
/// report preserves what the database can't (1 known case in the real data).
/// </summary>
internal static class DocumentItemImport
{
    public static void Add(
        ImportContext context,
        Invoice invoice,
        string itemRef,
        string itemName,
        int quantity,
        decimal itemPrice,
        decimal? itemTotal)
    {
        var trimmed = itemName.Trim();
        if (trimmed.Length > DocumentMapper.ItemNameMaxLength)
        {
            var flattened = string.Join(' ', trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            context.Flag(
                "item-name-truncated",
                itemRef,
                $"Item name is {trimmed.Length} chars, truncated to {DocumentMapper.ItemNameMaxLength}. Full text: {flattened}");
        }

        context.Db.InvoiceItems.Add(DocumentMapper.MapItem(invoice.Id, itemName, quantity, itemPrice, itemTotal, invoice.CreatedAtUtc));
    }
}
