using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MobmekApi.Entities;

namespace MobmekApi.Services;

/// <summary>
/// Sends mail through the Resend REST API (https://resend.com/docs/api-reference/emails/send-email)
/// over a typed <see cref="HttpClient"/> (registered via <c>AddHttpClient</c> in Program.cs, base
/// address <c>https://api.resend.com/</c>). The API key is read from configuration on every call
/// (not cached at construction) so a secret change doesn't need a process restart to take effect.
/// </summary>
public class ResendEmailSender(HttpClient httpClient, IConfiguration configuration, ILogger<ResendEmailSender> logger) : IEmailSender
{
    private string? ApiKey => configuration["Email:Resend:ApiKey"];

    public async Task<EmailSendResult> SendAsync(OutboundEmailMessage message, CancellationToken cancellationToken = default)
    {
        var apiKey = ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new EmailSendResult(false, null, null, NotConfigured: true);
        }

        var payload = new ResendSendRequest(
            From: $"{message.FromName} <{message.FromAddress}>",
            To: [message.To],
            Cc: string.IsNullOrWhiteSpace(message.Cc) ? null : [message.Cc],
            Bcc: string.IsNullOrWhiteSpace(message.Bcc) ? null : [message.Bcc],
            ReplyTo: string.IsNullOrWhiteSpace(message.ReplyTo) ? null : message.ReplyTo,
            Subject: message.Subject,
            Html: message.Html,
            Attachments: message.Attachments is { Count: > 0 }
                ? message.Attachments.Select(a => new ResendAttachment(a.FileName, Convert.ToBase64String(a.Content))).ToArray()
                : null);

        var response = await SendWithRetryAsync(apiKey, payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = await SafeReadErrorAsync(response, cancellationToken);
            logger.LogWarning("Resend send failed ({Status}): {Message}", (int)response.StatusCode, errorMessage);
            return new EmailSendResult(false, null, errorMessage ?? $"Resend returned {(int)response.StatusCode}");
        }

        var result = await response.Content.ReadFromJsonAsync<ResendSendResponse>(cancellationToken: cancellationToken);
        return new EmailSendResult(true, result?.Id, null);
    }

    public async Task<EmailProviderStatus> GetStatusAsync(string providerMessageId, CancellationToken cancellationToken = default)
    {
        var apiKey = ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            // Leave status unchanged — nothing to look up without a key.
            return new EmailProviderStatus(OutboundEmailStatus.Sent, null);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"emails/{providerMessageId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // A transient lookup failure isn't a delivery outcome — try again next poll tick.
            return new EmailProviderStatus(OutboundEmailStatus.Sent, null);
        }

        var result = await response.Content.ReadFromJsonAsync<ResendStatusResponse>(cancellationToken: cancellationToken);
        return MapStatus(result?.LastEvent);
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(string apiKey, ResendSendRequest payload, CancellationToken cancellationToken)
    {
        var response = await PostAsync(apiKey, payload, cancellationToken);
        if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
        {
            response.Dispose();
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            response = await PostAsync(apiKey, payload, cancellationToken);
        }

        return response;
    }

    private async Task<HttpResponseMessage> PostAsync(string apiKey, ResendSendRequest payload, CancellationToken cancellationToken)
    {
        // Must await inside the `using` — returning the un-awaited Task let `request` (and its
        // JsonContent) get disposed before the real async socket write completed, which only
        // ever showed up against a real HttpClient, not the synchronously-resolving test stub.
        using var request = new HttpRequestMessage(HttpMethod.Post, "emails") { Content = JsonContent.Create(payload) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return await httpClient.SendAsync(request, cancellationToken);
    }

    private static async Task<string?> SafeReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<ResendErrorResponse>(cancellationToken: cancellationToken);
            return body?.Message;
        }
        catch
        {
            // Error body wasn't JSON/parseable — the status code alone is still reported by the caller.
            return null;
        }
    }

    private static EmailProviderStatus MapStatus(string? lastEvent) => lastEvent switch
    {
        "delivered" => new EmailProviderStatus(OutboundEmailStatus.Delivered, null),
        "bounced" => new EmailProviderStatus(OutboundEmailStatus.Bounced, "Bounced"),
        "complained" => new EmailProviderStatus(OutboundEmailStatus.Complained, "Marked as spam"),
        _ => new EmailProviderStatus(OutboundEmailStatus.Sent, null),
    };

    private record ResendSendRequest(
        string From, string[] To, string[]? Cc, string[]? Bcc,
        [property: JsonPropertyName("reply_to")] string? ReplyTo,
        string Subject, string Html, ResendAttachment[]? Attachments);

    /// <summary>Resend's attachment shape: filename + base64-encoded content, no data-URI prefix.</summary>
    private record ResendAttachment(string Filename, string Content);

    private record ResendSendResponse(string Id);

    private record ResendStatusResponse([property: JsonPropertyName("last_event")] string? LastEvent);

    private record ResendErrorResponse(string? Message);
}
