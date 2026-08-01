namespace Microsandbox;

/// <summary>Raw frame flags used by the agent protocol.</summary>
public static class AgentFrameFlags
{
    /// <summary>Marks terminal-session traffic.</summary>
    public const byte Terminal = 0b0000_0001;

    /// <summary>Marks the first frame of a session.</summary>
    public const byte SessionStart = 0b0000_0010;

    /// <summary>Requests session shutdown.</summary>
    public const byte Shutdown = 0b0000_0100;
}

/// <summary>Contains one raw agent protocol frame with an unparsed CBOR body.</summary>
public sealed record RawFrame(uint Id, byte Flags, byte[] Body);

/// <summary>Owns a low-level native connection to agentd through a sandbox relay socket.</summary>
public sealed class AgentClient : IAsyncDisposable
{
    private readonly NativeApi _native;
    private readonly ConsumingCloseState _state;

    internal AgentClient(NativeApi native, ulong handle)
    {
        _native = native;
        _state = new ConsumingCloseState(handle);
    }

    /// <summary>Sends one raw frame and waits for one response frame.</summary>
    public Task<RawFrame> RequestAsync(
        byte flags,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken = default) =>
        _native.AgentRequestAsync(GetHandle(), flags, body, cancellationToken);

    /// <summary>Opens a raw streaming session.</summary>
    public async Task<AgentStream> StreamAsync(
        byte flags,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken = default)
    {
        var agentHandle = GetHandle();
        var stream = await _native.AgentStreamOpenAsync(agentHandle, flags, body, cancellationToken)
            .ConfigureAwait(false);
        return new AgentStream(_native, agentHandle, stream.StreamHandle, stream.Id);
    }

    /// <summary>Sends a follow-up frame on an existing correlation ID.</summary>
    public Task SendAsync(
        uint id,
        byte flags,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken = default) =>
        _native.AgentSendAsync(GetHandle(), id, flags, body, cancellationToken);

    /// <summary>Returns a copy of the cached handshake <c>core.ready</c> CBOR body.</summary>
    public byte[] GetReadyBytes() => _native.AgentReadyBytes(GetHandle());

    /// <summary>Closes and consumes the native agent connection handle.</summary>
    public Task CloseAsync(CancellationToken cancellationToken = default) =>
        _state.CloseAsync(_native.AgentCloseAsync, cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await CloseAsync(CancellationToken.None).ConfigureAwait(false);

    private ulong GetHandle() => _state.GetHandle(nameof(AgentClient));

    internal static long ConsumeHandle(ref long handle) => Interlocked.Exchange(ref handle, 0);
}

/// <summary>Owns one open raw agent streaming session.</summary>
public sealed class AgentStream : IAsyncDisposable
{
    private readonly NativeApi _native;
    private readonly ulong _agentHandle;
    private readonly ConsumingCloseState _state;

    internal AgentStream(NativeApi native, ulong agentHandle, ulong streamHandle, uint id)
    {
        _native = native;
        _agentHandle = agentHandle;
        _state = new ConsumingCloseState(streamHandle);
        Id = id;
    }

    /// <summary>Gets the protocol correlation ID used by follow-up frames.</summary>
    public uint Id { get; }

    /// <summary>Waits for the next raw frame, or returns <see langword="null"/> at EOF.</summary>
    public Task<RawFrame?> ReceiveAsync(CancellationToken cancellationToken = default) =>
        _native.AgentStreamNextAsync(_agentHandle, GetHandle(), cancellationToken);

    /// <summary>Closes and consumes the native stream handle.</summary>
    public Task CloseAsync(CancellationToken cancellationToken = default) =>
        _state.CloseAsync(
            (handle, token) => _native.AgentStreamCloseAsync(_agentHandle, handle, token),
            cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await CloseAsync(CancellationToken.None).ConfigureAwait(false);

    private ulong GetHandle() => _state.GetHandle(nameof(AgentStream));

    internal static long ConsumeHandle(ref long handle) => Interlocked.Exchange(ref handle, 0);
}
