using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsandbox;

public sealed class SnapshotService
{
    private readonly NativeApi _native;

    internal SnapshotService(NativeApi native) => _native = native;

    public Task<SnapshotArtifact> CreateAsync(
        SnapshotCreateOptions options,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.SourceSandbox);
        return _native.CreateSnapshotAsync(
            options.SourceSandbox,
            options.ToJson(),
            cancellationToken
        );
    }

    public Task<SnapshotArtifact> OpenAsync(
        string pathOrName,
        CancellationToken cancellationToken = default
    ) => _native.OpenSnapshotAsync(Required(pathOrName), cancellationToken);

    public Task<SnapshotVerifyReport> VerifyAsync(
        string pathOrName,
        CancellationToken cancellationToken = default
    ) => _native.VerifySnapshotAsync(Required(pathOrName), cancellationToken);

    public Task<SnapshotInfo> GetAsync(
        string nameOrDigest,
        CancellationToken cancellationToken = default
    ) => _native.GetSnapshotAsync(Required(nameOrDigest), cancellationToken);

    public Task<IReadOnlyList<SnapshotInfo>> ListAsync(
        CancellationToken cancellationToken = default
    ) => _native.ListSnapshotsAsync(cancellationToken);

    public Task<IReadOnlyList<SnapshotArtifact>> ListDirectoryAsync(
        string directory,
        CancellationToken cancellationToken = default
    ) => _native.ListSnapshotDirectoryAsync(Required(directory), cancellationToken);

    public Task RemoveAsync(
        string pathOrName,
        bool force = false,
        CancellationToken cancellationToken = default
    ) => _native.RemoveSnapshotAsync(Required(pathOrName), force, cancellationToken);

    public Task<uint> ReindexAsync(
        string directory,
        CancellationToken cancellationToken = default
    ) => _native.ReindexSnapshotsAsync(Required(directory), cancellationToken);

    public Task ExportAsync(
        string nameOrPath,
        string outputPath,
        SnapshotExportOptions? options = null,
        CancellationToken cancellationToken = default
    ) =>
        _native.ExportSnapshotAsync(
            Required(nameOrPath),
            Required(outputPath),
            (options ?? new SnapshotExportOptions()).ToJson(),
            cancellationToken
        );

    public Task<SnapshotInfo> ImportAsync(
        string archive,
        string destination,
        CancellationToken cancellationToken = default
    ) => _native.ImportSnapshotAsync(Required(archive), Required(destination), cancellationToken);

    private static string Required(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value;
    }
}

public sealed class SnapshotCreateOptions
{
    public required string Name { get; init; }
    public required string SourceSandbox { get; init; }
    public string? DestinationDirectory { get; init; }
    public IReadOnlyDictionary<string, string>? Labels { get; init; }
    public bool Force { get; init; }
    public bool RecordIntegrity { get; init; }
    public bool Resumable { get; init; }

    internal string ToJson() =>
        JsonSerializer.Serialize(
            new Payload
            {
                Name = Name,
                DestinationDirectory = DestinationDirectory,
                Labels = Labels,
                Force = Force,
                RecordIntegrity = RecordIntegrity,
                Resumable = Resumable,
            },
            JsonDefaults.Options
        );

    private sealed class Payload
    {
        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("dest_dir")]
        public string? DestinationDirectory { get; init; }

        [JsonPropertyName("labels")]
        public IReadOnlyDictionary<string, string>? Labels { get; init; }

        [JsonPropertyName("force")]
        public bool Force { get; init; }

        [JsonPropertyName("record_integrity")]
        public bool RecordIntegrity { get; init; }

        [JsonPropertyName("resumable")]
        public bool Resumable { get; init; }
    }
}

public sealed class SnapshotExportOptions
{
    public bool WithParents { get; init; }
    public bool WithImage { get; init; }
    public bool PlainTar { get; init; }

    internal string ToJson() =>
        JsonSerializer.Serialize(
            new Payload
            {
                WithParents = WithParents,
                WithImage = WithImage,
                PlainTar = PlainTar,
            },
            JsonDefaults.Options
        );

    private sealed class Payload
    {
        [JsonPropertyName("with_parents")]
        public bool WithParents { get; init; }

        [JsonPropertyName("with_image")]
        public bool WithImage { get; init; }

        [JsonPropertyName("plain_tar")]
        public bool PlainTar { get; init; }
    }
}

public sealed record SnapshotArtifact(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("digest")] string Digest,
    [property: JsonPropertyName("size_bytes")] ulong SizeBytes,
    [property: JsonPropertyName("image_ref")] string ImageReference,
    [property: JsonPropertyName("image_manifest_digest")] string ImageManifestDigest,
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("format")] string Format,
    [property: JsonPropertyName("fstype")] string Filesystem,
    [property: JsonPropertyName("parent")] string? Parent,
    [property: JsonPropertyName("created_at")] string CreatedAt,
    [property: JsonPropertyName("labels")] IReadOnlyDictionary<string, string> Labels,
    [property: JsonPropertyName("source_sandbox")] string? SourceSandbox
);

public sealed record SnapshotInfo(
    [property: JsonPropertyName("digest")] string Digest,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("parent_digest")] string? ParentDigest,
    [property: JsonPropertyName("image_ref")] string ImageReference,
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("format")] string Format,
    [property: JsonPropertyName("size_bytes")] ulong? SizeBytes,
    [property: JsonPropertyName("created_at_unix")] long CreatedAtUnix,
    [property: JsonPropertyName("path")] string Path
)
{
    public DateTimeOffset CreatedAt => DateTimeOffset.FromUnixTimeSeconds(CreatedAtUnix);
}

public sealed record SnapshotVerifyReport(
    [property: JsonPropertyName("digest")] string Digest,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("upper")] SnapshotUpperVerifyStatus Upper
);

public sealed record SnapshotUpperVerifyStatus(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("algorithm")] string? Algorithm,
    [property: JsonPropertyName("digest")] string? Digest
);
