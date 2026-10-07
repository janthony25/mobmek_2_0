using System.Security.Cryptography;
using System.Text;
using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class AccountService(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    IEmailSender emailSender,
    IEmailSettingsService emailSettingsService) : IAccountService
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

    public async Task<ProfileDto?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.AsNoTracking()
            .Include(u => u.Employee)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        return user?.Employee is null ? null : ToDto(user);
    }

    public async Task<ProfileDto?> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await db.Users
            .Include(u => u.Employee)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user?.Employee is null)
        {
            return null;
        }

        // Deliberately narrow: title/employment type/login email stay Admin-managed
        // (EmployeesController) — self-service only covers name/contact info.
        user.Employee.FirstName = request.FirstName;
        user.Employee.LastName = request.LastName;
        user.Employee.ContactNumber = request.ContactNumber;
        user.Employee.PhysicalAddress = request.PhysicalAddress;
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(user);
    }

    public async Task<AccountError> RequestPasswordChangeCodeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var settings = await emailSettingsService.GetCurrentAsync(cancellationToken);
        if (!settings.ResendConfigured)
        {
            return AccountError.NotConfigured;
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AccountError.NotConfigured;
        }

        var code = await IssueCodeAsync(userId, cancellationToken);

        var message = new OutboundEmailMessage(
            To: user.Email!,
            ToName: null, Cc: null, Bcc: null,
            ReplyTo: settings.ReplyToAddress,
            FromName: settings.FromName, FromAddress: settings.FromAddress,
            Subject: "Your Mobmek password change code",
            Html: $"<p>Your password change code is <strong>{code}</strong>. It expires in 10 minutes. " +
                  "If you didn't request this, you can safely ignore this email.</p>");

        var result = await emailSender.SendAsync(message, cancellationToken);
        return result.Success ? AccountError.None : AccountError.SendFailed;
    }

    public async Task<(AccountError Error, string? ErrorMessage)> ConfirmPasswordChangeAsync(
        Guid userId, ConfirmPasswordChangeRequest request, CancellationToken cancellationToken = default)
    {
        var codeRow = await db.PasswordChangeCodes
            .Where(c => c.UserId == userId && c.ConsumedAtUtc == null)
            .OrderByDescending(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (codeRow is null || !FixedTimeEquals(codeRow.CodeHash, Hash(request.Code)))
        {
            return (AccountError.InvalidCode, null);
        }

        if (codeRow.ExpiresAtUtc < DateTime.UtcNow)
        {
            return (AccountError.CodeExpired, null);
        }

        // Consumed as soon as it's matched — a code can reset a password at most once,
        // whether that attempt succeeds or fails on password policy.
        codeRow.ConsumedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return (AccountError.InvalidCode, null);
        }

        // Identity's own no-current-password-required reset path, same one "forgot password"
        // flows use — not a hand-rolled Remove+Add.
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
        {
            return (AccountError.WeakPassword, string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        return (AccountError.None, null);
    }

    public async Task<AccountError> RequestForgotPasswordCodeAsync(string email, CancellationToken cancellationToken = default)
    {
        var settings = await emailSettingsService.GetCurrentAsync(cancellationToken);
        if (!settings.ResendConfigured)
        {
            return AccountError.NotConfigured;
        }

        var user = await userManager.FindByEmailAsync(email);
        // No code is generated/sent for an unknown email, a deactivated account, or an
        // unconfirmed one (that's what the activation-link flow is for) — but this method
        // always returns None in every one of those cases, same as when a code genuinely goes
        // out. An anonymous caller must not be able to tell them apart by probing this endpoint.
        if (user is null || user.DeactivatedAtUtc is not null || !user.EmailConfirmed)
        {
            return AccountError.None;
        }

        var code = await IssueCodeAsync(user.Id, cancellationToken);
        var message = new OutboundEmailMessage(
            To: user.Email!,
            ToName: null, Cc: null, Bcc: null,
            ReplyTo: settings.ReplyToAddress,
            FromName: settings.FromName, FromAddress: settings.FromAddress,
            Subject: "Reset your Mobmek password",
            Html: $"<p>Your password reset code is <strong>{code}</strong>. It expires in 10 minutes. " +
                  "If you didn't request this, you can safely ignore this email — your password hasn't changed.</p>");

        // A real send failure here is deliberately still reported as None, not SendFailed — the
        // authenticated /account/password flow can afford to be specific since the caller is
        // already proven to own the account, but this endpoint can't let "it failed to send"
        // (only reachable once we already know the account is eligible) become a side channel
        // distinguishable from "no such account" (which never attempts a send at all).
        await emailSender.SendAsync(message, cancellationToken);
        return AccountError.None;
    }

    public async Task<(AccountError Error, string? ErrorMessage)> ResetForgottenPasswordAsync(
        ResetForgottenPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || user.DeactivatedAtUtc is not null)
        {
            // Same generic error as a wrong/expired code — an anonymous caller must not be able
            // to tell "no such account" apart from "wrong code" by probing this endpoint.
            return (AccountError.InvalidCode, null);
        }

        var codeRow = await db.PasswordChangeCodes
            .Where(c => c.UserId == user.Id && c.ConsumedAtUtc == null)
            .OrderByDescending(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (codeRow is null || !FixedTimeEquals(codeRow.CodeHash, Hash(request.Code)))
        {
            return (AccountError.InvalidCode, null);
        }

        if (codeRow.ExpiresAtUtc < DateTime.UtcNow)
        {
            return (AccountError.CodeExpired, null);
        }

        codeRow.ConsumedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
        {
            return (AccountError.WeakPassword, string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        return (AccountError.None, null);
    }

    /// <summary>Supersedes any still-pending code for this user and issues a fresh one. Shared by
    /// the authenticated (<see cref="RequestPasswordChangeCodeAsync"/>) and unauthenticated
    /// (<see cref="RequestForgotPasswordCodeAsync"/>) request-code flows — same table, same
    /// 10-minute lifetime, same "only the newest code is valid" rule either way.</summary>
    private async Task<string> IssueCodeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var pending = await db.PasswordChangeCodes
            .Where(c => c.UserId == userId && c.ConsumedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var old in pending)
        {
            old.ConsumedAtUtc = DateTime.UtcNow;
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        db.PasswordChangeCodes.Add(new PasswordChangeCode
        {
            UserId = userId,
            CodeHash = Hash(code),
            ExpiresAtUtc = DateTime.UtcNow.Add(CodeLifetime),
        });
        await db.SaveChangesAsync(cancellationToken);
        return code;
    }

    private static ProfileDto ToDto(ApplicationUser user) => new(
        user.EmployeeId, user.Employee!.FirstName, user.Employee.LastName,
        user.Employee.ContactNumber, user.Employee.PhysicalAddress, user.Email!);

    private static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    private static bool FixedTimeEquals(string storedHex, string candidateHex)
    {
        if (storedHex.Length != candidateHex.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(storedHex), Convert.FromHexString(candidateHex));
    }
}
