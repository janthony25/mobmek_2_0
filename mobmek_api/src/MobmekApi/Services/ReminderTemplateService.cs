using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class ReminderTemplateService(AppDbContext db) : IReminderTemplateService
{
    public async Task<IReadOnlyList<ReminderTemplateDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.ReminderTemplates
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => ToDto(t))
            .ToListAsync(cancellationToken);
    }

    public async Task<ReminderTemplateDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await db.ReminderTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        return template is null ? null : ToDto(template);
    }

    public async Task<ReminderTemplateDto> CreateAsync(CreateReminderTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var template = new ReminderTemplate
        {
            Name = request.Name,
            Description = request.Description,
            DefaultIntervalMonths = request.DefaultIntervalMonths,
        };

        db.ReminderTemplates.Add(template);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(template);
    }

    public async Task<ReminderTemplateDto?> UpdateAsync(Guid id, UpdateReminderTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var template = await db.ReminderTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (template is null)
        {
            return null;
        }

        template.Name = request.Name;
        template.Description = request.Description;
        template.DefaultIntervalMonths = request.DefaultIntervalMonths;
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(template);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await db.ReminderTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (template is null)
        {
            return false;
        }

        db.ReminderTemplates.Remove(template);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static ReminderTemplateDto ToDto(ReminderTemplate t) =>
        new(t.Id, t.Name, t.Description, t.DefaultIntervalMonths, t.CreatedAtUtc, t.UpdatedAtUtc);
}
