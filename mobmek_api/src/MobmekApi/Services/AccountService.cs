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

        // Only the newest code is ever valid — supersede anything still pending.
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
