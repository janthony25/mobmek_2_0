using System.Collections.Concurrent;
using System.Threading.Channels;

namespace MobmekApi.Services;

public sealed class AppointmentChangeNotifier : IAppointmentChangeNotifier
{
    private readonly ConcurrentDictionary<Guid, ChannelWriter<byte>> _subscribers = new();

    public void NotifyChanged()
    {
        foreach (var writer in _subscribers.Values)
        {
            // Bounded(1) + DropWrite: a subscriber only ever needs to know "something
            // changed since you last looked", not one signal per write. If a burst of
            // writes lands before the subscriber has drained the pending signal, the
            // extra ones are redundant — it's going to refetch current state either way.
            writer.TryWrite(0);
        }
    }

    public IDisposable Subscribe(out ChannelReader<byte> reader)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<byte>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false,
        });

        _subscribers[id] = channel.Writer;
        reader = channel.Reader;

        return new Subscription(this, id);
    }

    private sealed class Subscription(AppointmentChangeNotifier owner, Guid id) : IDisposable
    {
        public void Dispose() => owner._subscribers.TryRemove(id, out _);
    }
}
