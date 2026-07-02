using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>
/// A recurring schedule. <see cref="AccountName"/>/<see cref="CategoryName"/> are denormalised
/// for list views. <see cref="NextOccurrenceDate"/> and <see cref="MonthlyEquivalentAmount"/> are
/// computed, not stored.
/// </summary>
public record RecurringTransactionDto(
    Guid Id,
    string Description,
    string Direction,
    decimal Amount,
    Guid CategoryId,
    string CategoryName,
    Guid AccountId,
    string AccountName,
    string? Counterparty,
    string GstTreatment,
    string Frequency,
    int Interval,
    DateOnly AnchorDate,
    DateOnly? EndDate,
    bool AutoPost,
    bool IsPaused,
    DateOnly? NextOccurrenceDate,
    decimal MonthlyEquivalentAmount,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>An occurrence that is due (on or before the as-of date) and hasn't been posted yet.</summary>
public record DueOccurrenceDto(
    Guid RecurringTransactionId,
    string Description,
    string Direction,
    decimal Amount,
    Guid AccountId,
    string AccountName,
    DateOnly Date);

public record CreateRecurringTransactionRequest(
    [Required, MaxLength(500)] string Description,
    [Required, MaxLength(10)] string Direction,
    decimal Amount,
    Guid CategoryId,
    Guid AccountId,
    [MaxLength(200)] string? Counterparty,
    [MaxLength(20)] string? GstTreatment,
    [Required, MaxLength(20)] string Frequency,
    int Interval,
    DateOnly AnchorDate,
    DateOnly? EndDate,
    bool AutoPost);

public record UpdateRecurringTransactionRequest(
    [Required, MaxLength(500)] string Description,
    [Required, MaxLength(10)] string Direction,
    decimal Amount,
    Guid CategoryId,
    Guid AccountId,
    [MaxLength(200)] string? Counterparty,
    [MaxLength(20)] string? GstTreatment,
    [Required, MaxLength(20)] string Frequency,
    int Interval,
    DateOnly AnchorDate,
    DateOnly? EndDate,
    bool AutoPost,
    bool IsPaused);

/// <summary>Why a recurring-transaction write was refused.</summary>
public enum RecurringTransactionWriteError
{
    None,
    NotFound,
    AccountNotFound,
    AccountArchived,
    CategoryNotFound,
    InvalidDirection,
    InvalidGstTreatment,
    InvalidFrequency,
    DirectionMismatchesCategory,
    NonPositiveAmount,
    InvalidInterval,
    OccurrenceAlreadyPosted,
    OccurrenceNotDue,
}
