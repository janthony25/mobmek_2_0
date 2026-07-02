using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

public record PlannedTransactionDto(
    Guid Id,
    string Description,
    string Direction,
    decimal Amount,
    DateOnly ExpectedDate,
    Guid CategoryId,
    string CategoryName,
    Guid? AccountId,
    string? AccountName,
    string Status,
    string? ScenarioTag,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record CreatePlannedTransactionRequest(
    [Required, MaxLength(500)] string Description,
    [Required, MaxLength(10)] string Direction,
    decimal Amount,
    DateOnly ExpectedDate,
    Guid CategoryId,
    Guid? AccountId,
    [MaxLength(20)] string? ScenarioTag);

public record UpdatePlannedTransactionRequest(
    [Required, MaxLength(500)] string Description,
    [Required, MaxLength(10)] string Direction,
    decimal Amount,
    DateOnly ExpectedDate,
    Guid CategoryId,
    Guid? AccountId,
    [MaxLength(20)] string? ScenarioTag,
    [Required, MaxLength(20)] string Status);

/// <summary>Why a planned-transaction write was refused.</summary>
public enum PlannedTransactionWriteError
{
    None,
    NotFound,
    AccountNotFound,
    AccountArchived,
    CategoryNotFound,
    InvalidDirection,
    InvalidScenarioTag,
    InvalidStatus,
    NonPositiveAmount,
    DirectionMismatchesCategory,
    NotEditableOnceTerminal,
}
