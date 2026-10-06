namespace MobmekApi.Services;

/// <summary>
/// The fixed catalog of permissions a role can be granted (see <see cref="Entities.RolePermission"/>).
/// This list is deliberately not admin-editable — each entry corresponds to a real enforcement
/// point in code (an <c>[Authorize(Policy = ...)]</c> attribute or a claim check), so adding one
/// requires a code change. Which roles hold which of these is fully admin-editable, which is the
/// part meant to be customizable without a deploy.
/// </summary>
public static class Permissions
{
    public const string ManageEmployees = "ManageEmployees";
    public const string ManageAccounts = "ManageAccounts";
    public const string ManageBusinessSettings = "ManageBusinessSettings";
    public const string ManageCalendarSync = "ManageCalendarSync";
    public const string ManageReminderTemplates = "ManageReminderTemplates";
    public const string AccessCashFlow = "AccessCashFlow";

    /// <summary>Not an [Authorize] policy — checked directly by <see cref="Controllers.JobRoleRedaction"/>
    /// to decide whether cost/margin fields are included in the response.</summary>
    public const string ViewJobMargins = "ViewJobMargins";

    public static readonly IReadOnlyList<string> All =
    [
        ManageEmployees,
        ManageAccounts,
        ManageBusinessSettings,
        ManageCalendarSync,
        ManageReminderTemplates,
        AccessCashFlow,
        ViewJobMargins,
    ];
}
