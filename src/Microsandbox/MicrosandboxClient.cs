using System.Text;

namespace Microsandbox;

/// <summary>Loads and invokes the native microsandbox ABI.</summary>
public sealed class MicrosandboxClient
{
    private readonly NativeApi _native;

    private MicrosandboxClient(NativeApi native)
    {
        _native = native;
        Images = new ImageService(native);
        Volumes = new VolumeService(native);
        Snapshots = new SnapshotService(native);
    }

    /// <summary>Gets OCI image-cache operations.</summary>
    public ImageService Images { get; }

    /// <summary>Gets named persistent-volume operations.</summary>
    public VolumeService Volumes { get; }

    /// <summary>Gets snapshot artifact operations.</summary>
    public SnapshotService Snapshots { get; }

    /// <summary>Loads the native ABI from an explicit path, environment, package runtime asset, or system search path.</summary>
    public static MicrosandboxClient Load(string? nativeLibraryPath = null) => new(NativeApi.Load(nativeLibraryPath));

    /// <summary>Gets the microsandbox runtime version exposed by the loaded ABI.</summary>
    public string RuntimeVersion => _native.Version();

    /// <summary>Sets the resolved <c>msb</c> executable path used by the native runtime.</summary>
    public void SetMsbPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _native.SetMsbPath(path);
    }

    /// <summary>Creates and boots a sandbox.</summary>
    public async Task<Sandbox> CreateAsync(
        string name,
        SandboxOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ValidateName(name);
        var handle = await _native.CreateAsync(name, (options ?? new SandboxOptions()).ToJson(), cancellationToken)
            .ConfigureAwait(false);
        return new Sandbox(_native, name, handle);
    }

    /// <summary>Looks up persisted sandbox metadata without connecting.</summary>
    public Task<SandboxHandle> LookupAsync(string name, CancellationToken cancellationToken = default)
    {
        ValidateName(name);
        return _native.LookupAsync(name, cancellationToken);
    }

    /// <summary>Lists persisted sandboxes, optionally filtering by labels.</summary>
    public Task<IReadOnlyList<SandboxHandle>> ListAsync(
        SandboxFilter? filter = null,
        CancellationToken cancellationToken = default) =>
        _native.ListAsync((filter ?? new SandboxFilter()).ToJson(), cancellationToken);

    /// <summary>Connects to an already-running sandbox by name.</summary>
    public Task<Sandbox> ConnectAsync(string name, CancellationToken cancellationToken = default)
    {
        ValidateName(name);
        return _native.ConnectAsync(name, cancellationToken);
    }

    /// <summary>Starts a persisted sandbox by name.</summary>
    public Task<Sandbox> StartAsync(
        string name,
        bool detached = false,
        CancellationToken cancellationToken = default)
    {
        ValidateName(name);
        return _native.StartAsync(name, detached, cancellationToken);
    }

    /// <summary>Starts a persisted sandbox in detached mode.</summary>
    public Task<Sandbox> StartDetachedAsync(string name, CancellationToken cancellationToken = default)
    {
        ValidateName(name);
        return _native.StartAsync(name, true, cancellationToken);
    }

    /// <summary>Removes a stopped sandbox's persisted state by name.</summary>
    public Task RemoveAsync(string name, CancellationToken cancellationToken = default)
    {
        ValidateName(name);
        return _native.RemoveAsync(name, cancellationToken);
    }

    /// <summary>Gets point-in-time metrics for every running sandbox, keyed by sandbox name.</summary>
    public Task<IReadOnlyDictionary<string, SandboxMetrics>> AllMetricsAsync(
        CancellationToken cancellationToken = default) =>
        _native.AllMetricsAsync(cancellationToken);

    /// <summary>Connects a low-level raw agent client to a running sandbox by name.</summary>
    public async Task<AgentClient> ConnectAgentAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ValidateName(name);
        var handle = await _native.OpenAgentSandboxAsync(name, cancellationToken).ConfigureAwait(false);
        return new AgentClient(_native, handle);
    }

    /// <summary>Connects a low-level raw agent client to a relay socket path.</summary>
    public async Task<AgentClient> ConnectAgentPathAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var handle = await _native.OpenAgentPathAsync(path, cancellationToken).ConfigureAwait(false);
        return new AgentClient(_native, handle);
    }

    /// <summary>Resolves the host relay-socket path for a sandbox without connecting.</summary>
    public string GetAgentSocketPath(string name)
    {
        ValidateName(name);
        return _native.AgentSocketPath(name);
    }

    internal static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (Encoding.UTF8.GetByteCount(name) > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(name), "Sandbox names cannot exceed 128 UTF-8 bytes.");
        }
    }
}
