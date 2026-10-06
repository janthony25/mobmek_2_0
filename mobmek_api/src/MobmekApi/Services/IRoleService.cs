using MobmekApi.DTOs;

namespace MobmekApi.Services;

/// <summary>
/// Admin-only management of which roles exist and which permissions (see <see cref="Permissions"/>)
/// each one grants. Roles themselves are Identity's own <c>AspNetRoles</c> — this service only adds
/// the permission-grant layer (<see cref="Entities.RolePermission"/>) and the safety rules around
/// editing/deleting a role.
/// </summary>
public interface IRoleService
{
    Task<IReadOnlyList<RoleDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>The fixed, code-defined list every role's permissions are drawn from — for the
    /// UI to render one checkbox per entry, including ones no role holds yet.</summary>
    IReadOnlyList<string> GetPermissionCatalog();

    Task<(RoleDto? Role, RoleError Error)> CreateAsync(
        CreateRoleRequest request, CancellationToken cancellationToken = default);

    /// <summary>Fails with <see cref="RoleError.Protected"/> for the "Admin" role, and
    /// <see cref="RoleError.UnknownPermission"/> if the request names anything outside
    /// <see cref="GetPermissionCatalog"/>.</summary>
    Task<(RoleDto? Role, RoleError Error)> SetPermissionsAsync(
        Guid roleId, SetRolePermissionsRequest request, CancellationToken cancellationToken = default);

    /// <summary>Fails with <see cref="RoleError.Protected"/> for the "Admin" role, or
    /// <see cref="RoleError.InUse"/> if any account still holds this role — reassign them first.</summary>
    Task<RoleError> DeleteAsync(Guid roleId, CancellationToken cancellationToken = default);
}
