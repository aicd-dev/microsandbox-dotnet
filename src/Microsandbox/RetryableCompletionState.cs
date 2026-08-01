namespace Microsandbox;

internal sealed class RetryableCompletionState
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _complete;

    internal bool IsOpen => Volatile.Read(ref _complete) == 0;

    internal void EnsureOpen(string owner)
    {
        if (!IsOpen)
        {
            throw new ObjectDisposedException(owner);
        }
    }

    internal async Task CompleteAsync(Func<CancellationToken, Task> completeAsync, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsOpen)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await completeAsync(CancellationToken.None).ConfigureAwait(false);
            Volatile.Write(ref _complete, 1);
        }
        finally
        {
            _gate.Release();
        }
    }
}
