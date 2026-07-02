using MobmekApi.Data;
using MobmekApi.DTOs;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

/// <summary>
/// Builds the deterministic daily cash-balance projection (design §3.1). Every figure comes
/// from existing ledger/invoice/schedule data — nothing here is AI-generated or estimated
/// beyond the documented payment-behaviour model and scenario multipliers.
/// </summary>
public class ForecastService(AppDbContext db, ICashAccountService cashAccountService) : IForecastService
{
    private static readonly string[] ValidScenarios = ["BestCase", "Expected", "WorstCase"];
    private const int MaxHorizonDays = 366;
    private const int MaxDailyResolutionDays = 90;

    public async Task<ForecastResultDto> ProjectAsync(int horizonDays, string? scenario, CancellationToken cancellationToken = default)
    {
        horizonDays = Math.Clamp(horizonDays, 1, MaxHorizonDays);
        var normalizedScenario = ValidScenarios.Contains(scenario) ? scenario! : "Expected";

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizonEnd = today.AddDays(horizonDays);
        var openingBalance = await cashAccountService.GetTotalBalanceAsync(cancellationToken);
        var buffer = (await db.CashFlowSettings.AsNoTracking().OrderBy(s => s.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken))
            ?.SafetyBufferAmount ?? 0m;

        var requestedMovements = await BuildMovementsAsync(today, horizonEnd, normalizedScenario, cancellationToken);
        var (dailySeries, shortageIfRequestedIsExpected) = BuildDailySeries(today, horizonEnd, openingBalance, requestedMovements, buffer);

        // The shortage alert always reflects Expected, even when a different scenario was requested.
        var shortageDate = shortageIfRequestedIsExpected;
        if (normalizedScenario != "Expected")
        {
            var expectedMovements = await BuildMovementsAsync(today, horizonEnd, "Expected", cancellationToken);
            (_, shortageDate) = BuildDailySeries(today, horizonEnd, openingBalance, expectedMovements, buffer);
        }

        var monthlyPoints = RollUpMonthly(dailySeries);
        var dailyPoints = horizonDays <= MaxDailyResolutionDays ? dailySeries : [];

        return new ForecastResultDto(horizonDays, normalizedScenario, openingBalance, dailyPoints, monthlyPoints, shortageDate);
    }

    // Per-day (In, Out) movements for the scenario, composed from receivables + recurring + planned.
    private async Task<Dictionary<DateOnly, (decimal In, decimal Out)>> BuildMovementsAsync(
        DateOnly from, DateOnly to, string scenario, CancellationToken cancellationToken)
    {
        var movements = new Dictionary<DateOnly, (decimal In, decimal Out)>();

        void Add(DateOnly date, string direction, decimal amount)
        {
            if (amount == 0)
            {
                return;
            }

            // Anything already overdue lands at the start of the horizon rather than dropping off;
            // anything past the horizon end doesn't affect this projection.
            if (date < from)
            {
                date = from;
            }

            if (date > to)
            {
                return;
            }

            var current = movements.GetValueOrDefault(date, (In: 0m, Out: 0m));
            movements[date] = direction == "In" ? (current.In + amount, current.Out) : (current.In, current.Out + amount);
        }

        await AddReceivablesAsync(Add, scenario, cancellationToken);
        await AddRecurringAsync(Add, from, to, scenario, cancellationToken);
        await AddPlannedAsync(Add, from, to, scenario, cancellationToken);

        return movements;
    }

    private async Task AddReceivablesAsync(Action<DateOnly, string, decimal> add, string scenario, CancellationToken cancellationToken)
    {
        var unpaid = await db.Invoices.AsNoTracking()
            .Where(i => !i.IsPaid && i.Status == "Active" && i.DueDate != null)
            .Select(i => new { DueDate = i.DueDate!.Value, i.TotalAmount, CustomerId = i.Job!.CustomerId })
            .ToListAsync(cancellationToken);

        if (unpaid.Count == 0)
        {
            return;
        }

        var (receivablesPct, extraDelayDays, useCustomerLag) = scenario switch
        {
            "BestCase" => (1.00m, 0, false),
            "WorstCase" => (0.85m, 14, true),
            _ => (1.00m, 0, true), // Expected
        };

        Dictionary<Guid, double> lagByCustomer = [];
        double businessWideLag = 0;
        if (useCustomerLag)
        {
            (lagByCustomer, businessWideLag) = await ComputePaymentLagAsync(cancellationToken);
        }

        foreach (var invoice in unpaid)
        {
            var lagDays = useCustomerLag
                ? (int)Math.Round(lagByCustomer.GetValueOrDefault(invoice.CustomerId, businessWideLag))
                : 0;
            var expectedDate = invoice.DueDate.AddDays(lagDays + extraDelayDays);
            add(expectedDate, "In", invoice.TotalAmount * receivablesPct);
        }
    }

