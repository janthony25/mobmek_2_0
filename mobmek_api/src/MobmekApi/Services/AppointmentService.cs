using System.Linq.Expressions;
using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class AppointmentService(AppDbContext db) : IAppointmentService
{
    // Inline projection so EF resolves linked names via joins.
    private static readonly Expression<Func<Appointment, AppointmentDto>> ToDto =
        a => new AppointmentDto(
            a.Id,
            a.Title,
            a.StartUtc,
            a.EndUtc,
            a.Status,
            a.Notes,
            a.ContactName,
            a.ContactPhone,
            a.VehicleDescription,
            a.CustomerId,
            a.Customer != null ? a.Customer.FirstName + " " + a.Customer.LastName : null,
            a.CarId,
            a.Car != null
                ? a.Car.CarMake!.Name + " " + a.Car.CarModel!.Name + " (" + a.Car.Rego + ")"
                : null,
            a.JobId,
            a.Job != null ? a.Job.Title : null,
            a.MechanicId,
            a.Mechanic != null ? a.Mechanic.FirstName + " " + a.Mechanic.LastName : null,
            a.GoogleEventId,
            a.CreatedAtUtc,
            a.UpdatedAtUtc);

    public async Task<IReadOnlyList<AppointmentDto>> GetAllAsync(
        DateTime? from = null,
        DateTime? to = null,
        AppointmentStatus? status = null,
        Guid? mechanicId = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = db.Appointments.AsNoTracking();

        // Overlap semantics: anything that intersects [from, to), so appointments
        // spanning a boundary still show on both sides of it.
        if (from is { } f)
        {
            query = query.Where(a => a.EndUtc > f);
        }

        if (to is { } t)
        {
            query = query.Where(a => a.StartUtc < t);
        }

        if (status is { } s)
        {
            query = query.Where(a => a.Status == s);
        }

        if (mechanicId is { } m)
        {
            query = query.Where(a => a.MechanicId == m);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            // ToLower().Contains translates to LOWER(...) LIKE on Postgres and also works
            // on the in-memory test provider (unlike EF.Functions.ILike). Covers both the
            // linked records (customer name, car rego) and the soft phone-call fields.
            var term = search.Trim().ToLower();
            query = query.Where(a =>
                a.Title.ToLower().Contains(term) ||
                (a.ContactName != null && a.ContactName.ToLower().Contains(term)) ||
                (a.ContactPhone != null && a.ContactPhone.ToLower().Contains(term)) ||
                (a.VehicleDescription != null && a.VehicleDescription.ToLower().Contains(term)) ||
                (a.Customer != null && (a.Customer.FirstName + " " + a.Customer.LastName).ToLower().Contains(term)) ||
                (a.Car != null && a.Car.Rego.ToLower().Contains(term)));
        }

        return await query
            .OrderBy(a => a.StartUtc)
            .Select(ToDto)
            .ToListAsync(cancellationToken);
    }

    public async Task<AppointmentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await db.Appointments
            .AsNoTracking()
            .Where(a => a.Id == id)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(AppointmentDto? Appointment, AppointmentWriteError Error)> CreateAsync(
        CreateAppointmentRequest request, CancellationToken cancellationToken = default)
    {
        var error = await ValidateAsync(
            request.StartUtc, request.EndUtc, request.ContactName, request.ContactPhone,
            request.CustomerId, request.CarId, request.JobId, request.MechanicId, cancellationToken);
        if (error != AppointmentWriteError.None)
        {
            return (null, error);
        }

        var appointment = new Appointment
        {
            Title = request.Title,
            StartUtc = request.StartUtc,
            EndUtc = request.EndUtc,
            Status = request.Status,
            Notes = request.Notes,
            ContactName = request.ContactName,
            ContactPhone = request.ContactPhone,
            VehicleDescription = request.VehicleDescription,
            CustomerId = request.CustomerId,
            CarId = request.CarId,
            JobId = request.JobId,
            MechanicId = request.MechanicId,
        };

        db.Appointments.Add(appointment);
        await db.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(appointment.Id, cancellationToken), AppointmentWriteError.None);
    }

    public async Task<(AppointmentDto? Appointment, AppointmentWriteError Error)> UpdateAsync(
        Guid id, UpdateAppointmentRequest request, CancellationToken cancellationToken = default)
    {
        var appointment = await db.Appointments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (appointment is null)
        {
            return (null, AppointmentWriteError.NotFound);
        }

        var error = await ValidateAsync(
            request.StartUtc, request.EndUtc, request.ContactName, request.ContactPhone,
            request.CustomerId, request.CarId, request.JobId, request.MechanicId, cancellationToken);
        if (error != AppointmentWriteError.None)
        {
            return (null, error);
        }

        appointment.Title = request.Title;
        appointment.StartUtc = request.StartUtc;
        appointment.EndUtc = request.EndUtc;
        appointment.Status = request.Status;
        appointment.Notes = request.Notes;
        appointment.ContactName = request.ContactName;
        appointment.ContactPhone = request.ContactPhone;
        appointment.VehicleDescription = request.VehicleDescription;
        appointment.CustomerId = request.CustomerId;
        appointment.CarId = request.CarId;
        appointment.JobId = request.JobId;
        appointment.MechanicId = request.MechanicId;

        await db.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(appointment.Id, cancellationToken), AppointmentWriteError.None);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var appointment = await db.Appointments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (appointment is null)
        {
            return false;
        }

        db.Appointments.Remove(appointment);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<AppointmentWriteError> ValidateAsync(
        DateTime startUtc,
        DateTime endUtc,
        string? contactName,
        string? contactPhone,
        Guid? customerId,
        Guid? carId,
        Guid? jobId,
        Guid? mechanicId,
        CancellationToken cancellationToken)
    {
        if (endUtc <= startUtc)
        {
            return AppointmentWriteError.EndNotAfterStart;
        }

        // An appointment must be reachable: a linked customer, or a name + phone taken over the phone.
        var hasContact = !string.IsNullOrWhiteSpace(contactName) && !string.IsNullOrWhiteSpace(contactPhone);
        if (customerId is null && !hasContact)
        {
            return AppointmentWriteError.MissingContactOrCustomer;
        }

        if (customerId is { } cid && !await db.Customers.AnyAsync(c => c.Id == cid, cancellationToken))
        {
            return AppointmentWriteError.CustomerNotFound;
        }

        if (carId is { } carGuid)
        {
            if (customerId is null)
            {
                return AppointmentWriteError.CarWithoutCustomer;
            }

            var car = await db.Cars.AsNoTracking().FirstOrDefaultAsync(c => c.Id == carGuid, cancellationToken);
            if (car is null)
            {
                return AppointmentWriteError.CarNotFound;
            }

            if (car.CustomerId != customerId)
            {
                return AppointmentWriteError.CarNotOwnedByCustomer;
            }
        }

        if (jobId is { } jid)
        {
            var job = await db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == jid, cancellationToken);
            if (job is null)
            {
                return AppointmentWriteError.JobNotFound;
            }

            if (customerId is { } jobCustomer && job.CustomerId != jobCustomer)
            {
                return AppointmentWriteError.JobCustomerMismatch;
            }
        }

        if (mechanicId is { } mid && !await db.Employees.AnyAsync(e => e.Id == mid, cancellationToken))
        {
            return AppointmentWriteError.MechanicNotFound;
        }

        return AppointmentWriteError.None;
    }
}
