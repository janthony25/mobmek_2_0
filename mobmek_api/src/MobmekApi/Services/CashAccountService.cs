using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class CashAccountService(AppDbContext db) : ICashAccountService
{
    public async Task<IReadOnlyList<CashAccountDto>> GetAllAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var accounts = await db.CashAccounts.AsNoTracking()
            .Where(a => includeArchived || !a.IsArchived)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);

        // One grouped query for all balances instead of a sum per account.
        var sums = await db.CashTransactions.AsNoTracking()
            .GroupBy(t => new { t.AccountId, t.Direction })
            .Select(g => new { g.Key.AccountId, g.Key.Direction, Total = g.Sum(t => t.Amount) })
            .ToListAsync(cancellationToken);

        return accounts
            .Select(a => ToDto(a, a.OpeningBalance
                + sums.Where(s => s.AccountId == a.Id && s.Direction == "In").Sum(s => s.Total)
                - sums.Where(s => s.AccountId == a.Id && s.Direction == "Out").Sum(s => s.Total)))
            .ToList();
    }

    public async Task<CashAccountDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var account = await db.CashAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        return account is null ? null : ToDto(account, await BalanceAsync(account, cancellationToken));
    }

    public async Task<decimal> GetTotalBalanceAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await db.CashAccounts.AsNoTracking().Where(a => !a.IsArchived).ToListAsync(cancellationToken);
        if (accounts.Count == 0)
        {
            return 0m;
        }

        var sums = await db.CashTransactions.AsNoTracking()
            .Where(t => accounts.Select(a => a.Id).Contains(t.AccountId))
            .GroupBy(t => t.Direction)
            .Select(g => new { Direction = g.Key, Total = g.Sum(t => t.Amount) })
            .ToListAsync(cancellationToken);

        var openingTotal = accounts.Sum(a => a.OpeningBalance);
        var inTotal = sums.Where(s => s.Direction == "In").Sum(s => s.Total);
        var outTotal = sums.Where(s => s.Direction == "Out").Sum(s => s.Total);

        return openingTotal + inTotal - outTotal;
    }

    public async Task<CashAccountDto> CreateAsync(CreateCashAccountRequest request, CancellationToken cancellationToken = default)
    {
        var account = new CashAccount
        {
            Name = request.Name,
            Type = request.Type,
            AccountNumber = request.AccountNumber,
            OpeningBalance = request.OpeningBalance,
            OpeningDate = request.OpeningDate,
        };

        db.CashAccounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(account, account.OpeningBalance);
    }

    public async Task<CashAccountDto?> UpdateAsync(Guid id, UpdateCashAccountRequest request, CancellationToken cancellationToken = default)
    {
        var account = await db.CashAccounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (account is null)
        {
            return null;
        }

        account.Name = request.Name;
        account.Type = request.Type;
        account.AccountNumber = request.AccountNumber;
        account.OpeningBalance = request.OpeningBalance;
        account.OpeningDate = request.OpeningDate;
        account.IsArchived = request.IsArchived;
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(account, await BalanceAsync(account, cancellationToken));
    }

    public async Task<CashAccountDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var account = await db.CashAccounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (account is null)
        {
            return CashAccountDeleteResult.NotFound;
        }

        if (await db.CashTransactions.AnyAsync(t => t.AccountId == id, cancellationToken))
        {
            return CashAccountDeleteResult.HasTransactions;
        }

        // Don't leave invoice-payment routing pointing at a dead account.
        var settings = await db.CashFlowSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings is not null)
        {
            if (settings.DefaultAccountId == id) settings.DefaultAccountId = null;
            if (settings.CashAccountId == id) settings.CashAccountId = null;
            if (settings.CardAccountId == id) settings.CardAccountId = null;
            if (settings.BankTransferAccountId == id) settings.BankTransferAccountId = null;
        }

        db.CashAccounts.Remove(account);
        await db.SaveChangesAsync(cancellationToken);

        return CashAccountDeleteResult.Deleted;
    }

    private async Task<decimal> BalanceAsync(CashAccount account, CancellationToken cancellationToken)
    {
        var inTotal = await db.CashTransactions.AsNoTracking()
            .Where(t => t.AccountId == account.Id && t.Direction == "In")
            .SumAsync(t => t.Amount, cancellationToken);
        var outTotal = await db.CashTransactions.AsNoTracking()
            .Where(t => t.AccountId == account.Id && t.Direction == "Out")
            .SumAsync(t => t.Amount, cancellationToken);

        return account.OpeningBalance + inTotal - outTotal;
    }

    private static CashAccountDto ToDto(CashAccount a, decimal currentBalance) =>
        new(a.Id, a.Name, a.Type, a.AccountNumber, a.OpeningBalance, a.OpeningDate, a.IsArchived,
            currentBalance, a.CreatedAtUtc, a.UpdatedAtUtc);
}
