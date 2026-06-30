using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class CustomerService(AppDbContext db) : ICustomerService
{
    public async Task<IReadOnlyList<CustomerDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.Customers
            .AsNoTracking()
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .Select(c => ToDto(c))
            .ToListAsync(cancellationToken);
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
