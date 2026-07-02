using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class BusinessDetailsService(AppDbContext db) : IBusinessDetailsService
{
    public async Task<BusinessDetailsDto> GetCurrentAsync(CancellationToken cancellationToken = default) =>
        ToDto(await GetOrCreateAsync(cancellationToken));

    public async Task<BusinessDetailsDto> UpdateAsync(UpdateBusinessDetailsRequest request, CancellationToken cancellationToken = default)
    {
        var details = await GetOrCreateAsync(cancellationToken);
        details.Name = request.Name;
        details.Address = request.Address;
        details.Email = request.Email;
        details.BusinessPhone = request.BusinessPhone;
        details.Telephone = request.Telephone;
        details.GstNumber = request.GstNumber;
        details.Website = request.Website;
        details.BankDetails = request.BankDetails;
        details.LogoUrl = request.LogoUrl;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(details);
    }

    // Business details is a singleton: return the existing row, or create the default one on first use.
    private async Task<BusinessDetails> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        var details = await db.BusinessDetails.OrderBy(b => b.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);
        if (details is null)
        {
            details = new BusinessDetails();
            db.BusinessDetails.Add(details);
            await db.SaveChangesAsync(cancellationToken);
        }

        return details;
    }

    private static BusinessDetailsDto ToDto(BusinessDetails b) =>
        new(b.Id, b.Name, b.Address, b.Email, b.BusinessPhone, b.Telephone, b.GstNumber, b.Website, b.BankDetails, b.LogoUrl,
            b.CreatedAtUtc, b.UpdatedAtUtc);
}
