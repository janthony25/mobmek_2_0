using MobmekApi.DTOs;

namespace MobmekApi.Services;

public interface ICategorizationRuleService
{
    /// <summary>All rules in evaluation order (priority, then name).</summary>
    Task<IReadOnlyList<CategorizationRuleDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<CategorizationRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(CategorizationRuleDto? Rule, CategorizationRuleWriteError Error)> CreateAsync(CreateCategorizationRuleRequest request, CancellationToken cancellationToken = default);

    Task<(CategorizationRuleDto? Rule, CategorizationRuleWriteError Error)> UpdateAsync(Guid id, UpdateCategorizationRuleRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The first active rule (lowest priority) matching what's known so far, or <c>null</c>.</summary>
    Task<RuleSuggestionDto?> SuggestAsync(RuleSuggestionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies one rule to existing unmanaged history (no invoice/transfer rows, nothing
    /// reconciled or period-locked). <paramref name="commit"/> false = preview counts only.
    /// </summary>
    Task<(ApplyRuleResultDto? Result, CategorizationRuleWriteError Error)> ApplyToExistingAsync(Guid id, bool commit, CancellationToken cancellationToken = default);
}
