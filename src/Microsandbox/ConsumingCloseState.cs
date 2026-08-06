namespace Microsandbox;

internal sealed class ConsumingCloseState
{
    private readonly SemaphoreSlim _closeGate = new(1, 1);
    private long _handle;

    internal ConsumingCloseState(ulong handle)
    {
        if (handle == 0 || handle > long.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(handle));
        }

        _handle = (long)handle;
    }

    internal bool IsOpen => Volatile.Read(ref _handle) > 0;

    internal ulong GetHandle(string owner)
    {
        var handle = Volatile.Read(ref _handle);
        return handle > 0 ? (ulong)handle : throw new ObjectDisposedException(owner);
    }

    internal async Task CloseAsync(
        Func<ulong, CancellationToken, Task> closeAsync,
        CancellationToken cancellationToken
    )
    {
        await _closeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var handle = Volatile.Read(ref _handle);
            if (handle == 0)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            Volatile.Write(ref _handle, 0);
            await closeAsync((ulong)handle, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _closeGate.Release();
        }
    }
}
