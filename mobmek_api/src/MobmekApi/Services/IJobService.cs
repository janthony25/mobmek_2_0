using MobmekApi.DTOs;
using MobmekApi.Entities;

namespace MobmekApi.Services;

/// <summary>Outcome of a job write that depends on referenced records.</summary>
public enum JobWriteError
{
    None,
    NotFound,
    CustomerNotFound,
    CarNotFound,
    CarNotOwnedByCustomer,
    EmployeeNotFound,
    MechanicAlreadyAssigned,
}

public interface IJobService
{
    Task<IReadOnlyList<JobDto>> GetAllAsync(Guid? customerId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one page of jobs. <paramref name="search"/> matches the title, customer name, car
    /// make/model or rego, case-insensitively. <paramref name="sortBy"/> is "newest" (default) or
    /// "oldest". <paramref name="status"/> filters to a single <see cref="JobStatus"/>.
    /// <paramref name="dateFrom"/>/<paramref name="dateTo"/> filter by the job's created date (inclusive).
    /// </summary>
    Task<PagedResult<JobDto>> GetPagedAsync(
        int page, int pageSize, string? search,
        string? sortBy = null, JobStatus? status = null, DateOnly? dateFrom = null, DateOnly? dateTo = null,
        CancellationToken cancellationToken = default);

    Task<JobDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(JobDto? Job, JobWriteError Error)> CreateAsync(CreateJobRequest request, CancellationToken cancellationToken = default);

    Task<(JobDto? Job, JobWriteError Error)> UpdateAsync(Guid id, UpdateJobRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(JobDto? Job, JobWriteError Error)> AddMechanicAsync(Guid jobId, Guid employeeId, CancellationToken cancellationToken = default);

    Task<bool> RemoveMechanicAsync(Guid jobId, Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recomputes and stores <c>TotalJobPrice</c> / <c>TotalJobProfit</c> from the job's
    /// items, labour and service lines. Called by the child services after they mutate.
    /// </summary>
    Task RecalculateTotalsAsync(Guid jobId, CancellationToken cancellationToken = default);
}
