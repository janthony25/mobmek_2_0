using System.Net;
using System.Net.Http.Json;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace MobmekApi.Tests.Services;

/// <summary>Routes requests to a scriptable queue of responses instead of the network, and
/// records every request that came through — no extra package needed for this.</summary>
internal class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>The raw JSON body of each request, captured during the same read that guards
    /// against the dispose-before-send bug (see below) — re-reading Content afterwards isn't
    /// reliable once the caller's `using` has disposed the request.</summary>
    public List<string> RequestBodies { get; } = [];

    public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        // A real HttpClient writes the request body over the network asynchronously — it does
        // NOT resolve synchronously the way `Task.FromResult` does. Forcing a real async gap
        // (Task.Yield) before touching Content is what makes this stub catch a caller that
        // disposes the request/content before the send actually completes, instead of masking
        // it the way a synchronously-resolving stub would.
        await Task.Yield();
        if (request.Content is not null)
        {
            RequestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
        }

        return _responses.Count > 0 ? _responses.Dequeue() : new HttpResponseMessage(HttpStatusCode.OK);
    }
}

public class ResendEmailSenderTests
{
    private static readonly OutboundEmailMessage Message = new(
        "customer@example.com", "Customer", null, null, "shop@example.com", "Mobmek Workshop", "accounts@mobmek.co.nz",
        "Invoice INV-0001", "<p>Body</p>");

    private static (ResendEmailSender Sender, StubHttpMessageHandler Handler) Build(string? apiKey = "re_test_key")
    {
        var handler = new StubHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(apiKey is null
                ? []
                : new Dictionary<string, string?> { ["Email:Resend:ApiKey"] = apiKey })
            .Build();
        return (new ResendEmailSender(httpClient, config, NullLogger<ResendEmailSender>.Instance), handler);
    }

    [Fact]
    public async Task SendAsync_MissingApiKey_ReturnsNotConfigured_WithoutAnyHttpCall()
    {
        var (sender, handler) = Build(apiKey: null);

        var result = await sender.SendAsync(Message);

        Assert.True(result.NotConfigured);
        Assert.False(result.Success);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SendAsync_Success_ReturnsProviderMessageId_AndSendsBearerAuth()
    {
        var (sender, handler) = Build();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { id = "resend-abc" }),
        });

        var result = await sender.SendAsync(Message);

        Assert.True(result.Success);
        Assert.Equal("resend-abc", result.ProviderMessageId);
        Assert.Single(handler.Requests);
        Assert.Equal("Bearer", handler.Requests[0].Headers.Authorization?.Scheme);
        Assert.Equal("re_test_key", handler.Requests[0].Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task SendAsync_WithAttachment_Base64EncodesItIntoTheRequest()
    {
        var (sender, handler) = Build();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { id = "resend-abc" }) });
        var bytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // "%PDF"
        var messageWithAttachment = Message with { Attachments = [new EmailAttachment("INV-0001.pdf", "application/pdf", bytes)] };

        await sender.SendAsync(messageWithAttachment);

        var body = Assert.Single(handler.RequestBodies);
        using var json = System.Text.Json.JsonDocument.Parse(body);
        var attachment = json.RootElement.GetProperty("attachments")[0];
        Assert.Equal("INV-0001.pdf", attachment.GetProperty("filename").GetString());
        Assert.Equal(Convert.ToBase64String(bytes), attachment.GetProperty("content").GetString());
    }

    [Fact]
    public async Task SendAsync_WithoutAttachment_OmitsAttachmentsField()
    {
        var (sender, handler) = Build();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { id = "resend-abc" }) });

        await sender.SendAsync(Message);

        var body = Assert.Single(handler.RequestBodies);
        using var json = System.Text.Json.JsonDocument.Parse(body);
        Assert.True(
            !json.RootElement.TryGetProperty("attachments", out var value) || value.ValueKind == System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task SendAsync_4xx_FailsImmediately_WithoutRetrying()
    {
        var (sender, handler) = Build();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new { message = "Invalid `to` field" }),
        });

        var result = await sender.SendAsync(Message);

        Assert.False(result.Success);
        Assert.False(result.NotConfigured);
        Assert.Equal("Invalid `to` field", result.ErrorMessage);
        Assert.Single(handler.Requests); // no retry on a 4xx
    }

    [Fact]
    public async Task SendAsync_429_RetriesOnce_ThenSucceeds()
    {
        var (sender, handler) = Build();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { id = "resend-retry" }),
        });

        var result = await sender.SendAsync(Message);

        Assert.True(result.Success);
        Assert.Equal("resend-retry", result.ProviderMessageId);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task SendAsync_500_RetriesOnce_ThenFailsIfStillFailing()
    {
        var (sender, handler) = Build();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await sender.SendAsync(Message);

        Assert.False(result.Success);
        Assert.Equal(2, handler.Requests.Count); // exactly one retry, not a loop
    }

    [Fact]
    public async Task GetStatusAsync_MapsDeliveredBouncedAndComplained()
    {
        var (sender, handler) = Build();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { last_event = "delivered" }) });
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { last_event = "bounced" }) });
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { last_event = "complained" }) });

        Assert.Equal(OutboundEmailStatus.Delivered, (await sender.GetStatusAsync("id-1")).Status);
        Assert.Equal(OutboundEmailStatus.Bounced, (await sender.GetStatusAsync("id-2")).Status);
        Assert.Equal(OutboundEmailStatus.Complained, (await sender.GetStatusAsync("id-3")).Status);
    }

    [Fact]
    public async Task GetStatusAsync_UnknownOrInFlightEvent_StaysSent()
    {
        var (sender, handler) = Build();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { last_event = "sent" }) });

        var status = await sender.GetStatusAsync("id-1");

        Assert.Equal(OutboundEmailStatus.Sent, status.Status);
    }

    [Fact]
    public async Task GetStatusAsync_MissingApiKey_ReturnsSent_WithoutAnyHttpCall()
    {
        var (sender, handler) = Build(apiKey: null);

        var status = await sender.GetStatusAsync("id-1");

        Assert.Equal(OutboundEmailStatus.Sent, status.Status);
        Assert.Empty(handler.Requests);
    }
}
