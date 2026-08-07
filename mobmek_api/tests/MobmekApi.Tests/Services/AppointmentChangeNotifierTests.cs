using MobmekApi.Services;

namespace MobmekApi.Tests.Services;

public class AppointmentChangeNotifierTests
{
    [Fact]
    public async Task NotifyChanged_SignalsASubscriber()
    {
        var notifier = new AppointmentChangeNotifier();
        using var subscription = notifier.Subscribe(out var reader);

        notifier.NotifyChanged();

        // Times out (xUnit fails the test) if the signal never arrives.
        var signalled = await reader.WaitToReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(signalled);
    }

    [Fact]
    public async Task NotifyChanged_SignalsEverySubscriber()
    {
        var notifier = new AppointmentChangeNotifier();
        using var subscriptionA = notifier.Subscribe(out var readerA);
        using var subscriptionB = notifier.Subscribe(out var readerB);

        notifier.NotifyChanged();

        Assert.True(await readerA.WaitToReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.True(await readerB.WaitToReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void NotifyChanged_Coalesces_WhenSubscriberHasNotDrainedThePreviousSignal()
    {
        // A subscriber only ever needs to know "something changed since you last looked" —
        // a burst of writes before it reads once shouldn't block the writer or queue up.
        var notifier = new AppointmentChangeNotifier();
        using var subscription = notifier.Subscribe(out var reader);

        notifier.NotifyChanged();
        notifier.NotifyChanged();
        notifier.NotifyChanged();

        Assert.True(reader.TryRead(out _));
        Assert.False(reader.TryRead(out _));
    }

    [Fact]
    public void NotifyChanged_WithNoSubscribers_DoesNotThrow()
    {
        var notifier = new AppointmentChangeNotifier();

        var exception = Record.Exception(notifier.NotifyChanged);

        Assert.Null(exception);
    }

    [Fact]
    public void NotifyChanged_AfterSubscriptionDisposed_DoesNotThrow()
    {
        var notifier = new AppointmentChangeNotifier();
        var subscription = notifier.Subscribe(out _);
        subscription.Dispose();

        var exception = Record.Exception(notifier.NotifyChanged);

        Assert.Null(exception);
    }
}
