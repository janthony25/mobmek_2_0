using System.Linq.Expressions;
using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class JobService(AppDbContext db) : IJobService
{
    private const int MaxPageSize = 200;

    // Inline projection so EF resolves customer/car/mechanic names via joins.
    private static readonly Expression<Func<Job, JobDto>> ToDto =
        j => new JobDto(
            j.Id,
            j.CustomerId,
            j.Customer!.FirstName + " " + j.Customer.LastName,
            j.CarId,
            j.Car!.CarMake!.Name + " " + j.Car.CarModel!.Name + " (" + j.Car.Rego + ")",
            j.Title,
            j.Status,
            j.Odometer,
            j.JobNotes,
            j.InvoiceNotes,
            j.DiscountType,
            j.DiscountValue,
            j.TotalJobPrice,
            j.TotalJobProfit,
            j.Mechanics
                .Select(m => new JobMechanicDto(m.EmployeeId, m.Employee!.FirstName + " " + m.Employee.LastName))
                .ToList(),
            j.CreatedAtUtc,
            j.UpdatedAtUtc);

    public async Task<IReadOnlyList<JobDto>> GetAllAsync(Guid? customerId = null, CancellationToken cancellationToken = default)
    {
        var query = db.Jobs.AsNoTracking();

        if (customerId is { } id)
        {
            query = query.Where(j => j.CustomerId == id);
        }

        return await query
            .OrderByDescending(j => j.CreatedAtUtc)
            .Select(ToDto)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<JobDto>> GetPagedAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db.Jobs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            // ToLower().Contains translates to LOWER(...) LIKE on Postgres and also works
            // on the in-memory test provider (unlike EF.Functions.ILike).
            var term = search.Trim().ToLower();
            query = query.Where(j =>
                j.Title.ToLower().Contains(term) ||
                (j.Customer!.FirstName + " " + j.Customer.LastName).ToLower().Contains(term) ||
                j.Car!.CarMake!.Name.ToLower().Contains(term) ||
                j.Car.CarModel!.Name.ToLower().Contains(term) ||
                j.Car.Rego.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(j => j.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToDto)
            .ToListAsync(cancellationToken);

        return new PagedResult<JobDto>(items, totalCount, page, pageSize);
    }

    public async Task<JobDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await db.Jobs
            .AsNoTracking()
            .Where(j => j.Id == id)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(JobDto? Job, JobWriteError Error)> CreateAsync(CreateJobRequest request, CancellationToken cancellationToken = default)
    {
        var carError = await ValidateCustomerAndCarAsync(request.CustomerId, request.CarId, cancellationToken);
        if (carError != JobWriteError.None)
        {
            return (null, carError);
        }

        var job = new Job
        {
            CustomerId = request.CustomerId,
            CarId = request.CarId,
            Title = request.Title,
            Status = request.Status,
            Odometer = request.Odometer,
            JobNotes = request.JobNotes,
            InvoiceNotes = request.InvoiceNotes,
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
        };

        db.Jobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(job.Id, cancellationToken), JobWriteError.None);
    }

    public async Task<(JobDto? Job, JobWriteError Error)> UpdateAsync(Guid id, UpdateJobRequest request, CancellationToken cancellationToken = default)
    {
        var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (job is null)
        {
            return (null, JobWriteError.NotFound);
        }

        // Customer is fixed; the car may change but must still belong to that customer.
        var carError = await ValidateCustomerAndCarAsync(job.CustomerId, request.CarId, cancellationToken);
        if (carError != JobWriteError.None)
        {
            return (null, carError);
        }

        job.CarId = request.CarId;
        job.Title = request.Title;
        job.Status = request.Status;
        job.Odometer = request.Odometer;
        job.JobNotes = request.JobNotes;
        job.InvoiceNotes = request.InvoiceNotes;
        job.DiscountType = request.DiscountType;
        job.DiscountValue = request.DiscountValue;

        await db.SaveChangesAsync(cancellationToken);

        // The discount can change totals even when no item/labour/service line did.
        await RecalculateTotalsAsync(job.Id, cancellationToken);

        return (await GetByIdAsync(job.Id, cancellationToken), JobWriteError.None);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (job is null)
        {
            return false;
        }

        db.Jobs.Remove(job);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<(JobDto? Job, JobWriteError Error)> AddMechanicAsync(Guid jobId, Guid employeeId, CancellationToken cancellationToken = default)
    {
        if (!await db.Jobs.AnyAsync(j => j.Id == jobId, cancellationToken))
        {
            return (null, JobWriteError.NotFound);
        }

        if (!await db.Employees.AnyAsync(e => e.Id == employeeId, cancellationToken))
        {
            return (null, JobWriteError.EmployeeNotFound);
        }

        if (await db.JobMechanics.AnyAsync(m => m.JobId == jobId && m.EmployeeId == employeeId, cancellationToken))
        {
            return (null, JobWriteError.MechanicAlreadyAssigned);
        }

        db.JobMechanics.Add(new JobMechanic { JobId = jobId, EmployeeId = employeeId });
        await db.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(jobId, cancellationToken), JobWriteError.None);
    }

    public async Task<bool> RemoveMechanicAsync(Guid jobId, Guid employeeId, CancellationToken cancellationToken = default)
    {
        var link = await db.JobMechanics.FirstOrDefaultAsync(m => m.JobId == jobId && m.EmployeeId == employeeId, cancellationToken);
        if (link is null)
        {
            return false;
        }

        db.JobMechanics.Remove(link);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task RecalculateTotalsAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
        {
            return;
        }

        var itemsTotal = await db.JobItems.Where(i => i.JobId == jobId).SumAsync(i => i.ItemTotal, cancellationToken);
        var itemsProfit = await db.JobItems.Where(i => i.JobId == jobId).SumAsync(i => i.UnitProfit * i.ItemQuantity, cancellationToken);
        var labourTotal = await db.Labour.Where(l => l.JobId == jobId).SumAsync(l => l.TotalAmount, cancellationToken);
        var servicesTotal = await db.JobServiceLines.Where(s => s.JobId == jobId).SumAsync(s => s.LineTotal, cancellationToken);
        var subTotal = itemsTotal + labourTotal + servicesTotal;
        var discountAmount = DiscountCalculator.ComputeAmount(job.DiscountType, job.DiscountValue, subTotal);

        // Labour and services are treated as 100% profit (no cost tracked on them).
        job.TotalJobPrice = subTotal - discountAmount;
        job.TotalJobProfit = itemsProfit + labourTotal + servicesTotal - discountAmount;

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<JobWriteError> ValidateCustomerAndCarAsync(Guid customerId, Guid carId, CancellationToken cancellationToken)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == customerId, cancellationToken))
        {
            return JobWriteError.CustomerNotFound;
        }

        var car = await db.Cars.AsNoTracking().FirstOrDefaultAsync(c => c.Id == carId, cancellationToken);
        if (car is null)
        {
            return JobWriteError.CarNotFound;
        }

        if (car.CustomerId != customerId)
        {
            return JobWriteError.CarNotOwnedByCustomer;
        }

        return JobWriteError.None;
    }
}
