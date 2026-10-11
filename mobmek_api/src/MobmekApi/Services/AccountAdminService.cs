using System.Net;
using System.Security.Cryptography;
using System.Text;
using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class AccountAdminService(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IEmailSender emailSender,
    IEmailSettingsService emailSettingsService,
    IConfiguration configuration) : IAccountAdminService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(10);
    private const int DeactivationGracePeriodDays = 30;

    public async Task<IReadOnlyList<AccountListItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var users = await db.Users
            .AsNoTracking()
            .Include(u => u.Employee)
            .Where(u => u.Employee != null)
            .OrderBy(u => u.Employee!.LastName)
            .ThenBy(u => u.Employee!.FirstName)
            .ToListAsync(cancellationToken);

        var result = new List<AccountListItemDto>(users.Count);
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            result.Add(new AccountListItemDto(
                user.Id, user.EmployeeId, user.Employee!.FirstName, user.Employee.LastName,
                user.Email!, roles.ToArray(), user.EmailConfirmed, user.DeactivatedAtUtc));
        }

        return result;
    }

    public async Task<(AccountListItemDto? Account, AccountAdminError Error)> CreateAsync(
        CreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await emailSettingsService.GetCurrentAsync(cancellationToken);
        if (!settings.ResendConfigured)
        {
            return (null, AccountAdminError.NotConfigured);
        }

        if (!await roleManager.RoleExistsAsync(request.Role))
        {
            return (null, AccountAdminError.InvalidRole);
        }

        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId, cancellationToken);
        if (employee is null)
        {
            return (null, AccountAdminError.EmployeeNotFound);
        }

        if (await db.Users.AnyAsync(u => u.EmployeeId == request.EmployeeId, cancellationToken))
        {
            return (null, AccountAdminError.EmployeeAlreadyHasAccount);
        }

        if (await userManager.FindByEmailAsync(request.Email) is not null)
        {
            return (null, AccountAdminError.EmailInUse);
        }

        // Accounts start locked out of signing in (EmailConfirmed = false) unless the Admin opts
        // into ActivateImmediately — the new hire otherwise uses the emailed link to confirm and
        // (if no Password was given here) set their own password, which a random, never-surfaced
        // password is immediately superseded by at confirmation time.
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = request.ActivateImmediately,
            EmployeeId = request.EmployeeId,
        };

        var createResult = await userManager.CreateAsync(user, request.Password ?? GenerateRandomPassword());
        if (!createResult.Succeeded)
        {
            // Duplicate email/username is already ruled out above, so the only way UserManager
            // itself can still fail here is an Admin-supplied Password that fails the policy.
            var isPasswordError = createResult.Errors.Any(e => e.Code.StartsWith("Password", StringComparison.Ordinal));
            return (null, isPasswordError ? AccountAdminError.WeakPassword : AccountAdminError.EmailInUse);
        }

        await userManager.AddToRoleAsync(user, request.Role);

        string? confirmUrl = null;
        if (!request.ActivateImmediately)
        {
            var token = await IssueConfirmationTokenAsync(user.Id, cancellationToken);
            confirmUrl = $"{FrontendBaseUrl()}/confirm-account?token={token}";
        }

        var message = BuildAccountEmail(user, employee, settings, confirmUrl, $"{FrontendBaseUrl()}/forgot-password", request.Password);

        var sendResult = await emailSender.SendAsync(message, cancellationToken);
        if (!sendResult.Success)
        {
            // Without this, a failed send would leave a half-created, permanently-unconfirmable
            // account occupying the employee's one-account slot — Admin couldn't retry at all.
            await userManager.DeleteAsync(user);
            return (null, AccountAdminError.SendFailed);
        }

        return ((await GetAllAsync(cancellationToken)).First(a => a.UserId == user.Id), AccountAdminError.None);
    }

    public async Task<(AccountListItemDto? Account, AccountAdminError Error)> UpdateRoleAsync(
        Guid userId, UpdateAccountRoleRequest request, Guid actingUserId, CancellationToken cancellationToken = default)
    {
        if (userId == actingUserId)
        {
            return (null, AccountAdminError.CannotEditOwnRole);
        }

        if (!await roleManager.RoleExistsAsync(request.Role))
        {
            return (null, AccountAdminError.InvalidRole);
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return (null, AccountAdminError.UserNotFound);
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        if (currentRoles.Contains("Admin") && request.Role != "Admin")
        {
            // Never let the last Admin demote themselves (or another admin) into a state where
            // nobody can manage roles/settings anymore.
            var adminCount = (await userManager.GetUsersInRoleAsync("Admin")).Count;
            if (adminCount <= 1)
            {
                return (null, AccountAdminError.LastAdmin);
            }
        }

        if (currentRoles.Count > 0)
        {
            await userManager.RemoveFromRolesAsync(user, currentRoles);
        }

        await userManager.AddToRoleAsync(user, request.Role);

        return ((await GetAllAsync(cancellationToken)).First(a => a.UserId == user.Id), AccountAdminError.None);
    }

    public async Task<(AccountListItemDto? Account, AccountAdminError Error)> DeactivateAsync(
        Guid userId, Guid actingUserId, CancellationToken cancellationToken = default)
    {
        if (userId == actingUserId)
        {
            return (null, AccountAdminError.CannotDeactivateSelf);
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return (null, AccountAdminError.UserNotFound);
        }

        var roles = await userManager.GetRolesAsync(user);
        if (roles.Contains("Admin"))
        {
            // Same protection as UpdateRoleAsync's last-admin guard — deactivating is just
            // another way to remove an Admin's access, so it needs the same safety net.
            var adminCount = (await userManager.GetUsersInRoleAsync("Admin")).Count;
            if (adminCount <= 1)
            {
                return (null, AccountAdminError.LastAdmin);
            }
        }

        // LockoutEnabled defaults to true for every account created via UserManager.CreateAsync
        // (Options.Lockout.AllowedForNewUsers), but set it explicitly so a future account created
        // any other way can still be deactivated.
        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        user.DeactivatedAtUtc = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        return ((await GetAllAsync(cancellationToken)).First(a => a.UserId == user.Id), AccountAdminError.None);
    }

    public async Task<(AccountListItemDto? Account, AccountAdminError Error)> ReactivateAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return (null, AccountAdminError.UserNotFound);
        }

        await userManager.SetLockoutEndDateAsync(user, null);
        user.DeactivatedAtUtc = null;
        await userManager.UpdateAsync(user);

        return ((await GetAllAsync(cancellationToken)).First(a => a.UserId == user.Id), AccountAdminError.None);
    }

    public async Task<int> PurgeExpiredDeactivatedAccountsAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-DeactivationGracePeriodDays);
        var expired = await db.Users
            .Where(u => u.DeactivatedAtUtc != null && u.DeactivatedAtUtc <= cutoff)
            .ToListAsync(cancellationToken);

        var purged = 0;
        foreach (var user in expired)
        {
            // Neither table has a real FK to AspNetUsers (plain Guid + index), so deleting the
            // user wouldn't fail without this — but it would leave these rows orphaned forever.
            var confirmationCodes = await db.EmailConfirmationCodes.Where(c => c.UserId == user.Id).ToListAsync(cancellationToken);
            db.EmailConfirmationCodes.RemoveRange(confirmationCodes);
            var passwordCodes = await db.PasswordChangeCodes.Where(c => c.UserId == user.Id).ToListAsync(cancellationToken);
            db.PasswordChangeCodes.RemoveRange(passwordCodes);
            await db.SaveChangesAsync(cancellationToken);

            var result = await userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                purged++;
            }
        }

        return purged;
    }

    public async Task<(AccountInvitePreviewDto? Preview, AccountAdminError Error)> GetInvitePreviewAsync(
        string token, CancellationToken cancellationToken = default)
    {
        var (codeRow, error) = await FindValidTokenRowAsync(token, cancellationToken);
        if (codeRow is null)
        {
            return (null, error);
        }

        var user = await db.Users.AsNoTracking()
            .Include(u => u.Employee)
            .FirstOrDefaultAsync(u => u.Id == codeRow.UserId, cancellationToken);
        if (user?.Employee is null)
        {
            return (null, AccountAdminError.InvalidToken);
        }

        return (new AccountInvitePreviewDto(user.Email!, user.Employee.FirstName, user.Employee.LastName), AccountAdminError.None);
    }

    public async Task<AccountAdminError> ConfirmAccountAsync(
        ConfirmAccountRequest request, CancellationToken cancellationToken = default)
    {
        var (codeRow, error) = await FindValidTokenRowAsync(request.Token, cancellationToken);
        if (codeRow is null)
        {
            return error;
        }

        var user = await userManager.FindByIdAsync(codeRow.UserId.ToString());
        if (user is null)
        {
            return AccountAdminError.InvalidToken;
        }

        // Consumed as soon as it's matched — a link can activate an account at most once,
        // whether the password it's paired with passes policy or not.
        codeRow.ConsumedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);
        if (!resetResult.Succeeded)
        {
            return AccountAdminError.WeakPassword;
        }

        user.EmailConfirmed = true;
        await userManager.UpdateAsync(user);

        return AccountAdminError.None;
    }

    public async Task<AccountAdminError> ResendConfirmationAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var settings = await emailSettingsService.GetCurrentAsync(cancellationToken);
        if (!settings.ResendConfigured)
        {
            return AccountAdminError.NotConfigured;
        }

        var user = await db.Users.AsNoTracking()
            .Include(u => u.Employee)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user?.Employee is null)
        {
            return AccountAdminError.UserNotFound;
        }

        if (user.EmailConfirmed)
        {
            return AccountAdminError.AlreadyConfirmed;
        }

        var token = await IssueConfirmationTokenAsync(user.Id, cancellationToken);
        var confirmUrl = $"{FrontendBaseUrl()}/confirm-account?token={token}";

        // Whatever password (random or Admin-chosen) the account was created with only ever
        // existed as a hash — there's nothing to re-show here, so a resend always gets the
        // generic "click to set your password" copy, never the one-time username/password
        // reminder CreateAsync sends on the very first invite.
        var message = BuildAccountEmail(user, user.Employee, settings, confirmUrl, $"{FrontendBaseUrl()}/forgot-password", password: null);

        var sendResult = await emailSender.SendAsync(message, cancellationToken);
        return sendResult.Success ? AccountAdminError.None : AccountAdminError.SendFailed;
    }

    private string FrontendBaseUrl() => (configuration["Frontend:BaseUrl"] ?? "http://localhost:3000").TrimEnd('/');

    // `confirmUrl` is null exactly when the account was already activated at creation
    // (ActivateImmediately) — there's nothing left to confirm, so the email points at the
    // forgot-password flow instead. `password` is the Admin-chosen plaintext password, only
    // ever known here immediately after CreateAsync hashes it — never persisted or re-derivable,
    // which is why ResendConfirmationAsync always passes null.
    private static OutboundEmailMessage BuildAccountEmail(
        ApplicationUser user, Employee employee, EmailSettingsDto settings,
        string? confirmUrl, string forgotPasswordUrl, string? password)
    {
        var toName = $"{employee.FirstName} {employee.LastName}";
        var encodedPassword = password is null ? null : WebUtility.HtmlEncode(password);

        var (subject, html) = confirmUrl is not null
            ? ("Your Mobmek account is ready to activate", encodedPassword is null
                ? $"<p>An account has been created for you. " +
                  $"<a href=\"{confirmUrl}\">Click here to set your password</a> and activate your account. " +
                  "This link expires in 10 minutes.</p>"
                : $"<p>An account has been created for you.</p>" +
                  $"<p>Username: {user.Email}<br/>Password: {encodedPassword}</p>" +
                  $"<p><a href=\"{confirmUrl}\">Click here to activate your account</a> — you can sign in with the " +
                  "password above once it's active, or set your own there instead. This link expires in 10 minutes.</p>")
            : ("Your Mobmek account is ready", encodedPassword is null
                ? $"<p>Your Mobmek account has been activated.</p>" +
                  $"<p>Username: {user.Email}</p>" +
                  $"<p><a href=\"{forgotPasswordUrl}\">Click here to set your password</a>.</p>"
                : $"<p>Your Mobmek account has been activated.</p>" +
                  $"<p>Username: {user.Email}<br/>Password: {encodedPassword}</p>" +
                  $"<p>You can change this anytime — <a href=\"{forgotPasswordUrl}\">reset your password here</a>.</p>");

        return new OutboundEmailMessage(
            To: user.Email!, ToName: toName, Cc: null, Bcc: null,
            ReplyTo: settings.ReplyToAddress, FromName: settings.FromName, FromAddress: settings.FromAddress,
            Subject: subject, Html: html);
    }

    // The token itself is the lookup key (hashed) — unlike a short user-typed code, it's long
    // and unguessable enough that a direct hash-equality query is the standard, safe way to
    // resolve it, with no separate user/email anchor needed.
    private async Task<(EmailConfirmationCode? Row, AccountAdminError Error)> FindValidTokenRowAsync(
        string token, CancellationToken cancellationToken)
    {
        var tokenHash = Hash(token);
        var codeRow = await db.EmailConfirmationCodes
            .Where(c => c.CodeHash == tokenHash && c.ConsumedAtUtc == null)
            .FirstOrDefaultAsync(cancellationToken);

        if (codeRow is null)
        {
            return (null, AccountAdminError.InvalidToken);
        }

        if (codeRow.ExpiresAtUtc < DateTime.UtcNow)
        {
            return (null, AccountAdminError.TokenExpired);
        }

        return (codeRow, AccountAdminError.None);
    }

    private async Task<string> IssueConfirmationTokenAsync(Guid userId, CancellationToken cancellationToken)
    {
        var pending = await db.EmailConfirmationCodes
            .Where(c => c.UserId == userId && c.ConsumedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var old in pending)
        {
            old.ConsumedAtUtc = DateTime.UtcNow;
        }

        // 32 random bytes, hex-encoded (64 chars) — long/unguessable enough to be its own lookup
        // key, and URL-safe with no escaping needed, unlike a short user-typed code.
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        db.EmailConfirmationCodes.Add(new EmailConfirmationCode
        {
            UserId = userId,
            CodeHash = Hash(token),
            ExpiresAtUtc = DateTime.UtcNow.Add(TokenLifetime),
        });
        await db.SaveChangesAsync(cancellationToken);

        return token;
    }

    private static string GenerateRandomPassword() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + "Aa1!";

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
