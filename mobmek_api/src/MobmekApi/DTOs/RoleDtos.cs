using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>
/// One role as shown to an Admin managing access. <c>IsProtected</c> is true only for the
/// hardcoded "Admin" role — it can't be deleted or have its permissions edited, so there's
/// always at least one role that can manage everything, including other roles.
/// </summary>
public record RoleDto(
    Guid Id,
    string Name,
    bool IsProtected,
    int AccountCount,
    IReadOnlyList<string> Permissions);

public record CreateRoleRequest([Required, MaxLength(100)] string Name);

/// <summary>Replaces a role's entire permission set — not a partial add/remove — so the UI's
/// checkbox grid can just submit whatever's currently checked.</summary>
public record SetRolePermissionsRequest(IReadOnlyList<string> Permissions);

public enum RoleError
{
    None,
    NotFound,
    DuplicateName,
    Protected,
    InUse,
    UnknownPermission,
}
