using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class EmailTemplateService(AppDbContext db) : IEmailTemplateService
{
    // Defaults seeded on first read of each key. Kept here (not a migration seed) so the same
    // get-or-create-on-read guarantee holds in every environment, including a freshly-migrated
    // production database that never ran a dev-only seeder.
    private static readonly Dictionary<string, (string Name, string Subject, string Intro)> Defaults = new()
    {
        [EmailTemplateKeys.InvoiceSend] = (
            "Invoice / Quote Send",
            "{{DocumentLabel}} {{DocumentNumber}} from {{BusinessName}}",
            "Please find your {{DocumentLabel}} attached as a PDF."),
        [EmailTemplateKeys.ReminderDue] = (
            "Reminder Due",
            "Reminder: {{ReminderTitle}} — {{BusinessName}}",
            "Hi {{CustomerName}}, this is a reminder from {{BusinessName}} that \"{{ReminderTitle}}\" is due {{DueDate}}."),
        [EmailTemplateKeys.AppointmentConfirmation] = (
            "Appointment Confirmation",
            "Your appointment with {{BusinessName}} — {{AppointmentDate}}",
            "Hi {{CustomerName}}, this confirms your appointment \"{{AppointmentTitle}}\" on {{AppointmentDate}} at {{AppointmentTime}}."),
    };

    private static readonly IReadOnlyDictionary<string, string?> PreviewTokens = new Dictionary<string, string?>
    {
        ["BusinessName"] = "Mobmek Workshop",
        ["CustomerName"] = "Jane Doe",
        ["DocumentLabel"] = "Invoice",
        ["DocumentNumber"] = "INV-0001",
        ["DueDate"] = "15 Oct 2026",
        ["ReminderTitle"] = "WOF due",
        ["AppointmentTitle"] = "Brake inspection",
        ["AppointmentDate"] = "15 Oct 2026",
        ["AppointmentTime"] = "9:00 AM",
        ["CarPlate"] = "ABC123",
    };

    public async Task<IReadOnlyList<EmailTemplateDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var templates = await db.EmailTemplates.AsNoTracking().ToListAsync(cancellationToken);
        return EmailTemplateKeys.All
            .Select(key => templates.First(t => t.Key == key))
            .Select(ToDto)
            .ToList();
    }

    public async Task<EmailTemplateDto?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!Defaults.ContainsKey(key))
        {
            return null;
        }

        await EnsureSeededAsync(cancellationToken);
        var template = await db.EmailTemplates.AsNoTracking().FirstAsync(t => t.Key == key, cancellationToken);
        return ToDto(template);
    }

    public async Task<EmailTemplateDto?> UpdateAsync(string key, UpdateEmailTemplateRequest request, CancellationToken cancellationToken = default)
    {
        if (!Defaults.ContainsKey(key))
        {
            return null;
        }

        await EnsureSeededAsync(cancellationToken);
        var template = await db.EmailTemplates.FirstAsync(t => t.Key == key, cancellationToken);
        template.SubjectTemplate = request.SubjectTemplate;
        template.BodyIntroTemplate = request.BodyIntroTemplate;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(template);
    }

    public async Task<EmailTemplatePreviewDto?> PreviewAsync(string key, CancellationToken cancellationToken = default)
    {
        var template = await GetByKeyAsync(key, cancellationToken);
        return template is null
            ? null
            : new EmailTemplatePreviewDto(
                EmailTemplateRenderer.Render(template.SubjectTemplate, PreviewTokens),
                EmailTemplateRenderer.Render(template.BodyIntroTemplate, PreviewTokens));
    }

    private async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        var existingKeys = await db.EmailTemplates.Select(t => t.Key).ToListAsync(cancellationToken);
        var missing = Defaults.Keys.Except(existingKeys);

        var added = false;
        foreach (var key in missing)
        {
            var (name, subject, intro) = Defaults[key];
            db.EmailTemplates.Add(new EmailTemplate
            {
                Key = key,
                Name = name,
                SubjectTemplate = subject,
                BodyIntroTemplate = intro,
            });
            added = true;
        }

        if (added)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static EmailTemplateDto ToDto(EmailTemplate t) =>
        new(t.Id, t.Key, t.Name, t.SubjectTemplate, t.BodyIntroTemplate, t.IsSystem, t.CreatedAtUtc, t.UpdatedAtUtc, t.UpdatedByName);
}
