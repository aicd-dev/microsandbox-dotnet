using System.Text;
using System.Text.Json.Serialization;
using System.Runtime.ExceptionServices;

namespace Microsandbox;

/// <summary>Filesystem operations for a running sandbox.</summary>
public sealed class SandboxFilesystem
{
    private readonly Sandbox _sandbox;
    private readonly NativeApi _native;

    internal SandboxFilesystem(Sandbox sandbox, NativeApi native)
    {
        _sandbox = sandbox;
        _native = native;
    }

    public async Task<byte[]> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        path = Required(path);
        try
        {
            return await _native.FsReadAsync(_sandbox.GetHandle(), path, cancellationToken).ConfigureAwait(false);
        }
        catch (MicrosandboxException exception) when (exception.Kind == "buffer_too_small")
        {
            SandboxFileReadStream stream;
            try
            {
                stream = await ReadStreamAsync(path, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
                throw;
            }

            await using (stream.ConfigureAwait(false))
            {
                using var output = new MemoryStream();
                await stream.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                return output.ToArray();
            }
        }
    }

    public async Task<string> ReadStringAsync(string path, CancellationToken cancellationToken = default) =>
        Encoding.UTF8.GetString(await ReadAsync(path, cancellationToken).ConfigureAwait(false));

    public Task WriteAsync(string path, byte[] data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        return _native.FsWriteAsync(_sandbox.GetHandle(), Required(path), data, cancellationToken);
    }

    public Task WriteStringAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        return WriteAsync(path, Encoding.UTF8.GetBytes(content), cancellationToken);
    }

    public Task<IReadOnlyList<FilesystemEntry>> ListAsync(string path, CancellationToken cancellationToken = default) =>
        _native.FsListAsync(_sandbox.GetHandle(), Required(path), cancellationToken);

    public Task<FilesystemStat> StatAsync(string path, CancellationToken cancellationToken = default) =>
        _native.FsStatAsync(_sandbox.GetHandle(), Required(path), cancellationToken);

    public Task CopyFromHostAsync(string hostPath, string guestPath, CancellationToken cancellationToken = default) =>
        _native.FsCopyFromHostAsync(_sandbox.GetHandle(), Required(hostPath), Required(guestPath), cancellationToken);

    public Task CopyToHostAsync(string guestPath, string hostPath, CancellationToken cancellationToken = default) =>
        _native.FsCopyToHostAsync(_sandbox.GetHandle(), Required(guestPath), Required(hostPath), cancellationToken);

    public Task MkdirAsync(string path, CancellationToken cancellationToken = default) =>
        _native.FsMkdirAsync(_sandbox.GetHandle(), Required(path), cancellationToken);

    public Task RemoveAsync(string path, CancellationToken cancellationToken = default) =>
        _native.FsRemoveAsync(_sandbox.GetHandle(), Required(path), cancellationToken);

    public Task RemoveDirAsync(string path, CancellationToken cancellationToken = default) =>
        _native.FsRemoveDirAsync(_sandbox.GetHandle(), Required(path), cancellationToken);

    public Task CopyAsync(string source, string destination, CancellationToken cancellationToken = default) =>
        _native.FsCopyAsync(_sandbox.GetHandle(), Required(source), Required(destination), cancellationToken);

    public Task RenameAsync(string source, string destination, CancellationToken cancellationToken = default) =>
        _native.FsRenameAsync(_sandbox.GetHandle(), Required(source), Required(destination), cancellationToken);

    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default) =>
        _native.FsExistsAsync(_sandbox.GetHandle(), Required(path), cancellationToken);

    /// <summary>Opens a readable stream for a guest file.</summary>
    public async Task<SandboxFileReadStream> ReadStreamAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var handle = await _native.FsReadStreamAsync(
            _sandbox.GetHandle(),
            Required(path),
            cancellationToken).ConfigureAwait(false);
        return new SandboxFileReadStream(_native, handle);
    }

    /// <summary>Opens a writable stream that must be completed or disposed to commit EOF.</summary>
    public async Task<SandboxFileWriteStream> WriteStreamAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var handle = await _native.FsWriteStreamAsync(
            _sandbox.GetHandle(),
            Required(path),
            cancellationToken).ConfigureAwait(false);
        return new SandboxFileWriteStream(_native, handle);
    }

    private static string Required(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value;
    }
}

