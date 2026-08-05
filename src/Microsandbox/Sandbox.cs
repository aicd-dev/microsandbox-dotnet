using System.Text.Json;

namespace Microsandbox;

/// <summary>Represents a live, native microsandbox handle.</summary>
public sealed class Sandbox : IAsyncDisposable
{
    private static readonly TimeSpan DefaultStopTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DefaultKillTimeout = TimeSpan.FromSeconds(5);
    private readonly NativeApi _native;
    private readonly ConsumingCloseState _state;

    internal Sandbox(NativeApi native, string name, ulong handle)
    {
        _native = native;
        Name = name;
        _state = new ConsumingCloseState(handle);
        Filesystem = new SandboxFilesystem(this, native);
        Ssh = new SandboxSsh(this, native);
    }

    /// <summary>Gets the sandbox name.</summary>
    public string Name { get; }

    /// <summary>Gets non-streaming guest filesystem operations.</summary>
    public SandboxFilesystem Filesystem { get; }

    /// <summary>Gets native SSH and SFTP operations.</summary>
    public SandboxSsh Ssh { get; }

    /// <summary>Runs a command and collects its output.</summary>
    public Task<ExecResult> ExecuteAsync(
        string command,
        ExecOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        return _native.ExecAsync(
            GetHandle(),
            command,
            (options ?? new ExecOptions()).ToJson(),
            cancellationToken
        );
    }

    /// <summary>Starts a command and returns a live streaming exec handle.</summary>
    public async Task<ExecHandle> ExecuteStreamingAsync(
        string command,
        ExecOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        options ??= new ExecOptions();
        var handle = await _native
            .ExecStreamAsync(GetHandle(), command, options.ToJson(), cancellationToken)
            .ConfigureAwait(false);
        return new ExecHandle(_native, handle, options.StdinPipe);
    }

    /// <summary>Reads persisted sandbox logs into memory.</summary>
    public Task<IReadOnlyList<LogEntry>> LogsAsync(
        LogOptions? options = null,
        CancellationToken cancellationToken = default
    ) => _native.LogsAsync(GetHandle(), (options ?? new LogOptions()).ToJson(), cancellationToken);

    /// <summary>Starts a persisted log stream, optionally following new entries.</summary>
    public async Task<LogStream> LogStreamAsync(
        LogStreamOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        var handle = await _native
            .LogStreamAsync(
                GetHandle(),
                (options ?? new LogStreamOptions()).ToJson(),
                cancellationToken
            )
            .ConfigureAwait(false);
        return new LogStream(_native, handle);
    }

    /// <summary>Gets a point-in-time resource usage snapshot.</summary>
    public Task<SandboxMetrics> MetricsAsync(CancellationToken cancellationToken = default) =>
        _native.MetricsAsync(GetHandle(), cancellationToken);

    /// <summary>Starts a metrics stream at the requested polling interval.</summary>
    public async Task<MetricsStream> MetricsStreamAsync(
        TimeSpan interval,
        CancellationToken cancellationToken = default
    )
    {
        if (interval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval cannot be negative.");
        }

        var milliseconds =
            interval > TimeSpan.Zero ? checked((ulong)Math.Ceiling(interval.TotalMilliseconds)) : 0;
        var handle = await _native
            .MetricsStreamAsync(GetHandle(), milliseconds, cancellationToken)
            .ConfigureAwait(false);
        return new MetricsStream(_native, handle);
    }

    /// <summary>Runs a shell command through <c>/bin/sh -c</c>.</summary>
    public Task<ExecResult> ShellAsync(
        string command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        return ExecuteAsync(
            "/bin/sh",
            new ExecOptions { Arguments = ["-c", command] },
            cancellationToken
        );
    }

    /// <summary>Starts a command through <c>/bin/sh -c</c> and returns a live streaming exec handle.</summary>
    public Task<ExecHandle> ShellStreamingAsync(
        string command,
        ExecOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        options = (options ?? new ExecOptions()) with { Arguments = ["-c", command] };
        return ExecuteStreamingAsync("/bin/sh", options, cancellationToken);
    }

