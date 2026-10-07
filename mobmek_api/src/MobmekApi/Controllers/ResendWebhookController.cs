using System.Text.Json;
using System.Text.Json.Serialization;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

/// <summary>
/// Receives Resend's delivery-status webhook. This app has no public HTTPS endpoint today, so
/// the poll job (<see cref="OutboundStatusPollJob"/>) is the real delivery-status mechanism in
/// practice — this endpoint exists so a future public deployment upgrades to real-time for free,
/// and updates land through the exact same no-regress status state machine either way.
/// </summary>
[ApiController]
[Route("api/webhooks/resend")]
[AllowAnonymous]
public class ResendWebhookController(IOutboundEmailService outboundEmailService, IConfiguration configuration, ILogger<ResendWebhookController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        var secret = configuration["Email:Resend:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            // Feature off — no secret configured means nothing should be trusted to call this.
            return NotFound();
        }

        string body;
        using (var reader = new StreamReader(Request.Body))
        {
            body = await reader.ReadToEndAsync(cancellationToken);
        }

        var verified = ResendWebhookVerifier.Verify(
            secret, Request.Headers["svix-id"], Request.Headers["svix-timestamp"], Request.Headers["svix-signature"], body);
        if (!verified)
        {
            logger.LogWarning("Rejected Resend webhook call with an invalid or missing signature.");
            return Unauthorized();
        }

        ResendWebhookEvent? webhookEvent;
        try
        {
            webhookEvent = JsonSerializer.Deserialize<ResendWebhookEvent>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return BadRequest();
        }

        var emailId = webhookEvent?.Data?.EmailId;
        var status = MapEventType(webhookEvent?.Type);
        if (emailId is not null && status is not null)
        {
            await outboundEmailService.ApplyStatusByProviderMessageIdAsync(
                emailId, status.Value.Status, status.Value.Reason, DateTime.UtcNow, cancellationToken);
        }

        return Ok();
    }

    private static (OutboundEmailStatus Status, string? Reason)? MapEventType(string? type) => type switch
    {
        "email.delivered" => (OutboundEmailStatus.Delivered, null),
        "email.bounced" => (OutboundEmailStatus.Bounced, "Bounced"),
        "email.complained" => (OutboundEmailStatus.Complained, "Marked as spam"),
        _ => null, // e.g. email.sent, email.clicked — not a delivery outcome this app tracks
    };

    private record ResendWebhookEvent(string? Type, ResendWebhookEventData? Data);

    private record ResendWebhookEventData([property: JsonPropertyName("email_id")] string? EmailId);
}
