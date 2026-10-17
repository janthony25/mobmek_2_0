using MobmekApi.Services;

namespace MobmekApi.Tests.Fakes;

/// <summary>Scriptable <see cref="IXeroAccountingClient"/> test double — same shape as
/// <see cref="FakeGoogleCalendarClient"/>. Records every call for assertions.</summary>
public class FakeXeroAccountingClient : IXeroAccountingClient
{
    private readonly Queue<Exception?> _pushFailures = new();
    private readonly Queue<Exception?> _voidFailures = new();

    /// <summary>Seed to script "a Contact with this email already exists."</summary>
    public Dictionary<string, string> ContactIdByEmail { get; } = [];

    public List<(string Name, string? Email)> CreatedContacts { get; } = [];

    public List<XeroPushInvoiceRequest> PushedInvoices { get; } = [];

    public List<string> VoidedInvoiceIds { get; } = [];

    public List<XeroAccountSummary> Accounts { get; set; } = [];

    public void EnqueuePushFailure(Exception ex) => _pushFailures.Enqueue(ex);

    public void EnqueueVoidFailure(Exception ex) => _voidFailures.Enqueue(ex);

    public Task<string?> FindContactIdByEmailAsync(string accessToken, string tenantId, string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(ContactIdByEmail.GetValueOrDefault(email));

    public Task<string> CreateContactAsync(string accessToken, string tenantId, string name, string? email, CancellationToken cancellationToken = default)
    {
        CreatedContacts.Add((name, email));
        var id = $"fake-contact-{Guid.NewGuid():N}";
        if (email is not null)
        {
            ContactIdByEmail[email] = id;
        }

        return Task.FromResult(id);
    }

    public Task<XeroPushInvoiceResult> PushInvoiceAsync(string accessToken, string tenantId, XeroPushInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        if (_pushFailures.Count > 0 && _pushFailures.Dequeue() is { } ex)
        {
            throw ex;
        }

        PushedInvoices.Add(request);
        var id = request.ExistingXeroInvoiceId ?? $"fake-invoice-{Guid.NewGuid():N}";
        return Task.FromResult(new XeroPushInvoiceResult(id));
    }

    public Task VoidInvoiceAsync(string accessToken, string tenantId, string xeroInvoiceId, CancellationToken cancellationToken = default)
    {
        if (_voidFailures.Count > 0 && _voidFailures.Dequeue() is { } ex)
        {
            throw ex;
        }

        VoidedInvoiceIds.Add(xeroInvoiceId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<XeroAccountSummary>> ListActiveRevenueAccountsAsync(string accessToken, string tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<XeroAccountSummary>>(Accounts);
}
