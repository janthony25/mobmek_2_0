using System.Threading.Channels;
using MobmekApi.Services;

namespace MobmekApi.Tests.Fakes;

/// <summary>
/// No-op <see cref="IAppointmentChangeNotifier"/> test double, with a call count for tests
/// that assert a write actually signalled subscribers.
/// </summary>
public class FakeAppointmentChangeNotifier : IAppointmentChangeNotifier
{
    public int NotifyCount { get; private set; }

    public void NotifyChanged() => NotifyCount++;

    public IDisposable Subscribe(out ChannelReader<byte> reader)
    {
        var channel = Channel.CreateUnbounded<byte>();
        reader = channel.Reader;
        return new NoopSubscription();
    }

    private sealed class NoopSubscription : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
