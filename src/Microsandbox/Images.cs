using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsandbox;

public sealed class ImageService
{
    private readonly NativeApi _native;

    internal ImageService(NativeApi native) => _native = native;

    public Task<ImageInfo> GetAsync(
        string reference,
        CancellationToken cancellationToken = default
    ) => _native.GetImageAsync(Required(reference), cancellationToken);

    public Task<IReadOnlyList<ImageInfo>> ListAsync(
        CancellationToken cancellationToken = default
    ) => _native.ListImagesAsync(cancellationToken);

    public Task<ImageDetail> InspectAsync(
        string reference,
        CancellationToken cancellationToken = default
    ) => _native.InspectImageAsync(Required(reference), cancellationToken);

    public Task RemoveAsync(
        string reference,
        bool force = false,
        CancellationToken cancellationToken = default
    ) => _native.RemoveImageAsync(Required(reference), force, cancellationToken);

    public Task<ImagePruneReport> PruneAsync(CancellationToken cancellationToken = default) =>
        _native.PruneImagesAsync(cancellationToken);

    public Task<IReadOnlyList<ImageInfo>> LoadAsync(
        string inputPath,
        IReadOnlyList<string>? tags = null,
        CancellationToken cancellationToken = default
    ) =>
        _native.LoadImagesAsync(
            Required(inputPath),
            JsonSerializer.Serialize(tags ?? Array.Empty<string>(), JsonDefaults.Options),
            cancellationToken
        );

    public Task SaveAsync(
        IReadOnlyList<string> references,
        string outputPath,
        ImageArchiveFormat format = ImageArchiveFormat.Docker,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(references);
        return _native.SaveImagesAsync(
            JsonSerializer.Serialize(references, JsonDefaults.Options),
            Required(outputPath),
            format == ImageArchiveFormat.Oci ? "oci" : "docker",
            cancellationToken
        );
    }

    private static string Required(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value;
    }
}

public enum ImageArchiveFormat
{
    Docker,
    Oci,
}

public class ImageInfo
{
    [JsonPropertyName("reference")]
    public required string Reference { get; init; }

    [JsonPropertyName("manifest_digest")]
    public required string ManifestDigest { get; init; }

    [JsonPropertyName("architecture")]
    public required string Architecture { get; init; }

    [JsonPropertyName("os")]
    public required string OperatingSystem { get; init; }

    [JsonPropertyName("layer_count")]
    public uint LayerCount { get; init; }

    [JsonPropertyName("size_bytes")]
    public long? SizeBytes { get; init; }

    [JsonPropertyName("created_at_unix")]
    public long? CreatedAtUnix { get; init; }

    [JsonPropertyName("last_used_at_unix")]
    public long? LastUsedAtUnix { get; init; }

    public DateTimeOffset? CreatedAt =>
        CreatedAtUnix is { } value ? DateTimeOffset.FromUnixTimeSeconds(value) : null;
    public DateTimeOffset? LastUsedAt =>
        LastUsedAtUnix is { } value ? DateTimeOffset.FromUnixTimeSeconds(value) : null;
}

public sealed class ImageDetail : ImageInfo
{
    [JsonPropertyName("config")]
    public ImageConfig? Config { get; init; }

    [JsonPropertyName("layers")]
    public IReadOnlyList<ImageLayer> Layers { get; init; } = Array.Empty<ImageLayer>();
}

public sealed record ImageConfig(
    [property: JsonPropertyName("digest")] string Digest,
    [property: JsonPropertyName("env")] IReadOnlyList<string> Environment,
    [property: JsonPropertyName("cmd")] IReadOnlyList<string> Command,
    [property: JsonPropertyName("entrypoint")] IReadOnlyList<string> Entrypoint,
    [property: JsonPropertyName("working_dir")] string WorkingDirectory,
    [property: JsonPropertyName("user")] string User,
    [property: JsonPropertyName("labels")] IReadOnlyDictionary<string, string> Labels,
    [property: JsonPropertyName("stop_signal")] string StopSignal
);

public sealed record ImageLayer(
    [property: JsonPropertyName("diff_id")] string DiffId,
    [property: JsonPropertyName("blob_digest")] string BlobDigest,
    [property: JsonPropertyName("media_type")] string MediaType,
    [property: JsonPropertyName("compressed_size_bytes")] long? CompressedSizeBytes,
    [property: JsonPropertyName("erofs_size_bytes")] long? ErofsSizeBytes,
    [property: JsonPropertyName("position")] int Position
);

public sealed record ImagePruneReport(
    [property: JsonPropertyName("image_refs_removed")] uint ImageReferencesRemoved,
    [property: JsonPropertyName("manifests_removed")] uint ManifestsRemoved,
    [property: JsonPropertyName("layers_removed")] uint LayersRemoved,
    [property: JsonPropertyName("fsmeta_removed")] uint FilesystemMetadataRemoved,
    [property: JsonPropertyName("vmdk_removed")] uint VmdkRemoved,
    [property: JsonPropertyName("bytes_reclaimed")] ulong? BytesReclaimed
);
