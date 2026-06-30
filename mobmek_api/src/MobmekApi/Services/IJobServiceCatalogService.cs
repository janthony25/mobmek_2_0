using MobmekApi.DTOs;

namespace MobmekApi.Services;

/// <summary>CRUD for the catalog of sellable services (<see cref="Entities.JobService"/>).</summary>
public interface IJobServiceCatalogService
{
    Task<IReadOnlyList<JobServiceDto>> GetAllAsync(bool? activeOnly = null, CancellationToken cancellationToken = default);

    Task<JobServiceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<JobServiceDto> CreateAsync(CreateJobServiceRequest request, CancellationToken cancellationToken = default);

    Task<JobServiceDto?> UpdateAsync(Guid id, UpdateJobServiceRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
