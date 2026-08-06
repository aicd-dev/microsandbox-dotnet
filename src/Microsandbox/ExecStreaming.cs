using System.Text.Json.Serialization;

namespace Microsandbox;

/// <summary>Structured details for an exec process that failed to start or accept stdin.</summary>
public sealed record ExecFailure(
    [property: JsonPropertyName("kind")] string? Kind = null,
    [property: JsonPropertyName("errno")] int? Errno = null,
    [property: JsonPropertyName("errno_name")] string? ErrnoName = null,
    [property: JsonPropertyName("message")] string Message = "",
    [property: JsonPropertyName("path")] string? Path = null
);

/// <summary>Base type for events received from a streaming exec session.</summary>
public abstract record ExecEvent;

/// <summary>Indicates that the guest process started.</summary>
public sealed record ExecStartedEvent(uint ProcessId) : ExecEvent;

/// <summary>Carries a standard-output byte chunk.</summary>
public sealed record ExecStandardOutputEvent(byte[] Data) : ExecEvent;

/// <summary>Carries a standard-error byte chunk.</summary>
public sealed record ExecStandardErrorEvent(byte[] Data) : ExecEvent;

/// <summary>Indicates that the guest process exited.</summary>
public sealed record ExecExitedEvent(int ExitCode) : ExecEvent;

/// <summary>Indicates that the guest process failed to start.</summary>
public sealed record ExecFailedEvent(ExecFailure Failure) : ExecEvent;

/// <summary>Reports a non-terminal failure while writing process stdin.</summary>
public sealed record ExecStdinErrorEvent(ExecFailure Failure) : ExecEvent;

/// <summary>Indicates that every exec event has been consumed.</summary>
public sealed record ExecDoneEvent : ExecEvent
{
    private ExecDoneEvent() { }

    /// <summary>Gets the shared completion event instance.</summary>
    public static ExecDoneEvent Instance { get; } = new();
}

/// <summary>A live streaming exec session backed by an owned native handle.</summary>
public sealed class ExecHandle : IAsyncDisposable
{
    private readonly NativeApi _native;
    private readonly bool _hasStdin;
    private long _handle;
    private int _stdinTaken;

    internal ExecHandle(NativeApi native, ulong handle, bool hasStdin)
    {
        _native = native;
        _handle = checked((long)handle);
        _hasStdin = hasStdin;
    }

    /// <summary>Gets the protocol identifier assigned to this exec session.</summary>
    public string Id => _native.ExecId(GetHandle());

    /// <summary>Receives the next typed event, including <see cref="ExecDoneEvent"/> at end of stream.</summary>
    public Task<ExecEvent> ReceiveAsync(CancellationToken cancellationToken = default) =>
        _native.ExecReceiveAsync(GetHandle(), cancellationToken);

    /// <summary>Drains remaining events and returns collected output.</summary>
    public Task<ExecResult> CollectAsync(CancellationToken cancellationToken = default) =>
        _native.ExecCollectAsync(GetHandle(), cancellationToken);

    /// <summary>Waits for the process to exit and returns its exit code.</summary>
    public Task<int> WaitAsync(CancellationToken cancellationToken = default) =>
        _native.ExecWaitAsync(GetHandle(), cancellationToken);

    /// <summary>Sends SIGKILL to the process.</summary>
    public Task KillAsync(CancellationToken cancellationToken = default) =>
        _native.ExecKillAsync(GetHandle(), cancellationToken);

    /// <summary>Sends a Unix signal number to the process.</summary>
    public Task SignalAsync(int signal, CancellationToken cancellationToken = default) =>
        _native.ExecSignalAsync(GetHandle(), signal, cancellationToken);

    /// <summary>Resizes the pseudo-terminal allocated for this process.</summary>
    public Task ResizeAsync(
        ushort rows,
        ushort columns,
        CancellationToken cancellationToken = default
    ) => _native.ExecResizeAsync(GetHandle(), rows, columns, cancellationToken);

    /// <summary>
    /// Takes the process stdin sink once, or returns <see langword="null"/> when stdin was not piped or was already taken.
    /// </summary>
    public ExecStdinSink? TakeStdin() =>
        _hasStdin && Interlocked.Exchange(ref _stdinTaken, 1) == 0
            ? new ExecStdinSink(this, _native)
            : null;

    /// <summary>Releases the native exec handle without killing the process.</summary>
    public async ValueTask DisposeAsync()
    {
        var handle = ConsumeHandle(ref _handle);
        if (handle != 0)
        {
            await _native
                .ExecCloseAsync(checked((ulong)handle), CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    internal ulong GetHandle()
    {
        var handle = Interlocked.Read(ref _handle);
        return handle != 0
            ? checked((ulong)handle)
            : throw new ObjectDisposedException(nameof(ExecHandle));
    }

    internal static long ConsumeHandle(ref long handle) => Interlocked.Exchange(ref handle, 0);
}

/// <summary>A single-owner asynchronous sink for a streaming exec session's stdin.</summary>
public sealed class ExecStdinSink : IAsyncDisposable
{
    private readonly ExecHandle _exec;
    private readonly NativeApi _native;
    private readonly RetryableCompletionState _completion = new();

    internal ExecStdinSink(ExecHandle exec, NativeApi native)
    {
        _exec = exec;
        _native = native;
    }

    /// <summary>Writes a byte chunk to process stdin using standard base64 at the native boundary.</summary>
    public Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        _completion.EnsureOpen(nameof(ExecStdinSink));
        return _native.ExecStdinWriteAsync(_exec.GetHandle(), data, cancellationToken);
    }

    /// <summary>Closes process stdin. This operation may only be completed once.</summary>
    public Task CompleteAsync(CancellationToken cancellationToken = default) =>
        _completion.CompleteAsync(
            token => _native.ExecStdinCloseAsync(_exec.GetHandle(), token),
            cancellationToken
        );

    /// <summary>Completes process stdin without allowing cleanup cancellation.</summary>
    public async ValueTask DisposeAsync() =>
        await CompleteAsync(CancellationToken.None).ConfigureAwait(false);
}
