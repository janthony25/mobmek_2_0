using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>A normalized counterparty; picking one pre-fills its default category/GST treatment.</summary>
public record PayeeDto(
    Guid Id,
    string Name,
    Guid? DefaultCategoryId,
    string? DefaultCategoryName,
    string? DefaultGstTreatment,
    string? Notes,
    bool IsArchived,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>Spend history for one payee (12-month window for the totals).</summary>
public record PayeeSummaryDto(
    Guid Id,
    string Name,
    int TransactionCount,
    DateOnly? FirstDate,
    DateOnly? LastDate,
    decimal TotalIn12Months,
    decimal TotalOut12Months);

public record CreatePayeeRequest(
    [Required, MaxLength(200)] string Name,
    Guid? DefaultCategoryId,
    [MaxLength(20)] string? DefaultGstTreatment,
    [MaxLength(2000)] string? Notes);

public record UpdatePayeeRequest(
    [Required, MaxLength(200)] string Name,
    Guid? DefaultCategoryId,
    [MaxLength(20)] string? DefaultGstTreatment,
    [MaxLength(2000)] string? Notes,
    bool IsArchived);

public enum PayeeWriteError
{
    None,
    NotFound,
    DuplicateName,
    CategoryNotFound,
    InvalidGstTreatment,
    InUse,
}
