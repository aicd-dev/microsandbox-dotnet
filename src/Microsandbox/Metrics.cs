using System.Text.Json.Serialization;

namespace Microsandbox;

/// <summary>A point-in-time sandbox resource usage snapshot.</summary>
public sealed record SandboxMetrics(
    [property: JsonPropertyName("cpu_percent")] double CpuPercent,
    [property: JsonPropertyName("vcpu_time_ns")] ulong VcpuTimeNanoseconds,
    [property: JsonPropertyName("memory_bytes")] ulong MemoryBytes,
    [property: JsonPropertyName("memory_available_bytes")] ulong? MemoryAvailableBytes,
    [property: JsonPropertyName("memory_host_resident_bytes")] ulong? MemoryHostResidentBytes,
    [property: JsonPropertyName("memory_limit_bytes")] ulong MemoryLimitBytes,
    [property: JsonPropertyName("disk_read_bytes")] ulong DiskReadBytes,
    [property: JsonPropertyName("disk_write_bytes")] ulong DiskWriteBytes,
    [property: JsonPropertyName("net_rx_bytes")] ulong NetworkReceiveBytes,
    [property: JsonPropertyName("net_tx_bytes")] ulong NetworkTransmitBytes,
    [property: JsonPropertyName("upper_used_bytes")] ulong? UpperUsedBytes,
    [property: JsonPropertyName("upper_free_bytes")] ulong? UpperFreeBytes,
    [property: JsonPropertyName("upper_host_allocated_bytes")] ulong? UpperHostAllocatedBytes,
    [property: JsonPropertyName("uptime_secs")] ulong UptimeSeconds)
{
    /// <summary>Gets the sandbox uptime.</summary>
    public TimeSpan Uptime => TimeSpan.FromSeconds(UptimeSeconds);
}

/// <summary>An owned live subscription to sandbox metrics.</summary>
public sealed class MetricsStream : IAsyncDisposable
{
    private readonly NativeApi _native;
    private long _handle;

    internal MetricsStream(NativeApi native, ulong handle)
    {
        _native = native;
        _handle = checked((long)handle);
    }

    /// <summary>Receives the next snapshot, or <see langword="null"/> after the sandbox exits.</summary>
    public Task<SandboxMetrics?> ReceiveAsync(CancellationToken cancellationToken = default) =>
        _native.MetricsReceiveAsync(GetHandle(), cancellationToken);

    /// <summary>Stops the stream and consumes its native handle.</summary>
    public async ValueTask DisposeAsync()
    {
        var handle = Interlocked.Exchange(ref _handle, 0);
        if (handle != 0)
        {
            await _native.MetricsCloseAsync(checked((ulong)handle), CancellationToken.None).ConfigureAwait(false);
        }
    }

    internal static long ConsumeHandle(ref long handle) => Interlocked.Exchange(ref handle, 0);

    private ulong GetHandle()
    {
        var handle = Interlocked.Read(ref _handle);
        return handle != 0
            ? checked((ulong)handle)
            : throw new ObjectDisposedException(nameof(MetricsStream));
    }
}
