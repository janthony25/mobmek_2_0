using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class CustomerService(AppDbContext db) : ICustomerService
{
    private const int MaxPageSize = 200;

    public async Task<IReadOnlyList<CustomerDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.Customers
            .AsNoTracking()
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .Select(c => ToDto(c))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<CustomerListItemDto>> GetPagedAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            // ToLower().Contains translates to LOWER(...) LIKE on Postgres and also works
            // on the in-memory test provider (unlike EF.Functions.ILike).
            var term = search.Trim().ToLower();
            query = query.Where(c =>
                (c.FirstName + " " + c.LastName).ToLower().Contains(term) ||
                c.PhoneNumber.ToLower().Contains(term) ||
                (c.EmailAddress != null && c.EmailAddress.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CustomerListItemDto(
                c.Id,
                c.FirstName,
                c.LastName,
                c.PhoneNumber,
                c.EmailAddress,
                c.PhysicalAddress,
                c.Notes,
                c.CreatedAtUtc,
                c.UpdatedAtUtc,
                c.Cars
                    .OrderBy(car => car.CreatedAtUtc)
                    .Select(car => new CustomerCarSummaryDto(
                        car.Id,
                        car.Year,
                        car.CarMake!.Name,
                        car.CarModel!.Name,
                        db.Reminders.Count(r => r.CarId == car.Id && !r.IsDone),
                        db.Reminders
                            .Where(r => r.CarId == car.Id && !r.IsDone)
                            .Min(r => (DateOnly?)r.DueDate)))
                    .ToList(),
                db.Notes.Count(n => n.CustomerId == c.Id && !n.IsDone),
                db.Notes
                    .Where(n => n.CustomerId == c.Id && !n.IsDone)
                    .Min(n => n.DueDate)))
            .ToListAsync(cancellationToken);

        return new PagedResult<CustomerListItemDto>(items, totalCount, page, pageSize);
    }

    public async Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        return customer is null ? null : ToDto(customer);
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var customer = new Customer
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            EmailAddress = request.EmailAddress,
            PhysicalAddress = request.PhysicalAddress,
            Notes = request.Notes,
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(customer);
    }

    public async Task<CustomerDto?> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (customer is null)
        {
            return null;
        }

        customer.FirstName = request.FirstName;
        customer.LastName = request.LastName;
        customer.PhoneNumber = request.PhoneNumber;
        customer.EmailAddress = request.EmailAddress;
        customer.PhysicalAddress = request.PhysicalAddress;
        customer.Notes = request.Notes;

        await db.SaveChangesAsync(cancellationToken);

        return ToDto(customer);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (customer is null)
        {
            return false;
        }

        db.Customers.Remove(customer);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static CustomerDto ToDto(Customer c) =>
        new(c.Id, c.FirstName, c.LastName, c.PhoneNumber, c.EmailAddress, c.PhysicalAddress, c.Notes, c.CreatedAtUtc, c.UpdatedAtUtc);
}
