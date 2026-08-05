using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsandbox;

/// <summary>Provides native in-process SSH operations for a live sandbox.</summary>
public sealed class SandboxSsh
{
    private readonly Sandbox _sandbox;
    private readonly NativeApi _native;

    internal SandboxSsh(Sandbox sandbox, NativeApi native)
    {
        _sandbox = sandbox;
        _native = native;
    }

    /// <summary>Opens an SSH client connected to the sandbox.</summary>
    public async Task<SshClient> OpenClientAsync(
        SshClientOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        var handle = await _native
            .SshConnectAsync(
                _sandbox.GetHandle(),
                (options ?? new SshClientOptions()).ToJson(),
                cancellationToken
            )
            .ConfigureAwait(false);
        return new SshClient(_native, handle);
    }

    /// <summary>Prepares a reusable SSH server endpoint for the sandbox.</summary>
    public async Task<SshServer> PrepareServerAsync(
        SshServerOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        var handle = await _native
            .SshServerAsync(
                _sandbox.GetHandle(),
                (options ?? new SshServerOptions()).ToJson(),
                cancellationToken
            )
            .ConfigureAwait(false);
        return new SshServer(_native, handle);
    }
}

/// <summary>Configures an in-process SSH client connection.</summary>
public sealed record SshClientOptions
{
    /// <summary>Gets the guest SSH user.</summary>
    [JsonPropertyName("user")]
    public string? User { get; init; }

    /// <summary>Gets the terminal name used by interactive sessions.</summary>
    [JsonPropertyName("term")]
    public string? Terminal { get; init; }

    /// <summary>Gets whether the internal SSH server enables SFTP.</summary>
    [JsonPropertyName("sftp")]
    public bool? EnableSftp { get; init; }

    internal string ToJson() => JsonSerializer.Serialize(this, JsonDefaults.Options);
}

/// <summary>Configures an SSH exec request.</summary>
public sealed record SshExecOptions
{
    /// <summary>Gets whether the request allocates a pseudo-terminal.</summary>
    [JsonPropertyName("tty")]
    public bool? Tty { get; init; }

    internal string ToJson() => JsonSerializer.Serialize(this, JsonDefaults.Options);
}

/// <summary>Configures a console-attached interactive SSH shell.</summary>
public sealed record SshAttachOptions
{
    /// <summary>Gets the terminal name.</summary>
    [JsonPropertyName("term")]
    public string? Terminal { get; init; }

    /// <summary>Gets the detach key sequence.</summary>
    [JsonPropertyName("detach_keys")]
    public string? DetachKeys { get; init; }

    internal string ToJson() => JsonSerializer.Serialize(this, JsonDefaults.Options);
}

/// <summary>Configures a prepared SSH server endpoint.</summary>
public sealed record SshServerOptions
{
    /// <summary>Gets the host private key path.</summary>
    [JsonPropertyName("host_key_path")]
    public string? HostKeyPath { get; init; }

    /// <summary>Gets the authorized-keys path.</summary>
    [JsonPropertyName("authorized_keys_path")]
    public string? AuthorizedKeysPath { get; init; }

    /// <summary>Gets the guest user used for exec requests.</summary>
    [JsonPropertyName("user")]
    public string? User { get; init; }

    /// <summary>Gets whether the endpoint enables SFTP.</summary>
    [JsonPropertyName("sftp")]
    public bool? EnableSftp { get; init; }

    internal string ToJson() => JsonSerializer.Serialize(this, JsonDefaults.Options);
}

/// <summary>Contains collected output from an SSH exec request.</summary>
public sealed record SshOutput(int Status, byte[] StandardOutput, byte[] StandardError)
{
    /// <summary>Gets whether the command exited successfully.</summary>
    public bool IsSuccess => Status == 0;

    /// <summary>Gets standard output decoded as UTF-8.</summary>
    public string StandardOutputText => Encoding.UTF8.GetString(StandardOutput);

    /// <summary>Gets standard error decoded as UTF-8.</summary>
    public string StandardErrorText => Encoding.UTF8.GetString(StandardError);
}

/// <summary>Owns a native in-process SSH client handle.</summary>
public sealed class SshClient : IAsyncDisposable
{
    private readonly NativeApi _native;
    private readonly ConsumingCloseState _state;

    internal SshClient(NativeApi native, ulong handle)
    {
        _native = native;
        _state = new ConsumingCloseState(handle);
    }

    /// <summary>Runs a command and collects stdout, stderr, and exit status.</summary>
    public Task<SshOutput> ExecuteAsync(
        string command,
        SshExecOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        return _native.SshClientExecAsync(
            GetHandle(),
            command,
            (options ?? new SshExecOptions()).ToJson(),
            cancellationToken
        );
    }

    /// <summary>Bridges the process console to an interactive SSH shell until it exits or detaches.</summary>
    public Task<int> AttachConsoleAsync(
        SshAttachOptions? options = null,
        CancellationToken cancellationToken = default
    ) =>
        _native.SshClientAttachAsync(
            GetHandle(),
            (options ?? new SshAttachOptions()).ToJson(),
            cancellationToken
        );

    /// <summary>Opens an SFTP session over this SSH connection.</summary>
    public async Task<SftpClient> OpenSftpAsync(CancellationToken cancellationToken = default)
    {
        var handle = await _native
            .SshClientSftpAsync(GetHandle(), cancellationToken)
            .ConfigureAwait(false);
        return new SftpClient(_native, handle);
    }

