using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsandbox;

public sealed class VolumeService
{
    private readonly NativeApi _native;

    internal VolumeService(NativeApi native) => _native = native;

    public Task<VolumeInfo> CreateAsync(
        string name,
        VolumeCreateOptions? options = null,
        CancellationToken cancellationToken = default
    ) =>
        _native.CreateVolumeAsync(
            Required(name),
            (options ?? new VolumeCreateOptions()).ToJson(),
            cancellationToken
        );

    public Task<VolumeInfo> GetAsync(string name, CancellationToken cancellationToken = default) =>
        _native.GetVolumeAsync(Required(name), cancellationToken);

    public Task<IReadOnlyList<VolumeInfo>> ListAsync(
        CancellationToken cancellationToken = default
    ) => _native.ListVolumesAsync(cancellationToken);

    public Task RemoveAsync(string name, CancellationToken cancellationToken = default) =>
        _native.RemoveVolumeAsync(Required(name), cancellationToken);

    private static string Required(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value;
    }
}

public enum VolumeKind
{
    Dir,
    Disk,
}

public sealed class VolumeCreateOptions
{
    public uint QuotaMiB { get; init; }
    public VolumeKind? Kind { get; init; }
    public uint SizeMiB { get; init; }
    public IReadOnlyDictionary<string, string>? Labels { get; init; }

    internal string ToJson() =>
        JsonSerializer.Serialize(
            new Payload
            {
                QuotaMiB = QuotaMiB,
                Kind = Kind,
                SizeMiB = SizeMiB,
                Labels = Labels,
            },
            JsonDefaults.Options
        );

    private sealed class Payload
    {
        [JsonPropertyName("quota_mib")]
        public uint QuotaMiB { get; init; }

        [JsonPropertyName("kind")]
        public VolumeKind? Kind { get; init; }

        [JsonPropertyName("size_mib")]
        public uint SizeMiB { get; init; }

        [JsonPropertyName("labels")]
        public IReadOnlyDictionary<string, string>? Labels { get; init; }
    }
}

public sealed record VolumeInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("kind")] VolumeKind Kind,
    [property: JsonPropertyName("quota_mib")] uint? QuotaMiB,
    [property: JsonPropertyName("used_bytes")] ulong UsedBytes,
    [property: JsonPropertyName("capacity_bytes")] ulong? CapacityBytes,
    [property: JsonPropertyName("disk_format")] string? DiskFormat,
    [property: JsonPropertyName("disk_fstype")] string? DiskFilesystem,
    [property: JsonPropertyName("labels")] IReadOnlyDictionary<string, string> Labels,
    [property: JsonPropertyName("created_at_unix")] long? CreatedAtUnix
)
{
    public DateTimeOffset? CreatedAt =>
        CreatedAtUnix is { } value ? DateTimeOffset.FromUnixTimeSeconds(value) : null;
}
