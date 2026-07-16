#if WINDOWS
using Windows.Foundation;
#endif

namespace AgentTrafficLight.Server.Utils;

/// <summary>
/// Helpers for converting event-based APIs into awaitable tasks.
/// </summary>
public static class EventAsync
{
#if WINDOWS
    /// <summary>
    /// Waits for a <see cref="TypedEventHandler{TSender, TResult}"/> event that satisfies the given predicate.
    /// </summary>
    /// <ttypeparam name="TSender">The sender type.</ttypeparam>
    /// <ttypeparam name="TResult">The event argument type.</ttypeparam>
    /// <param name="add">Action that subscribes the handler.</param>
    /// <param name="remove">Action that unsubscribes the handler.</param>
    /// <param name="predicate">Optional predicate that decides whether the event matches. Defaults to the first event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes with the matching event arguments.</returns>
    public static Task<TResult> WaitForEventAsync<TSender, TResult>(
        Action<TypedEventHandler<TSender, TResult>> add,
        Action<TypedEventHandler<TSender, TResult>> remove,
        Func<TResult, bool>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(add);
        ArgumentNullException.ThrowIfNull(remove);

        predicate ??= _ => true;

        var tcs = new TaskCompletionSource<TResult>();
        TypedEventHandler<TSender, TResult> handler = null!;

        handler = (_, args) =>
        {
            if (predicate(args))
            {
                remove(handler);
                tcs.TrySetResult(args);
            }
        };

        add(handler);
        RegisterCancellation(tcs, remove, handler, cancellationToken);

        return tcs.Task;
    }
#endif

    /// <summary>
    /// Waits for an <see cref="EventHandler{TEventArgs}"/> event that satisfies the given predicate.
    /// </summary>
    /// <ttypeparam name="TEventArgs">The event argument type.</ttypeparam>
    /// <param name="add">Action that subscribes the handler.</param>
    /// <param name="remove">Action that unsubscribes the handler.</param>
    /// <param name="predicate">Optional predicate that decides whether the event matches. Defaults to the first event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes with the matching event arguments.</returns>
    public static Task<TEventArgs> WaitForEventAsync<TEventArgs>(
        Action<EventHandler<TEventArgs>> add,
        Action<EventHandler<TEventArgs>> remove,
        Func<TEventArgs, bool>? predicate = null,
        CancellationToken cancellationToken = default)
        where TEventArgs : EventArgs
    {
        ArgumentNullException.ThrowIfNull(add);
        ArgumentNullException.ThrowIfNull(remove);

        predicate ??= _ => true;

        var tcs = new TaskCompletionSource<TEventArgs>();
        EventHandler<TEventArgs> handler = null!;

        handler = (_, args) =>
        {
            if (predicate(args))
            {
                remove(handler);
                tcs.TrySetResult(args);
            }
        };

        add(handler);
        RegisterCancellation(tcs, remove, handler, cancellationToken);

        return tcs.Task;
    }

    private static void RegisterCancellation<THandler, TResult>(
        TaskCompletionSource<TResult> tcs,
        Action<THandler> remove,
        THandler handler,
        CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled)
        {
            return;
        }

        var registration = cancellationToken.Register(() =>
        {
            remove(handler);
            tcs.TrySetCanceled(cancellationToken);
        });

        _ = tcs.Task.ContinueWith(
            _ => registration.Dispose(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
