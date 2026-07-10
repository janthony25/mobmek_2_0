using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class BusinessDetailsService(AppDbContext db, IFileStorage fileStorage) : IBusinessDetailsService
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
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(details);
    }

    public async Task<BusinessDetailsDto> UploadLogoAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var details = await GetOrCreateAsync(cancellationToken);
        if (details.LogoStorageKey is not null)
        {
            await fileStorage.DeleteAsync(details.LogoStorageKey, cancellationToken);
        }

        details.LogoStorageKey = await fileStorage.SaveAsync(content, fileName, cancellationToken);
        details.LogoFileName = fileName;
        details.LogoContentType = contentType;
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(details);
    }

    public async Task<(string FileName, string ContentType, Stream Content)?> GetLogoAsync(CancellationToken cancellationToken = default)
    {
        var details = await GetOrCreateAsync(cancellationToken);
        if (details.LogoStorageKey is null)
        {
            return null;
        }

        var content = await fileStorage.OpenReadAsync(details.LogoStorageKey, cancellationToken);
        return content is null ? null : (details.LogoFileName!, details.LogoContentType!, content);
    }

    public async Task<bool> DeleteLogoAsync(CancellationToken cancellationToken = default)
    {
        var details = await GetOrCreateAsync(cancellationToken);
        if (details.LogoStorageKey is null)
        {
            return false;
        }

        await fileStorage.DeleteAsync(details.LogoStorageKey, cancellationToken);
        details.LogoStorageKey = null;
        details.LogoFileName = null;
        details.LogoContentType = null;
        await db.SaveChangesAsync(cancellationToken);

        return true;
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
        new(b.Id, b.Name, b.Address, b.Email, b.BusinessPhone, b.Telephone, b.GstNumber, b.Website, b.BankDetails,
            b.LogoStorageKey is null ? null : "/business-details/logo",
            b.CreatedAtUtc, b.UpdatedAtUtc, b.UpdatedByName);
}
