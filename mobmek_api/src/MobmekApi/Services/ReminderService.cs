using System.Linq.Expressions;
using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class ReminderService(AppDbContext db) : IReminderService
{
    // Inline projection so EF resolves customer/car/template labels via joins.
    private static readonly Expression<Func<Reminder, ReminderDto>> ToDto =
        r => new ReminderDto(
            r.Id,
            r.CustomerId,
            r.Customer!.FirstName + " " + r.Customer.LastName,
            r.CarId,
            r.Car == null ? null : r.Car.CarMake!.Name + " " + r.Car.CarModel!.Name + " (" + r.Car.Rego + ")",
            r.ReminderTemplateId,
            r.ReminderTemplate == null ? null : r.ReminderTemplate.Name,
            r.Title,
            r.DueDate,
            r.IsDone,
            r.Notes,
            r.CreatedAtUtc,
            r.UpdatedAtUtc);

    public async Task<IReadOnlyList<ReminderDto>> GetAllAsync(Guid? customerId = null, Guid? carId = null, bool includeDone = true, CancellationToken cancellationToken = default)
    {
        var query = db.Reminders.AsNoTracking();

        if (customerId is { } cid)
        {
            query = query.Where(r => r.CustomerId == cid);
        }

        if (carId is { } carIdValue)
        {
            query = query.Where(r => r.CarId == carIdValue);
        }

        if (!includeDone)
        {
            query = query.Where(r => !r.IsDone);
        }

        // Outstanding (soonest due) first; done ones sink to the bottom.
        return await query
            .OrderBy(r => r.IsDone)
            .ThenBy(r => r.DueDate)
            .Select(ToDto)
            .ToListAsync(cancellationToken);
    }

    public async Task<ReminderDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await db.Reminders
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(ReminderDto? Reminder, ReminderWriteError Error)> CreateAsync(CreateReminderRequest request, CancellationToken cancellationToken = default)
    {
        var error = await ValidateReferencesAsync(request.CustomerId, request.CarId, request.ReminderTemplateId, cancellationToken);
        if (error != ReminderWriteError.None)
        {
            return (null, error);
        }

        var reminder = new Reminder
        {
            CustomerId = request.CustomerId,
            CarId = request.CarId,
            ReminderTemplateId = request.ReminderTemplateId,
            Title = request.Title,
            DueDate = request.DueDate,
            IsDone = request.IsDone,
            Notes = request.Notes,
        };

        db.Reminders.Add(reminder);
        await db.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(reminder.Id, cancellationToken), ReminderWriteError.None);
    }

    public async Task<(ReminderDto? Reminder, ReminderWriteError Error)> UpdateAsync(Guid id, UpdateReminderRequest request, CancellationToken cancellationToken = default)
    {
        var reminder = await db.Reminders.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (reminder is null)
        {
            return (null, ReminderWriteError.NotFound);
        }

        // Customer is fixed; the car may change but must still belong to that customer.
        var error = await ValidateReferencesAsync(reminder.CustomerId, request.CarId, request.ReminderTemplateId, cancellationToken);
        if (error != ReminderWriteError.None)
        {
            return (null, error);
        }

        reminder.CarId = request.CarId;
        reminder.ReminderTemplateId = request.ReminderTemplateId;
        reminder.Title = request.Title;
        reminder.DueDate = request.DueDate;
        reminder.IsDone = request.IsDone;
        reminder.Notes = request.Notes;

        await db.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(reminder.Id, cancellationToken), ReminderWriteError.None);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var reminder = await db.Reminders.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (reminder is null)
        {
            return false;
        }

        db.Reminders.Remove(reminder);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<ReminderWriteError> ValidateReferencesAsync(Guid customerId, Guid? carId, Guid? templateId, CancellationToken cancellationToken)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == customerId, cancellationToken))
        {
            return ReminderWriteError.CustomerNotFound;
        }

        if (carId is { } cid)
        {
            var car = await db.Cars.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cid, cancellationToken);
            if (car is null)
            {
                return ReminderWriteError.CarNotFound;
            }

            if (car.CustomerId != customerId)
            {
                return ReminderWriteError.CarNotOwnedByCustomer;
            }
        }

        if (templateId is { } tid
            && !await db.ReminderTemplates.AnyAsync(t => t.Id == tid, cancellationToken))
        {
            return ReminderWriteError.TemplateNotFound;
        }

        return ReminderWriteError.None;
    }
}
