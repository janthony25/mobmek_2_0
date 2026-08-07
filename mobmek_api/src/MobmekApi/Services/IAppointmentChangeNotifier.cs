namespace MobmekApi.Services;

/// <summary>
/// In-process pub/sub so the staff calendar can update itself the instant an appointment is
/// created, edited, or deleted — including a booking arriving from the public website —
/// without polling. Carries no payload: subscribers just refetch on signal, the same way a
/// manual refresh already works.
/// </summary>
/// <remarks>
/// Single-instance only by design: this broadcasts within one process. Fine at this app's
/// current scale (one API container); scaling to multiple instances would need a shared
/// backplane (e.g. Redis pub/sub) for a subscriber connected to instance A to hear about a
/// write that landed on instance B.
/// </remarks>
public interface IAppointmentChangeNotifier
{
    /// <summary>Call after any appointment create/update/delete commits successfully.</summary>
    void NotifyChanged();

    /// <summary>
    /// Registers a live subscriber. Reading <paramref name="reader"/> yields once per
    /// <see cref="NotifyChanged"/> call; dispose the returned handle to unsubscribe (do this
    /// when the client disconnects, or the subscriber list leaks).
    /// </summary>
    IDisposable Subscribe(out System.Threading.Channels.ChannelReader<byte> reader);
}
