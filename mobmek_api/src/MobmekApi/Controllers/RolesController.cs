using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

/// <summary>Admin-only role/permission management — who can hold which role, and what each
/// role can do. Gated the same way as <see cref="AccountsController"/> since managing roles is
/// managing access, the same concern.</summary>
[ApiController]
[Route("api/roles")]
[Produces("application/json")]
[Authorize(Policy = Permissions.ManageAccounts)]
public class RolesController(IRoleService roleService) : ControllerBase
{
    /// <summary>Every role, with its permissions and how many accounts currently hold it.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await roleService.GetAllAsync(cancellationToken));
    }

    /// <summary>The fixed catalog every role's permissions are drawn from, for rendering a
    /// checkbox per entry (including ones no role holds yet).</summary>
    [HttpGet("permissions")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<string>> GetPermissionCatalog()
    {
        return Ok(roleService.GetPermissionCatalog());
    }

    /// <summary>Creates a role with no permissions yet — set them separately.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RoleDto>> Create(CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var (role, error) = await roleService.CreateAsync(request, cancellationToken);
        return error != RoleError.None
            ? MapError(error)
            : CreatedAtAction(nameof(GetAll), role);
    }

    /// <summary>Replaces a role's entire permission set with exactly what's in the request.</summary>
    [HttpPut("{id:guid}/permissions")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoleDto>> SetPermissions(
        Guid id, SetRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        var (role, error) = await roleService.SetPermissionsAsync(id, request, cancellationToken);
        return error != RoleError.None ? MapError(error) : Ok(role);
    }

    /// <summary>Deletes a role — refuses if it's the protected Admin role or still assigned to
    /// any account (reassign them first).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var error = await roleService.DeleteAsync(id, cancellationToken);
        return error switch
        {
            RoleError.None => NoContent(),
            _ => MapError(error),
        };
    }

    private ActionResult MapError(RoleError error) => error switch
    {
        RoleError.NotFound => NotFound(),
        RoleError.DuplicateName => Problem(detail: "A role with that name already exists.", statusCode: StatusCodes.Status400BadRequest),
        RoleError.Protected => Problem(detail: "The Admin role can't be edited or deleted.", statusCode: StatusCodes.Status400BadRequest),
        RoleError.InUse => Problem(detail: "This role is still assigned to one or more accounts — reassign them first.", statusCode: StatusCodes.Status400BadRequest),
        RoleError.UnknownPermission => Problem(detail: "One or more permissions aren't recognised.", statusCode: StatusCodes.Status400BadRequest),
        _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
    };
}