    /// <summary>
    /// Payment-behaviour model (design §3.2): per-customer median days-late over their paid
    /// invoices, falling back to the business-wide median, falling back to 0.
    /// </summary>
    private async Task<(Dictionary<Guid, double> PerCustomerMedianDays, double BusinessWideMedianDays)> ComputePaymentLagAsync(
        CancellationToken cancellationToken)
    {
        var paid = await db.Invoices.AsNoTracking()
            .Where(i => i.IsPaid && i.Status == "Active" && i.DueDate != null && i.DatePaid != null)
            .Select(i => new { CustomerId = i.Job!.CustomerId, DueDate = i.DueDate!.Value, DatePaid = i.DatePaid!.Value })
            .ToListAsync(cancellationToken);

        var lagDays = paid.Select(p => (CustomerId: p.CustomerId, Days: (double)(p.DatePaid.DayNumber - p.DueDate.DayNumber))).ToList();

        var perCustomer = lagDays
            .GroupBy(p => p.CustomerId)
            .ToDictionary(g => g.Key, g => Median(g.Select(p => p.Days)));

        var businessWide = lagDays.Count > 0 ? Median(lagDays.Select(p => p.Days)) : 0;

        return (perCustomer, businessWide);
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        if (sorted.Count == 0)
        {
            return 0;
        }

        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2.0 : sorted[mid];
    }

    private async Task AddRecurringAsync(
        Action<DateOnly, string, decimal> add, DateOnly from, DateOnly to, string scenario, CancellationToken cancellationToken)
    {
        var active = await db.RecurringTransactions.AsNoTracking().Where(r => !r.IsPaused).ToListAsync(cancellationToken);
        if (active.Count == 0)
        {
            return;
        }

        var (inMultiplier, outMultiplier) = scenario switch
        {
            "BestCase" => (1.10m, 0.95m),
            "WorstCase" => (0.85m, 1.10m),
            _ => (1.00m, 1.00m), // Expected
        };

        var recurringIds = active.Select(r => r.Id).ToList();
        var posted = await db.CashTransactions.AsNoTracking()
            .Where(t => t.RecurringTransactionId != null && recurringIds.Contains(t.RecurringTransactionId!.Value))
            .Select(t => new { RecurringTransactionId = t.RecurringTransactionId!.Value, t.Date })
            .ToListAsync(cancellationToken);
        var postedByRecurring = posted
            .GroupBy(p => p.RecurringTransactionId)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Date).ToHashSet());

        foreach (var r in active)
        {
            var postedDates = postedByRecurring.GetValueOrDefault(r.Id, []);
            var multiplier = r.Direction == "In" ? inMultiplier : outMultiplier;
            foreach (var date in RecurringOccurrences.Expand(r.Frequency, r.Interval, r.AnchorDate, r.EndDate, from, to))
            {
                if (!postedDates.Contains(date))
                {
                    add(date, r.Direction, r.Amount * multiplier);
                }
            }
        }
    }

    private async Task AddPlannedAsync(
        Action<DateOnly, string, decimal> add, DateOnly from, DateOnly to, string scenario, CancellationToken cancellationToken)
    {
        var planned = await db.PlannedTransactions.AsNoTracking()
            .Where(p => p.Status == "Planned" && p.ExpectedDate >= from && p.ExpectedDate <= to)
            .ToListAsync(cancellationToken);

        foreach (var p in planned.Where(p => p.ScenarioTag is null || p.ScenarioTag == scenario))
        {
            add(p.ExpectedDate, p.Direction, p.Amount);
        }
    }

    private static (List<ForecastPointDto> Daily, DateOnly? ShortageDate) BuildDailySeries(
        DateOnly from, DateOnly to, decimal openingBalance, Dictionary<DateOnly, (decimal In, decimal Out)> movements, decimal buffer)
    {
        var points = new List<ForecastPointDto>();
        DateOnly? shortageDate = null;
        var running = openingBalance;

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var (inAmount, outAmount) = movements.GetValueOrDefault(date, (In: 0m, Out: 0m));
            var opening = running;
            var closing = opening + inAmount - outAmount;
            points.Add(new ForecastPointDto(date, opening, inAmount, outAmount, closing));

            if (shortageDate is null && closing < buffer)
            {
                shortageDate = date;
            }

            running = closing;
        }

        return (points, shortageDate);
    }

    private static List<ForecastMonthPointDto> RollUpMonthly(List<ForecastPointDto> daily) =>
        daily
            .GroupBy(p => (p.Date.Year, p.Date.Month))
            .OrderBy(g => g.Key)
            .Select(g => new ForecastMonthPointDto(g.Key.Year, g.Key.Month, g.Sum(p => p.In), g.Sum(p => p.Out), g.Last().ClosingBalance))
            .ToList();
}
