using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsandbox;

/// <summary>Identifies the source of a persisted sandbox log entry.</summary>
public enum LogSource
{
    /// <summary>Standard output.</summary>
    Stdout,
    /// <summary>Standard error.</summary>
    Stderr,
    /// <summary>Combined process output.</summary>
    Output,
    /// <summary>Runtime and kernel diagnostics.</summary>
    System,
}

/// <summary>Filters a collected log query.</summary>
public sealed class LogOptions
{
    /// <summary>Gets the maximum number of trailing entries to return.</summary>
    public ulong Tail { get; init; }
    /// <summary>Gets the inclusive lower timestamp bound.</summary>
    public DateTimeOffset? Since { get; init; }
    /// <summary>Gets the exclusive upper timestamp bound.</summary>
    public DateTimeOffset? Until { get; init; }
    /// <summary>Gets the sources to include.</summary>
    public IReadOnlyList<LogSource>? Sources { get; init; }

    internal string ToJson() => JsonSerializer.Serialize(new Payload
    {
        Tail = Tail,
        SinceMilliseconds = Since?.ToUnixTimeMilliseconds(),
        UntilMilliseconds = Until?.ToUnixTimeMilliseconds(),
        Sources = Sources,
    }, JsonDefaults.Options);

    private sealed class Payload
    {
        [JsonPropertyName("tail")]
        public ulong Tail { get; init; }

        [JsonPropertyName("since_ms")]
        public long? SinceMilliseconds { get; init; }

        [JsonPropertyName("until_ms")]
        public long? UntilMilliseconds { get; init; }

        [JsonPropertyName("sources")]
        public IReadOnlyList<LogSource>? Sources { get; init; }
    }
}

/// <summary>Configures a live log stream.</summary>
public sealed class LogStreamOptions
{
    /// <summary>Gets the sources to include. The native default includes stdout, stderr, and output.</summary>
    public IReadOnlyList<LogSource>? Sources { get; init; }

    /// <summary>Gets the inclusive timestamp from which to start. Mutually exclusive with <see cref="FromCursor"/>.</summary>
    public DateTimeOffset? Since { get; init; }

    /// <summary>Gets the opaque cursor after which to resume. Mutually exclusive with <see cref="Since"/>.</summary>
    public string? FromCursor { get; init; }

    /// <summary>Gets the exclusive timestamp at which to stop.</summary>
    public DateTimeOffset? Until { get; init; }

    /// <summary>Gets whether the stream remains open to follow new entries.</summary>
    public bool Follow { get; init; }

    internal string ToJson() => JsonSerializer.Serialize(new Payload
    {
        Sources = Sources,
        SinceMilliseconds = Since?.ToUnixTimeMilliseconds(),
        FromCursor = FromCursor,
        UntilMilliseconds = Until?.ToUnixTimeMilliseconds(),
        Follow = Follow,
    }, JsonDefaults.Options);

    private sealed class Payload
    {
        [JsonPropertyName("sources")]
        public IReadOnlyList<LogSource>? Sources { get; init; }

        [JsonPropertyName("since_ms")]
        public long? SinceMilliseconds { get; init; }

        [JsonPropertyName("from_cursor")]
        public string? FromCursor { get; init; }

        [JsonPropertyName("until_ms")]
        public long? UntilMilliseconds { get; init; }

        [JsonPropertyName("follow")]
        public bool Follow { get; init; }
    }
}

/// <summary>One persisted sandbox log entry.</summary>
public sealed record LogEntry(LogSource Source, ulong? SessionId, DateTimeOffset Timestamp, byte[] Data, string Cursor)
{
    /// <summary>Gets the payload decoded as UTF-8 text.</summary>
    public string Text => Encoding.UTF8.GetString(Data);
}

/// <summary>An owned live subscription to persisted sandbox logs.</summary>
public sealed class LogStream : IAsyncDisposable
{
    private readonly NativeApi _native;
    private long _handle;

    internal LogStream(NativeApi native, ulong handle)
    {
        _native = native;
        _handle = checked((long)handle);
    }

    /// <summary>Receives the next entry, or <see langword="null"/> when the stream ends.</summary>
    public Task<LogEntry?> ReceiveAsync(CancellationToken cancellationToken = default) =>
        _native.LogReceiveAsync(GetHandle(), cancellationToken);

    /// <summary>Stops the stream and consumes its native handle.</summary>
    public async ValueTask DisposeAsync()
    {
        var handle = Interlocked.Exchange(ref _handle, 0);
        if (handle != 0)
        {
            await _native.LogCloseAsync(checked((ulong)handle), CancellationToken.None).ConfigureAwait(false);
        }
    }

    internal static long ConsumeHandle(ref long handle) => Interlocked.Exchange(ref handle, 0);

    private ulong GetHandle()
    {
        var handle = Interlocked.Read(ref _handle);
        return handle != 0
            ? checked((ulong)handle)
            : throw new ObjectDisposedException(nameof(LogStream));
    }
}
