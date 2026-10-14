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

    /// <summary>Not an [Authorize] policy — <c>POST /api/customers/with-car</c> stays open to any
    /// authenticated user so staff's existing "+Add Customer" flow is unaffected. This entry is
    /// enforced client-side only: the frontend sends an account whose entire permission set is
    /// exactly this one permission to a standalone, nav-free "add customer + car" page instead of
    /// the normal app shell — see <c>KioskRedirect.tsx</c> in the frontend. Meant for a dedicated
    /// tablet/kiosk account so a customer can fill in their own details.</summary>
    public const string AddCustomerAndCar = "AddCustomerAndCar";

    public static readonly IReadOnlyList<string> All =
    [
        ManageEmployees,
        ManageAccounts,
        ManageBusinessSettings,
        ManageCalendarSync,
        ManageReminderTemplates,
        AccessCashFlow,
        ViewJobMargins,
        AddCustomerAndCar,
    ];
}
