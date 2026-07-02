using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>An auto-categorization rule; lowest priority wins when several match.</summary>
public record CategorizationRuleDto(
    Guid Id,
    string Name,
    int Priority,
    string MatchField,
    string MatchType,
    string MatchValue,
    string? Direction,
    decimal? AmountMin,
    decimal? AmountMax,
    Guid SetCategoryId,
    string SetCategoryName,
    string? SetGstTreatment,
    Guid? SetPayeeId,
    string? SetPayeeName,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record CreateCategorizationRuleRequest(
    [Required, MaxLength(200)] string Name,
    int Priority,
    [Required, MaxLength(20)] string MatchField,
    [Required, MaxLength(20)] string MatchType,
    [Required, MaxLength(200)] string MatchValue,
    [MaxLength(10)] string? Direction,
    decimal? AmountMin,
    decimal? AmountMax,
    Guid SetCategoryId,
    [MaxLength(20)] string? SetGstTreatment,
    Guid? SetPayeeId,
    bool IsActive = true);

public record UpdateCategorizationRuleRequest(
    [Required, MaxLength(200)] string Name,
    int Priority,
    [Required, MaxLength(20)] string MatchField,
    [Required, MaxLength(20)] string MatchType,
    [Required, MaxLength(200)] string MatchValue,
    [MaxLength(10)] string? Direction,
    decimal? AmountMin,
    decimal? AmountMax,
    Guid SetCategoryId,
    [MaxLength(20)] string? SetGstTreatment,
    Guid? SetPayeeId,
    bool IsActive);

/// <summary>What the entry form knows while the user is typing; any field may be omitted.</summary>
public record RuleSuggestionRequest(
    string? Description,
    string? Counterparty,
    string? Direction,
    decimal? Amount);

/// <summary>The winning rule's outcome, offered as a pre-fill (never applied silently).</summary>
public record RuleSuggestionDto(
    Guid RuleId,
    string RuleName,
    Guid CategoryId,
    string CategoryName,
    string? GstTreatment,
    Guid? PayeeId,
    string? PayeeName);

/// <summary>
/// Result of applying a rule to existing history. Preview mode reports
/// <see cref="MatchCount"/> without changing anything; commit mode also reports how many
/// rows were actually rewritten (managed/reconciled/locked rows are excluded up front).
/// </summary>
public record ApplyRuleResultDto(int MatchCount, int UpdatedCount);

public enum CategorizationRuleWriteError
{
    None,
    NotFound,
    CategoryNotFound,
    PayeeNotFound,
    InvalidMatchField,
    InvalidMatchType,
    InvalidDirection,
    InvalidGstTreatment,
    InvalidAmountBand,
}