    /// <summary>Closes and consumes the native SSH client handle.</summary>
    public Task CloseAsync(CancellationToken cancellationToken = default) =>
        _state.CloseAsync(_native.SshClientCloseAsync, cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync() =>
        await CloseAsync(CancellationToken.None).ConfigureAwait(false);

    private ulong GetHandle() => _state.GetHandle(nameof(SshClient));

    internal static long ConsumeHandle(ref long handle) => Interlocked.Exchange(ref handle, 0);
}

/// <summary>Owns a native SFTP session handle.</summary>
public sealed class SftpClient : IAsyncDisposable
{
    private readonly NativeApi _native;
    private readonly ConsumingCloseState _state;

    internal SftpClient(NativeApi native, ulong handle)
    {
        _native = native;
        _state = new ConsumingCloseState(handle);
    }

    /// <summary>Reads a file into memory.</summary>
    public Task<byte[]> ReadAsync(string path, CancellationToken cancellationToken = default) =>
        _native.SftpReadAsync(GetHandle(), Required(path), cancellationToken);

    /// <summary>Reads a UTF-8 file into a string.</summary>
    public async Task<string> ReadStringAsync(
        string path,
        CancellationToken cancellationToken = default
    ) => Encoding.UTF8.GetString(await ReadAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>Creates or truncates a file with the supplied bytes.</summary>
    public Task WriteAsync(
        string path,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default
    ) => _native.SftpWriteAsync(GetHandle(), Required(path), data, cancellationToken);

    /// <summary>Creates or truncates a UTF-8 file.</summary>
    public Task WriteStringAsync(
        string path,
        string content,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(content);
        return WriteAsync(path, Encoding.UTF8.GetBytes(content), cancellationToken);
    }

    /// <summary>Creates a directory.</summary>
    public Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default) =>
        _native.SftpMkdirAsync(GetHandle(), Required(path), cancellationToken);

    /// <summary>Removes a file.</summary>
    public Task RemoveFileAsync(string path, CancellationToken cancellationToken = default) =>
        _native.SftpRemoveFileAsync(GetHandle(), Required(path), cancellationToken);

    /// <summary>Removes an empty directory.</summary>
    public Task RemoveDirectoryAsync(string path, CancellationToken cancellationToken = default) =>
        _native.SftpRemoveDirectoryAsync(GetHandle(), Required(path), cancellationToken);

    /// <summary>Renames a file or directory.</summary>
    public Task RenameAsync(
        string oldPath,
        string newPath,
        CancellationToken cancellationToken = default
    ) =>
        _native.SftpRenameAsync(
            GetHandle(),
            Required(oldPath),
            Required(newPath),
            cancellationToken
        );

    /// <summary>Resolves a path to its canonical absolute form.</summary>
    public Task<string> GetRealPathAsync(
        string path,
        CancellationToken cancellationToken = default
    ) => _native.SftpRealPathAsync(GetHandle(), Required(path), cancellationToken);

    /// <summary>Reads a symbolic-link target.</summary>
    public Task<string> ReadLinkAsync(string path, CancellationToken cancellationToken = default) =>
        _native.SftpReadLinkAsync(GetHandle(), Required(path), cancellationToken);

    /// <summary>Creates a symbolic link.</summary>
    public Task CreateSymbolicLinkAsync(
        string target,
        string linkPath,
        CancellationToken cancellationToken = default
    ) =>
        _native.SftpSymlinkAsync(
            GetHandle(),
            Required(target),
            Required(linkPath),
            cancellationToken
        );

    /// <summary>Closes and consumes the native SFTP handle.</summary>
    public Task CloseAsync(CancellationToken cancellationToken = default) =>
        _state.CloseAsync(_native.SftpCloseAsync, cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync() =>
        await CloseAsync(CancellationToken.None).ConfigureAwait(false);

    private ulong GetHandle() => _state.GetHandle(nameof(SftpClient));

    internal static long ConsumeHandle(ref long handle) => Interlocked.Exchange(ref handle, 0);

    private static string Required(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value;
    }
}

/// <summary>Owns a prepared native SSH server endpoint.</summary>
public sealed class SshServer : IAsyncDisposable
{
    private readonly NativeApi _native;
    private readonly ConsumingCloseState _state;

    internal SshServer(NativeApi native, ulong handle)
    {
        _native = native;
        _state = new ConsumingCloseState(handle);
    }

    /// <summary>Compatibility alias for <see cref="ServeStdioAsync"/>.</summary>
    public Task ServeConnectionAsync(CancellationToken cancellationToken = default) =>
        _native.SshServerServeConnectionAsync(GetHandle(), cancellationToken);

    /// <summary>Serves one SSH transport over this process's standard input and output.</summary>
    public Task ServeStdioAsync(CancellationToken cancellationToken = default) =>
        _native.SshServerServeStandardIoAsync(GetHandle(), cancellationToken);

    /// <summary>Compatibility alias for <see cref="ServeStdioAsync"/>.</summary>
    public Task ServeConsoleConnectionAsync(CancellationToken cancellationToken = default) =>
        ServeConnectionAsync(cancellationToken);

    /// <summary>Closes and consumes the native SSH server handle.</summary>
    public Task CloseAsync(CancellationToken cancellationToken = default) =>
        _state.CloseAsync(_native.SshServerCloseAsync, cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync() =>
        await CloseAsync(CancellationToken.None).ConfigureAwait(false);

    private ulong GetHandle() => _state.GetHandle(nameof(SshServer));

    internal static long ConsumeHandle(ref long handle) => Interlocked.Exchange(ref handle, 0);
}
