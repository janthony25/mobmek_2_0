using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

public record TransactionCategoryDto(
    Guid Id,
    string Name,
    string Direction,
    string Group,
    bool IsSystem,
    string DefaultGstTreatment,
    bool ExcludeFromOperatingExpense,
    bool IsArchived,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record CreateTransactionCategoryRequest(
    [Required, MaxLength(100)] string Name,
    [Required, MaxLength(10)] string Direction,
    [Required, MaxLength(50)] string Group,
    [MaxLength(20)] string? DefaultGstTreatment,
    bool ExcludeFromOperatingExpense);

/// <summary>
/// On a system category only <see cref="Name"/> and <see cref="IsArchived"/> are applied —
/// its direction, group and flags are fixed because auto-posting and reports rely on them.
/// </summary>
public record UpdateTransactionCategoryRequest(
    [Required, MaxLength(100)] string Name,
    [Required, MaxLength(10)] string Direction,
    [Required, MaxLength(50)] string Group,
    [MaxLength(20)] string? DefaultGstTreatment,
    bool ExcludeFromOperatingExpense,
    bool IsArchived);
