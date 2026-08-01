namespace Microsandbox;

/// <summary>Provides metadata and name-addressed operations for a persisted sandbox.</summary>
public sealed class SandboxHandle
{
    private static readonly TimeSpan DefaultStopTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DefaultKillTimeout = TimeSpan.FromSeconds(5);
    private readonly NativeApi _native;

    internal SandboxHandle(NativeApi native, SandboxMetadata metadata)
    {
        _native = native;
        Metadata = metadata;
    }

    /// <summary>Gets the latest metadata captured for this handle.</summary>
    public SandboxMetadata Metadata { get; }
    /// <summary>Gets the sandbox name.</summary>
    public string Name => Metadata.Name;
    /// <summary>Gets the last-known lifecycle status.</summary>
    public SandboxStatus Status => Metadata.Status;

    /// <summary>Fetches current metadata for this sandbox.</summary>
    public Task<SandboxHandle> RefreshAsync(CancellationToken cancellationToken = default) =>
        _native.LookupAsync(Name, cancellationToken);

    /// <summary>Connects to an already-running sandbox.</summary>
    public Task<Sandbox> ConnectAsync(CancellationToken cancellationToken = default) =>
        _native.ConnectAsync(Name, cancellationToken);

    /// <summary>Starts the sandbox and returns a lifecycle-owning handle.</summary>
    public Task<Sandbox> StartAsync(CancellationToken cancellationToken = default) =>
        _native.StartAsync(Name, false, cancellationToken);

    /// <summary>Starts the sandbox in detached mode.</summary>
    public Task<Sandbox> StartDetachedAsync(CancellationToken cancellationToken = default) =>
        _native.StartAsync(Name, true, cancellationToken);

    /// <summary>Gracefully stops the sandbox by name.</summary>
    public Task StopAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
        _native.StopByNameAsync(Name, StopTimeoutMilliseconds(timeout), cancellationToken);

    /// <summary>Requests graceful shutdown by name and returns after the request is sent.</summary>
    public Task RequestStopAsync(CancellationToken cancellationToken = default) =>
        _native.RequestStopByNameAsync(Name, cancellationToken);

    /// <summary>Force-kills the sandbox by name.</summary>
    public Task KillAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
        _native.KillByNameAsync(Name, KillTimeoutMilliseconds(timeout), cancellationToken);

    /// <summary>Requests immediate termination by name and returns after the request is sent.</summary>
    public Task RequestKillAsync(CancellationToken cancellationToken = default) =>
        _native.RequestKillByNameAsync(Name, cancellationToken);

    /// <summary>Requests graceful drain by name and returns after the request is sent.</summary>
    public Task RequestDrainAsync(CancellationToken cancellationToken = default) =>
        _native.RequestDrainByNameAsync(Name, cancellationToken);

    /// <summary>Waits until the sandbox reaches a terminal state.</summary>
    public Task<SandboxStopResult> WaitUntilStoppedAsync(CancellationToken cancellationToken = default) =>
        _native.WaitUntilStoppedByNameAsync(Name, cancellationToken);

    /// <summary>Checks agent reachability without refreshing idle activity.</summary>
    public Task<SandboxPingResult> PingAsync(CancellationToken cancellationToken = default) =>
        _native.PingByNameAsync(Name, cancellationToken);

    /// <summary>Refreshes the sandbox idle-activity timer.</summary>
    public Task<SandboxTouchResult> TouchAsync(CancellationToken cancellationToken = default) =>
        _native.TouchByNameAsync(Name, cancellationToken);

    /// <summary>Plans or applies a modification by persisted sandbox name.</summary>
    public async Task<SandboxModificationPlan> ModifyAsync(
        SandboxModificationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var json = await _native.ModifyByNameAsync(Name, options.ToJson(), cancellationToken).ConfigureAwait(false);
        return SandboxModificationPlan.Parse(json);
    }

    /// <summary>Reads persisted sandbox logs into memory without requiring a live handle.</summary>
    public Task<IReadOnlyList<LogEntry>> LogsAsync(
        LogOptions? options = null,
        CancellationToken cancellationToken = default) =>
        _native.LogsByNameAsync(Name, (options ?? new LogOptions()).ToJson(), cancellationToken);

    /// <summary>Starts a persisted log stream without requiring a live handle.</summary>
    public async Task<LogStream> LogStreamAsync(
        LogStreamOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var handle = await _native.LogStreamByNameAsync(
            Name,
            (options ?? new LogStreamOptions()).ToJson(),
            cancellationToken).ConfigureAwait(false);
        return new LogStream(_native, handle);
    }

    /// <summary>Gets a point-in-time metrics snapshot without opening a live sandbox handle.</summary>
    public Task<SandboxMetrics> MetricsAsync(CancellationToken cancellationToken = default) =>
        _native.MetricsByNameAsync(Name, cancellationToken);

    /// <summary>Creates a named snapshot without opening a live sandbox handle.</summary>
    public Task<SnapshotArtifact> SnapshotAsync(
        string snapshotName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotName);
        return _native.SnapshotByNameAsync(Name, snapshotName, cancellationToken);
    }

    /// <summary>Removes the stopped sandbox's persisted state.</summary>
    public Task RemoveAsync(CancellationToken cancellationToken = default) =>
        _native.RemoveAsync(Name, cancellationToken);

    internal static ulong StopTimeoutMilliseconds(TimeSpan? timeout) =>
        TimeoutMilliseconds(timeout, DefaultStopTimeout);

    internal static ulong KillTimeoutMilliseconds(TimeSpan? timeout) =>
        TimeoutMilliseconds(timeout, DefaultKillTimeout);

    private static ulong TimeoutMilliseconds(TimeSpan? timeout, TimeSpan defaultTimeout)
    {
        var value = timeout ?? defaultTimeout;
        if (value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout cannot be negative.");
        }

        return checked((ulong)Math.Ceiling(value.TotalMilliseconds));
    }
}

/// <summary>Contains persisted metadata for a sandbox.</summary>
public sealed record SandboxMetadata(
    string Name,
    SandboxStatus Status,
    string ConfigJson,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);
