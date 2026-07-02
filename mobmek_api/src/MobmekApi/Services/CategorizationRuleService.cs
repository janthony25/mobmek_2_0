using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class CategorizationRuleService(AppDbContext db, ICashFlowAuditService audit) : ICategorizationRuleService
{
    private static readonly string[] ValidMatchFields = ["Description", "Counterparty", "Either"];
    private static readonly string[] ValidMatchTypes = ["Contains", "StartsWith", "Equals"];
    private static readonly string[] ValidDirections = ["In", "Out"];
    private static readonly string[] ValidGstTreatments = ["Taxable", "Exempt", "ZeroRated"];

    public async Task<IReadOnlyList<CategorizationRuleDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rules = await db.CategorizationRules.AsNoTracking()
            .Include(r => r.SetCategory)
            .Include(r => r.SetPayee)
            .OrderBy(r => r.Priority).ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);
        return rules.Select(ToDto).ToList();
    }

    public async Task<CategorizationRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await db.CategorizationRules.AsNoTracking()
            .Include(r => r.SetCategory)
            .Include(r => r.SetPayee)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        return rule is null ? null : ToDto(rule);
    }

    public async Task<(CategorizationRuleDto? Rule, CategorizationRuleWriteError Error)> CreateAsync(
        CreateCategorizationRuleRequest request, CancellationToken cancellationToken = default)
    {
        var error = await ValidateAsync(request.MatchField, request.MatchType, request.Direction,
            request.AmountMin, request.AmountMax, request.SetCategoryId, request.SetGstTreatment, request.SetPayeeId, cancellationToken);
        if (error != CategorizationRuleWriteError.None)
        {
            return (null, error);
        }

        var rule = new CategorizationRule
        {
            Name = request.Name.Trim(),
            Priority = request.Priority,
            MatchField = request.MatchField,
            MatchType = request.MatchType,
            MatchValue = request.MatchValue.Trim(),
            Direction = request.Direction,
            AmountMin = request.AmountMin,
            AmountMax = request.AmountMax,
            SetCategoryId = request.SetCategoryId,
            SetGstTreatment = request.SetGstTreatment,
            SetPayeeId = request.SetPayeeId,
            IsActive = request.IsActive,
        };

        db.CategorizationRules.Add(rule);
        audit.Record("CategorizationRule", rule.Id, "Created", $"Rule \"{rule.Name}\" created");
        await db.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(rule.Id, cancellationToken), CategorizationRuleWriteError.None);
    }

    public async Task<(CategorizationRuleDto? Rule, CategorizationRuleWriteError Error)> UpdateAsync(
        Guid id, UpdateCategorizationRuleRequest request, CancellationToken cancellationToken = default)
    {
        var rule = await db.CategorizationRules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (rule is null)
        {
            return (null, CategorizationRuleWriteError.NotFound);
        }

        var error = await ValidateAsync(request.MatchField, request.MatchType, request.Direction,
            request.AmountMin, request.AmountMax, request.SetCategoryId, request.SetGstTreatment, request.SetPayeeId, cancellationToken);
        if (error != CategorizationRuleWriteError.None)
        {
            return (null, error);
        }

        var changes = new List<AuditFieldChange>();
        AuditDiff.Add(changes, "Name", rule.Name, request.Name.Trim());
        AuditDiff.Add(changes, "Priority", rule.Priority, request.Priority);
        AuditDiff.Add(changes, "Match field", rule.MatchField, request.MatchField);
        AuditDiff.Add(changes, "Match type", rule.MatchType, request.MatchType);
        AuditDiff.Add(changes, "Match value", rule.MatchValue, request.MatchValue.Trim());
        AuditDiff.Add(changes, "Direction", rule.Direction, request.Direction);
        AuditDiff.Add(changes, "Amount min", rule.AmountMin, request.AmountMin);
        AuditDiff.Add(changes, "Amount max", rule.AmountMax, request.AmountMax);
        AuditDiff.Add(changes, "Category", rule.SetCategoryId, request.SetCategoryId);
        AuditDiff.Add(changes, "GST", rule.SetGstTreatment, request.SetGstTreatment);
        AuditDiff.Add(changes, "Payee", rule.SetPayeeId, request.SetPayeeId);
        AuditDiff.Add(changes, "Active", rule.IsActive, request.IsActive);

        rule.Name = request.Name.Trim();
        rule.Priority = request.Priority;
        rule.MatchField = request.MatchField;
        rule.MatchType = request.MatchType;
        rule.MatchValue = request.MatchValue.Trim();
        rule.Direction = request.Direction;
        rule.AmountMin = request.AmountMin;
        rule.AmountMax = request.AmountMax;
        rule.SetCategoryId = request.SetCategoryId;
        rule.SetGstTreatment = request.SetGstTreatment;
        rule.SetPayeeId = request.SetPayeeId;
        rule.IsActive = request.IsActive;

        if (changes.Count > 0)
        {
            audit.Record("CategorizationRule", rule.Id, "Updated", AuditDiff.Summarize(changes), changes);
        }

        await db.SaveChangesAsync(cancellationToken);
        return (await GetByIdAsync(id, cancellationToken), CategorizationRuleWriteError.None);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await db.CategorizationRules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (rule is null)
        {
            return false;
        }

        db.CategorizationRules.Remove(rule);
        audit.Record("CategorizationRule", rule.Id, "Deleted", $"Rule \"{rule.Name}\" deleted");
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<RuleSuggestionDto?> SuggestAsync(RuleSuggestionRequest request, CancellationToken cancellationToken = default)
    {
        var rules = await db.CategorizationRules.AsNoTracking()
            .Include(r => r.SetCategory)
            .Include(r => r.SetPayee)
            .Where(r => r.IsActive)
            .OrderBy(r => r.Priority).ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);

        var winner = rules.FirstOrDefault(r => Matches(r, request.Description, request.Counterparty, request.Direction, request.Amount));
        if (winner is null)
        {
            return null;
        }

        return new RuleSuggestionDto(winner.Id, winner.Name, winner.SetCategoryId, winner.SetCategory?.Name ?? string.Empty,
            winner.SetGstTreatment, winner.SetPayeeId, winner.SetPayee?.Name);
    }

    public async Task<(ApplyRuleResultDto? Result, CategorizationRuleWriteError Error)> ApplyToExistingAsync(
        Guid id, bool commit, CancellationToken cancellationToken = default)
    {
        var rule = await db.CategorizationRules.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (rule is null)
        {
            return (null, CategorizationRuleWriteError.NotFound);
        }

        var lockDate = (await db.CashFlowSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken))?.LockDate;

        // Managed rows (invoice-posted, transfer legs), reconciled rows and locked periods are
        // untouchable; split lines are eligible because each line owns its own category.
        var candidates = db.CashTransactions
            .Where(t => t.InvoiceId == null && t.TransferGroupId == null && t.Status != "Reconciled");
        if (lockDate is not null)
        {
            candidates = candidates.Where(t => t.Date > lockDate);
        }

        // Text matching happens in memory so Suggest, import and retro-apply all share one matcher.
        var rows = await candidates.ToListAsync(cancellationToken);
        var matched = rows.Where(t => Matches(rule, t.Description, t.Counterparty, t.Direction, t.Amount)).ToList();

        string? payeeName = null;
        if (rule.SetPayeeId is not null)
        {
            payeeName = (await db.Payees.AsNoTracking().FirstOrDefaultAsync(p => p.Id == rule.SetPayeeId, cancellationToken))?.Name;
        }

        var updated = 0;
        foreach (var t in matched)
        {
            var changes = new List<AuditFieldChange>();
            AuditDiff.Add(changes, "Category", t.CategoryId, rule.SetCategoryId);
            if (rule.SetGstTreatment is not null)
            {
                AuditDiff.Add(changes, "GST", t.GstTreatment, rule.SetGstTreatment);
            }

            if (rule.SetPayeeId is not null)
            {
                AuditDiff.Add(changes, "Payee", t.PayeeId, rule.SetPayeeId);
            }

            if (changes.Count == 0)
            {
                continue;
            }

            updated++;
            if (!commit)
            {
                continue;
            }

            t.CategoryId = rule.SetCategoryId;
            if (rule.SetGstTreatment is not null)
            {
                t.GstTreatment = rule.SetGstTreatment;
            }

            if (rule.SetPayeeId is not null)
            {
                t.PayeeId = rule.SetPayeeId;
                t.Counterparty = payeeName ?? t.Counterparty;
            }

            audit.Record("CashTransaction", t.Id, "Updated", $"Rule \"{rule.Name}\": {AuditDiff.Summarize(changes)}", changes);
        }

        if (commit && updated > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return (new ApplyRuleResultDto(matched.Count, updated), CategorizationRuleWriteError.None);
    }

    /// <summary>
    /// The one matcher shared by Suggest, retro-apply and (Phase 2) statement import, so a
    /// rule can never behave differently between paths.
    /// </summary>
    internal static bool Matches(CategorizationRule rule, string? description, string? counterparty, string? direction, decimal? amount)
    {
        if (rule.Direction is not null && direction is not null && rule.Direction != direction)
        {
            return false;
        }

        if (amount is not null && ((rule.AmountMin is not null && amount < rule.AmountMin)
            || (rule.AmountMax is not null && amount > rule.AmountMax)))
        {
            return false;
        }

        var value = rule.MatchValue.Trim();
        return rule.MatchField switch
        {
            "Description" => MatchesText(rule.MatchType, description, value),
            "Counterparty" => MatchesText(rule.MatchType, counterparty, value),
            _ => MatchesText(rule.MatchType, description, value) || MatchesText(rule.MatchType, counterparty, value),
        };
    }

    private static bool MatchesText(string matchType, string? text, string value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return matchType switch
        {
            "StartsWith" => text.StartsWith(value, StringComparison.OrdinalIgnoreCase),
            "Equals" => text.Equals(value, StringComparison.OrdinalIgnoreCase),
            _ => text.Contains(value, StringComparison.OrdinalIgnoreCase),
        };
    }

    private async Task<CategorizationRuleWriteError> ValidateAsync(
        string matchField, string matchType, string? direction, decimal? amountMin, decimal? amountMax,
        Guid setCategoryId, string? setGstTreatment, Guid? setPayeeId, CancellationToken cancellationToken)
    {
        if (!ValidMatchFields.Contains(matchField))
        {
            return CategorizationRuleWriteError.InvalidMatchField;
        }

        if (!ValidMatchTypes.Contains(matchType))
        {
            return CategorizationRuleWriteError.InvalidMatchType;
        }

        if (direction is not null && !ValidDirections.Contains(direction))
        {
            return CategorizationRuleWriteError.InvalidDirection;
        }

        if (amountMin is not null && amountMax is not null && amountMin > amountMax)
        {
            return CategorizationRuleWriteError.InvalidAmountBand;
        }

        if (setGstTreatment is not null && !ValidGstTreatments.Contains(setGstTreatment))
        {
            return CategorizationRuleWriteError.InvalidGstTreatment;
        }

        if (!await db.TransactionCategories.AnyAsync(c => c.Id == setCategoryId, cancellationToken))
        {
            return CategorizationRuleWriteError.CategoryNotFound;
        }

        if (setPayeeId is not null && !await db.Payees.AnyAsync(p => p.Id == setPayeeId, cancellationToken))
        {
            return CategorizationRuleWriteError.PayeeNotFound;
        }

        return CategorizationRuleWriteError.None;
    }

    private static CategorizationRuleDto ToDto(CategorizationRule r) =>
        new(r.Id, r.Name, r.Priority, r.MatchField, r.MatchType, r.MatchValue, r.Direction,
            r.AmountMin, r.AmountMax, r.SetCategoryId, r.SetCategory?.Name ?? string.Empty,
            r.SetGstTreatment, r.SetPayeeId, r.SetPayee?.Name, r.IsActive, r.CreatedAtUtc, r.UpdatedAtUtc);
}