/// <summary>A non-seekable readable stream backed by the guest filesystem streaming ABI.</summary>
public sealed class SandboxFileReadStream : Stream
{
    private readonly NativeApi _native;
    private readonly SemaphoreSlim _readLock = new(1, 1);
    private byte[]? _chunk;
    private int _chunkOffset;
    private bool _eof;
    private long _handle;

    internal SandboxFileReadStream(NativeApi native, ulong handle)
    {
        _native = native;
        _handle = checked((long)handle);
    }

    /// <inheritdoc />
    public override bool CanRead => Volatile.Read(ref _handle) != 0;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _handle) == 0, this);
        if (buffer.Length == 0 || _eof)
        {
            return 0;
        }

        await _readLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            while (_chunk is null || _chunkOffset == _chunk.Length)
            {
                _chunk = await _native.FsReadStreamReceiveAsync(GetHandle(), cancellationToken).ConfigureAwait(false);
                _chunkOffset = 0;
                if (_chunk is null)
                {
                    _eof = true;
                    return 0;
                }
            }

            var count = Math.Min(buffer.Length, _chunk.Length - _chunkOffset);
            _chunk.AsMemory(_chunkOffset, count).CopyTo(buffer);
            _chunkOffset += count;
            return count;
        }
        finally
        {
            _readLock.Release();
        }
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        var handle = Interlocked.Exchange(ref _handle, 0);
        if (handle != 0)
        {
            _native.FsReadStreamCloseAsync(checked((ulong)handle), CancellationToken.None).GetAwaiter().GetResult();
        }

        if (disposing)
        {
            _readLock.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    public override ValueTask DisposeAsync()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private ulong GetHandle()
    {
        var handle = Interlocked.Read(ref _handle);
        return handle != 0
            ? checked((ulong)handle)
            : throw new ObjectDisposedException(nameof(SandboxFileReadStream));
    }
}

/// <summary>A non-seekable writable stream backed by the guest filesystem streaming ABI.</summary>
public sealed class SandboxFileWriteStream : Stream
{
    private readonly NativeApi _native;
    private readonly ConsumingCloseState _state;

    internal SandboxFileWriteStream(NativeApi native, ulong handle)
    {
        _native = native;
        _state = new ConsumingCloseState(handle);
    }

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => _state.IsOpen;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) =>
        WriteAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var task = _native.FsWriteStreamWriteAsync(GetHandle(), buffer, cancellationToken);
        return new ValueTask(task);
    }

    /// <summary>Sends EOF and waits for the guest to confirm the completed write.</summary>
    public Task CompleteAsync(CancellationToken cancellationToken = default) =>
        _state.CloseAsync(_native.FsWriteStreamCloseAsync, cancellationToken);

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        CompleteAsync(CancellationToken.None).GetAwaiter().GetResult();
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await CompleteAsync(CancellationToken.None).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private ulong GetHandle() => _state.GetHandle(nameof(SandboxFileWriteStream));
}

public enum FilesystemEntryKind
{
    File,
    Dir,
    Directory,
    Symlink,
    Other,
}

public sealed record FilesystemEntry(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("kind")] FilesystemEntryKind Kind,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("mode")] uint Mode);

public sealed record FilesystemStat(
    [property: JsonPropertyName("kind")] FilesystemEntryKind Kind,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("mode")] uint Mode,
    [property: JsonPropertyName("readonly")] bool IsReadOnly,
    [property: JsonPropertyName("modified_unix")] long? ModifiedUnix)
{
    public DateTimeOffset? ModifiedAt => ModifiedUnix is { } value ? DateTimeOffset.FromUnixTimeSeconds(value) : null;
    public bool IsDirectory => Kind is FilesystemEntryKind.Dir or FilesystemEntryKind.Directory;
}
