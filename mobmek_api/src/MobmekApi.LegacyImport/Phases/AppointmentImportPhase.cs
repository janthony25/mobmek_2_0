using MobmekApi.LegacyImport.Legacy;
using MobmekApi.LegacyImport.Mappers;
using MobmekApi.LegacyImport.Pipeline;

namespace MobmekApi.LegacyImport.Phases;

/// <summary>
/// Imports legacy Appointments (design §3.6). Runs last: hard links resolve through the
/// car map (customer derived via the car) and the old Job.AppointmentId back-link becomes
/// the new Appointment.JobId (first job by lowest id — real data has no multi-job links).
/// </summary>
public sealed class AppointmentImportPhase : ImportPhase
{
    public const string EntityType = "Appointment";

    public override string Name => "appointments";

    public override async Task RunAsync(ImportContext context, LegacyDbReader source, CancellationToken ct)
    {
        var stats = context.StatsFor(Name);
        var carsById = (await source.CarsAsync(ct)).ToDictionary(c => c.CarId);
        var jobsByAppointment = (await source.JobsAsync(ct))
            .Where(j => j.AppointmentId is not null)
            .ToLookup(j => j.AppointmentId!.Value);

        foreach (var legacy in await source.AppointmentsAsync(ct))
        {
            if (context.Map.Contains(EntityType, legacy.AppointmentId))
            {
                stats.Skipped++;
                continue;
            }

            var appointmentRef = $"Appointment #{legacy.AppointmentId}";

            Guid? carId = null;
            Guid? customerId = null;
            if (legacy.CarId is int legacyCarId)
            {
                carId = context.Map.Get(CarImportPhase.EntityType, legacyCarId);
                customerId = context.Map.Get(CustomerImportPhase.EntityType, carsById[legacyCarId].CustomerId);
            }

            var linkedJob = jobsByAppointment[legacy.AppointmentId].OrderBy(j => j.JobId).FirstOrDefault();
            var jobId = linkedJob is null ? (Guid?)null : context.Map.Get(JobImportPhase.EntityType, linkedJob.JobId);

            var (appointment, statusFlagRaw, endAdjusted) = AppointmentMapper.Map(legacy, customerId, carId, jobId);

            if (statusFlagRaw is not null)
            {
                context.Flag(
                    "appointment-status-mapped",
                    appointmentRef,
                    $"Legacy status '{statusFlagRaw}' has no direct equivalent — imported as '{appointment.Status}'");
            }

            if (endAdjusted)
            {
                context.Flag(
                    "appointment-end-adjusted",
                    appointmentRef,
                    $"Stored end time {legacy.TimeEnd} is not after start {legacy.AppointmentTime} — imported as start + 1 h");
            }

            context.Db.Appointments.Add(appointment);
            await context.Map.AddAsync(EntityType, legacy.AppointmentId, appointment.Id, ct);
            stats.Imported++;
        }
    }
}
