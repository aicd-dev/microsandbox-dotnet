using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsandbox;

/// <summary>Identifies a persisted sandbox lifecycle state.</summary>
public enum SandboxStatus
{
    /// <summary>The sandbox record has been created.</summary>
    Created,
    /// <summary>The sandbox is starting.</summary>
    Starting,
    /// <summary>The sandbox is running.</summary>
    Running,
    /// <summary>The sandbox is draining.</summary>
    Draining,
    /// <summary>The sandbox is paused.</summary>
    Paused,
    /// <summary>The sandbox stopped normally.</summary>
    Stopped,
    /// <summary>The sandbox process crashed.</summary>
    Crashed,
}

/// <summary>Filters name-addressed sandbox listings.</summary>
public sealed record SandboxFilter
{
    /// <summary>Gets labels that every returned sandbox must match.</summary>
    public IReadOnlyDictionary<string, string>? Labels { get; init; }

    internal string ToJson() => JsonSerializer.Serialize(new FilterPayload { Labels = Labels }, JsonDefaults.Options);

    private sealed record FilterPayload
    {
        [JsonPropertyName("labels")]
        public IReadOnlyDictionary<string, string>? Labels { get; init; }
    }
}

/// <summary>Describes a terminal sandbox state.</summary>
public sealed record SandboxStopResult(
    string Name,
    SandboxStatus Status,
    int? ExitCode,
    int? Signal,
    DateTimeOffset ObservedAt,
    string? Source);

/// <summary>Describes a successful agent reachability check.</summary>
public sealed record SandboxPingResult(string Name, TimeSpan Latency);

/// <summary>Describes an explicit idle-activity refresh.</summary>
public sealed record SandboxTouchResult(string Name, ulong ActivitySequence);

/// <summary>Configures the guest PID 1 process.</summary>
public sealed record SandboxInitOptions
{
    /// <summary>Gets the init command.</summary>
    [JsonPropertyName("cmd")]
    public required string Command { get; init; }
    /// <summary>Gets init command arguments.</summary>
    [JsonPropertyName("args")]
    public IReadOnlyList<string>? Arguments { get; init; }
    /// <summary>Gets init environment entries as key/value pairs.</summary>
    [JsonPropertyName("env")]
    public IReadOnlyList<IReadOnlyList<string>>? Environment { get; init; }
}
