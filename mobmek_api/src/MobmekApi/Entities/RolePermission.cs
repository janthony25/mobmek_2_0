namespace MobmekApi.Entities;

/// <summary>
/// Grants one permission (see <see cref="Services.Permissions"/>) to an Identity role. A role
/// can hold many permissions and the same permission can be granted to many roles — composite
/// key on (RoleId, Permission) so the same grant can't be stored twice. No navigation property
/// back to the role: roles are Identity's own <c>AspNetRoles</c>, owned by Identity's model
/// configuration, not ours.
/// </summary>
public class RolePermission
{
    public Guid RoleId { get; set; }

    public required string Permission { get; set; }
}
