using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class LoginAttemptService(AppDbContext db) : ILoginAttemptService
{
    public async Task RecordAsync(
        string email, Guid? employeeId, bool succeeded, string? failureReason, string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        db.LoginAttempts.Add(new LoginAttempt
        {
            Email = email,
            EmployeeId = employeeId,
            Succeeded = succeeded,
            FailureReason = failureReason,
            IpAddress = ipAddress,
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<LoginAttemptPageDto> GetPagedAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.LoginAttempts.AsNoTracking().Include(a => a.Employee);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.CreatedAtUtc).ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new LoginAttemptDto(
                a.Id, a.Email, a.EmployeeId,
                a.Employee == null ? null : a.Employee.FirstName + " " + a.Employee.LastName,
                a.Succeeded, a.FailureReason, a.IpAddress, a.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new LoginAttemptPageDto(items, page, pageSize, totalCount);
    }
}
