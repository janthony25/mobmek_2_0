using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>
/// A cash account with its derived balance. <see cref="CurrentBalance"/> is computed on read
/// (opening balance + inflows − outflows) and never stored.
/// </summary>
public record CashAccountDto(
    Guid Id,
    string Name,
    string Type,
    string? AccountNumber,
    decimal OpeningBalance,
    DateOnly OpeningDate,
    bool IsArchived,
    decimal CurrentBalance,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record CreateCashAccountRequest(
    [Required, MaxLength(200)] string Name,
    [Required, MaxLength(30)] string Type,
    [MaxLength(50)] string? AccountNumber,
    decimal OpeningBalance,
    DateOnly OpeningDate);

public record UpdateCashAccountRequest(
    [Required, MaxLength(200)] string Name,
    [Required, MaxLength(30)] string Type,
    [MaxLength(50)] string? AccountNumber,
    decimal OpeningBalance,
    DateOnly OpeningDate,
    bool IsArchived);
