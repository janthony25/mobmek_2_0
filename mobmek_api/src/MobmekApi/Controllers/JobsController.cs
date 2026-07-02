using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class JobsController(IJobService jobService) : ControllerBase
{
    /// <summary>Returns all jobs, optionally filtered by customer via <c>?customerId=</c>.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<JobDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<JobDto>>> GetAll([FromQuery] Guid? customerId, CancellationToken cancellationToken)
    {
        var jobs = await jobService.GetAllAsync(customerId, cancellationToken);
        return Ok(jobs);
    }

    /// <summary>Returns one page of jobs (newest first), optionally filtered by <c>?search=</c>.</summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<JobDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<JobDto>>> GetPaged(
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 15,
        [FromQuery] string? search = null)
    {
        var result = await jobService.GetPagedAsync(page, pageSize, search, cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns a single job by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(JobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var job = await jobService.GetByIdAsync(id, cancellationToken);
        return job is null ? NotFound() : Ok(job);
    }

    /// <summary>Creates a new job for a customer's car.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(JobDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<JobDto>> Create(CreateJobRequest request, CancellationToken cancellationToken)
    {
        var (job, error) = await jobService.CreateAsync(request, cancellationToken);
        if (error != JobWriteError.None)
        {
            return MapError(error);
        }

        return CreatedAtAction(nameof(GetById), new { id = job!.Id }, job);
    }

    /// <summary>Updates an existing job.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(JobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobDto>> Update(Guid id, UpdateJobRequest request, CancellationToken cancellationToken)
    {
        var (job, error) = await jobService.UpdateAsync(id, request, cancellationToken);
        if (error != JobWriteError.None)
        {
            return MapError(error);
        }

        return Ok(job);
    }

    /// <summary>Deletes a job (and its items, labour, service lines and mechanic links).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await jobService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>Assigns a mechanic (employee) to a job.</summary>
    [HttpPost("{id:guid}/mechanics")]
    [ProducesResponseType(typeof(JobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobDto>> AddMechanic(Guid id, AddJobMechanicRequest request, CancellationToken cancellationToken)
    {
        var (job, error) = await jobService.AddMechanicAsync(id, request.EmployeeId, cancellationToken);
        return error != JobWriteError.None ? MapError(error) : Ok(job);
    }

    /// <summary>Removes a mechanic from a job.</summary>
    [HttpDelete("{id:guid}/mechanics/{employeeId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMechanic(Guid id, Guid employeeId, CancellationToken cancellationToken)
    {
        var removed = await jobService.RemoveMechanicAsync(id, employeeId, cancellationToken);
        return removed ? NoContent() : NotFound();
    }

    private ActionResult MapError(JobWriteError error) => error switch
    {
        JobWriteError.NotFound => NotFound(),
        JobWriteError.CustomerNotFound => Problem(detail: "Customer does not exist.", statusCode: StatusCodes.Status400BadRequest),
        JobWriteError.CarNotFound => Problem(detail: "Car does not exist.", statusCode: StatusCodes.Status400BadRequest),
        JobWriteError.CarNotOwnedByCustomer => Problem(detail: "The selected car does not belong to this customer.", statusCode: StatusCodes.Status400BadRequest),
        JobWriteError.EmployeeNotFound => Problem(detail: "Employee does not exist.", statusCode: StatusCodes.Status400BadRequest),
        JobWriteError.MechanicAlreadyAssigned => Problem(detail: "That mechanic is already assigned to this job.", statusCode: StatusCodes.Status400BadRequest),
        _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
    };
}