    /// <summary>Gracefully stops the sandbox and waits until it is stopped.</summary>
    public Task StopAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var timeoutMilliseconds = StopTimeoutMilliseconds(timeout);
        return _native.StopAsync(GetHandle(), timeoutMilliseconds, cancellationToken);
    }

    /// <summary>Requests graceful shutdown and returns after the request is sent.</summary>
    public Task RequestStopAsync(CancellationToken cancellationToken = default) =>
        _native.RequestStopAsync(GetHandle(), cancellationToken);

    /// <summary>Stops the sandbox and returns the VM process exit code when available.</summary>
    public Task<int?> StopAndWaitAsync(CancellationToken cancellationToken = default) =>
        _native.StopAndWaitAsync(GetHandle(), cancellationToken);

    /// <summary>Force-kills the sandbox and waits for stopped-state observation.</summary>
    public Task KillAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var timeoutMilliseconds = KillTimeoutMilliseconds(timeout);
        return _native.KillAsync(GetHandle(), timeoutMilliseconds, cancellationToken);
    }

    /// <summary>Requests immediate termination and returns after the request is sent.</summary>
    public Task RequestKillAsync(CancellationToken cancellationToken = default) =>
        _native.RequestKillAsync(GetHandle(), cancellationToken);

    /// <summary>Triggers graceful drain and waits for native acknowledgement.</summary>
    public Task DrainAsync(CancellationToken cancellationToken = default) =>
        _native.DrainAsync(GetHandle(), cancellationToken);

    /// <summary>Requests graceful drain and returns after the request is sent.</summary>
    public Task RequestDrainAsync(CancellationToken cancellationToken = default) =>
        _native.RequestDrainAsync(GetHandle(), cancellationToken);

    /// <summary>Waits for the underlying sandbox process and returns its exit code when available.</summary>
    public Task<int?> WaitAsync(CancellationToken cancellationToken = default) =>
        _native.WaitAsync(GetHandle(), cancellationToken);

    /// <summary>Waits until persisted state reports a terminal sandbox status.</summary>
    public Task<SandboxStopResult> WaitUntilStoppedAsync(
        CancellationToken cancellationToken = default
    ) => _native.WaitUntilStoppedAsync(GetHandle(), cancellationToken);

    /// <summary>Checks agent reachability without refreshing idle activity.</summary>
    public Task<SandboxPingResult> PingAsync(CancellationToken cancellationToken = default) =>
        _native.PingAsync(GetHandle(), cancellationToken);

    /// <summary>Refreshes the sandbox idle-activity timer.</summary>
    public Task<SandboxTouchResult> TouchAsync(CancellationToken cancellationToken = default) =>
        _native.TouchAsync(GetHandle(), cancellationToken);

    /// <summary>Attaches the current terminal to a command running with a guest PTY.</summary>
    public Task<int> AttachAsync(
        string command,
        IReadOnlyList<string>? arguments = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        var options = JsonSerializer.Serialize(
            new { args = arguments ?? [] },
            JsonDefaults.Options
        );
        return _native.AttachAsync(GetHandle(), command, options, cancellationToken);
    }

    /// <summary>Attaches the current terminal to the sandbox's default shell.</summary>
    public Task<int> AttachShellAsync(CancellationToken cancellationToken = default) =>
        _native.AttachShellAsync(GetHandle(), cancellationToken);

    /// <summary>Opens a low-level raw agent client for this sandbox.</summary>
    public async Task<AgentClient> ConnectAgentAsync(CancellationToken cancellationToken = default)
    {
        var handle = await _native
            .OpenAgentSandboxAsync(Name, cancellationToken)
            .ConfigureAwait(false);
        return new AgentClient(_native, handle);
    }

    /// <summary>Plans or applies a modification through this live handle.</summary>
    public async Task<SandboxModificationPlan> ModifyAsync(
        SandboxModificationOptions options,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        var json = await _native
            .ModifyAsync(GetHandle(), options.ToJson(), cancellationToken)
            .ConfigureAwait(false);
        return SandboxModificationPlan.Parse(json);
    }

    /// <summary>Gets whether this handle owns the sandbox VM lifecycle.</summary>
    public bool OwnsLifecycle => _native.OwnsLifecycle(GetHandle());

    /// <summary>Consumes this handle without stopping its detached VM.</summary>
    public Task DetachAsync(CancellationToken cancellationToken = default) =>
        _state.CloseAsync(_native.DetachAsync, cancellationToken);

    /// <summary>Removes stopped persisted state and consumes this live handle.</summary>
    public Task RemovePersistedAsync(CancellationToken cancellationToken = default) =>
        _state.CloseAsync(_native.RemovePersistedAsync, cancellationToken);

    /// <summary>Releases the native handle. Lifecycle-owned sandboxes are stopped by the native runtime.</summary>
    public async ValueTask DisposeAsync() =>
        await _state.CloseAsync(_native.CloseAsync, CancellationToken.None).ConfigureAwait(false);

    internal ulong GetHandle() => _state.GetHandle(nameof(Sandbox));

    internal static long ConsumeHandle(ref long handle) => Interlocked.Exchange(ref handle, 0);

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
