using MobmekApi.Entities;
using MobmekApi.Services;

namespace MobmekApi.Tests.Fakes;

/// <summary>
/// Scriptable <see cref="IEmailSender"/> test double — no mocking library is used in this repo.
/// Enqueue results for <see cref="SendAsync"/>/<see cref="GetStatusAsync"/> to script a scenario;
/// <see cref="PreSendCallback"/> fires right as <see cref="SendAsync"/> is invoked, before the
/// caller processes the result, so a test can assert the DB already has a Queued row at that
/// exact moment (the "audit before provider call" invariant).
/// </summary>
public class FakeEmailSender : IEmailSender
{
    private readonly Queue<EmailSendResult> _sendResults = new();
    private readonly Queue<EmailProviderStatus> _statusResults = new();

    public List<OutboundEmailMessage> SentMessages { get; } = [];

    public Action? PreSendCallback { get; set; }

    public void EnqueueSendResult(EmailSendResult result) => _sendResults.Enqueue(result);

    public void EnqueueStatusResult(EmailProviderStatus result) => _statusResults.Enqueue(result);

    public Task<EmailSendResult> SendAsync(OutboundEmailMessage message, CancellationToken cancellationToken = default)
    {
        SentMessages.Add(message);
        PreSendCallback?.Invoke();

        var result = _sendResults.Count > 0
            ? _sendResults.Dequeue()
            : new EmailSendResult(true, $"fake-{Guid.NewGuid():N}", null);
        return Task.FromResult(result);
    }

    public Task<EmailProviderStatus> GetStatusAsync(string providerMessageId, CancellationToken cancellationToken = default)
    {
        var result = _statusResults.Count > 0
            ? _statusResults.Dequeue()
            : new EmailProviderStatus(OutboundEmailStatus.Sent, null);
        return Task.FromResult(result);
    }
}
