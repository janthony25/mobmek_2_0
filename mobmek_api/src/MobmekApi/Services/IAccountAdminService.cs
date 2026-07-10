using MobmekApi.DTOs;

namespace MobmekApi.Services;

/// <summary>
/// Admin-only account/role management: listing every login account, creating a new one for an
/// existing Employee (emailing an invite link so it starts inactive), and changing an existing
/// account's role. Previewing and confirming the emailed link is the one piece a brand-new
/// account does for itself, before it can sign in at all — so it lives here too, even though it
/// isn't Admin-gated.
/// </summary>
public interface IAccountAdminService
{
    Task<IReadOnlyList<AccountListItemDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<(AccountListItemDto? Account, AccountAdminError Error)> CreateAsync(
        CreateAccountRequest request, CancellationToken cancellationToken = default);

    /// <summary>Fails with <see cref="AccountAdminError.CannotEditOwnRole"/> when <paramref name="userId"/>
    /// equals <paramref name="actingUserId"/> — an Admin must have another Admin change their role,
    /// never their own, so nobody can escalate/demote themselves without a second person involved.</summary>
    Task<(AccountListItemDto? Account, AccountAdminError Error)> UpdateRoleAsync(
        Guid userId, UpdateAccountRoleRequest request, Guid actingUserId, CancellationToken cancellationToken = default);

    /// <summary>Looks up the invite by its (unhashed) token, so the confirm page can show whose
    /// account it's activating before asking for a password.</summary>
    Task<(AccountInvitePreviewDto? Preview, AccountAdminError Error)> GetInvitePreviewAsync(
        string token, CancellationToken cancellationToken = default);

    Task<AccountAdminError> ConfirmAccountAsync(
        ConfirmAccountRequest request, CancellationToken cancellationToken = default);

    /// <summary>Blocks sign-in immediately (via Identity's own lockout) and starts the 30-day
    /// countdown to <see cref="AccountPurgeJob"/> hard-deleting the account. Fails with
    /// <see cref="AccountAdminError.CannotDeactivateSelf"/> for your own account, or
    /// <see cref="AccountAdminError.LastAdmin"/> if it's the only remaining Admin.</summary>
    Task<(AccountListItemDto? Account, AccountAdminError Error)> DeactivateAsync(
        Guid userId, Guid actingUserId, CancellationToken cancellationToken = default);

    /// <summary>Clears the deactivation and lifts the sign-in block. No-op restrictions beyond
    /// the account needing to exist — reactivating your own account isn't a self-lockout risk.</summary>
    Task<(AccountListItemDto? Account, AccountAdminError Error)> ReactivateAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Hard-deletes every account deactivated 30+ days ago, called by
    /// <see cref="AccountPurgeJob"/>. Returns the number of accounts purged.</summary>
    Task<int> PurgeExpiredDeactivatedAccountsAsync(CancellationToken cancellationToken = default);
}
