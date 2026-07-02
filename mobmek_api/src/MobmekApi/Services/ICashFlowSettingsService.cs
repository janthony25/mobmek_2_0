using MobmekApi.DTOs;

namespace MobmekApi.Services;

public interface ICashFlowSettingsService
{
    /// <summary>Returns the singleton settings row, creating an empty one on first use.</summary>
    Task<CashFlowSettingsDto> GetCurrentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the invoice-payment routing. Returns <c>null</c> when any of the supplied
    /// account ids doesn't reference an existing cash account.
    /// </summary>
    Task<CashFlowSettingsDto?> UpdateAsync(UpdateCashFlowSettingsRequest request, CancellationToken cancellationToken = default);
}
