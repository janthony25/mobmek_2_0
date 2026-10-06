using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class EmployeesController(IEmployeeService employeeService) : ControllerBase
{
    /// <summary>Returns all employees, including contact/address details — gated separately
    /// from <see cref="GetSummaries"/> since those are genuinely private, not just internal.</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.ManageEmployees)]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EmployeeDto>>> GetAll(CancellationToken cancellationToken)
    {
        var employees = await employeeService.GetAllAsync(cancellationToken);
        return Ok(employees);
    }

    /// <summary>Name-only list for pickers (job mechanics, appointment assignment) — any signed-in
    /// staff member needs this for core workflows, so it isn't behind ManageEmployees.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EmployeeSummaryDto>>> GetSummaries(CancellationToken cancellationToken)
    {
        return Ok(await employeeService.GetSummariesAsync(cancellationToken));
    }

    /// <summary>Returns a single employee by id.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.ManageEmployees)]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var employee = await employeeService.GetByIdAsync(id, cancellationToken);
        return employee is null ? NotFound() : Ok(employee);
    }

    /// <summary>Creates a new employee. The referenced title and employment type must exist.</summary>
    [HttpPost]
    [Authorize(Policy = Permissions.ManageEmployees)]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeeDto>> Create(CreateEmployeeRequest request, CancellationToken cancellationToken)
    {
        var (employee, error) = await employeeService.CreateAsync(request, cancellationToken);
        if (error != EmployeeWriteError.None)
        {
            return MapError(error, request.TitleId, request.EmploymentTypeId);
        }

        return CreatedAtAction(nameof(GetById), new { id = employee!.Id }, employee);
    }

    /// <summary>Updates an existing employee.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.ManageEmployees)]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDto>> Update(Guid id, UpdateEmployeeRequest request, CancellationToken cancellationToken)
    {
        var (employee, error) = await employeeService.UpdateAsync(id, request, cancellationToken);
        if (error != EmployeeWriteError.None)
        {
            return MapError(error, request.TitleId, request.EmploymentTypeId);
        }

        return Ok(employee);
    }

    /// <summary>Deletes an employee.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.ManageEmployees)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await employeeService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private ActionResult MapError(EmployeeWriteError error, Guid titleId, Guid employmentTypeId) => error switch
    {
        EmployeeWriteError.NotFound => NotFound(),
        EmployeeWriteError.TitleNotFound => Problem(
            detail: $"Employee title '{titleId}' does not exist.", statusCode: StatusCodes.Status400BadRequest),
        EmployeeWriteError.EmploymentTypeNotFound => Problem(
            detail: $"Employment type '{employmentTypeId}' does not exist.", statusCode: StatusCodes.Status400BadRequest),
        _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
    };
}
