using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsandbox;

/// <summary>Selects how a sandbox modification is planned or applied.</summary>
public enum ModificationPolicy
{
    /// <summary>Applies only changes that can complete without restarting.</summary>
    NoRestart,
    /// <summary>Persists changes for the next start without changing a running VM.</summary>
    NextStart,
    /// <summary>Restarts the sandbox when restart-required changes are present.</summary>
    Restart,
}

/// <summary>Describes a sandbox modification request.</summary>
public sealed record SandboxModificationOptions
{
    /// <summary>Gets the desired configuration patch.</summary>
    [JsonPropertyName("patch")]
    public SandboxModificationPatch Patch { get; init; } = new();

    /// <summary>Gets the apply policy.</summary>
    [JsonPropertyName("policy")]
    public ModificationPolicy Policy { get; init; }

    /// <summary>Gets whether to compute the plan without applying it.</summary>
    [JsonPropertyName("dry_run")]
    public bool DryRun { get; init; }

    internal string ToJson()
    {
        Patch.Validate();
        return JsonSerializer.Serialize(new ModificationRequest
        {
            Patch = Patch.ToWire(),
            Policy = Policy,
            DryRun = DryRun,
        }, JsonDefaults.Options);
    }

    private sealed record ModificationRequest
    {
        [JsonPropertyName("patch")]
        public required object Patch { get; init; }

        [JsonPropertyName("policy")]
        public ModificationPolicy Policy { get; init; }

        [JsonPropertyName("dry_run")]
        public bool DryRun { get; init; }
    }
}

/// <summary>Contains fields that can be changed on a persisted sandbox.</summary>
public sealed record SandboxModificationPatch
{
    /// <summary>Gets the desired effective vCPU count.</summary>
    public byte? CPUs { get; init; }

    /// <summary>Gets the desired boot-time maximum vCPU count.</summary>
    public byte? MaxCPUs { get; init; }

    /// <summary>Gets the desired effective memory in MiB.</summary>
    public uint? MemoryMiB { get; init; }

    /// <summary>Gets the desired boot-time maximum memory in MiB.</summary>
    public uint? MaxMemoryMiB { get; init; }

    /// <summary>Gets the desired managed or tmpfs root-disk size in MiB.</summary>
    public uint? RootDiskSizeMiB { get; init; }

    /// <summary>Gets environment variables to set for future execs.</summary>
    public IReadOnlyDictionary<string, string>? Environment { get; init; }

    /// <summary>Gets environment variable keys to remove.</summary>
    public IReadOnlyList<string>? EnvironmentRemove { get; init; }

    /// <summary>Gets labels to set.</summary>
    public IReadOnlyDictionary<string, string>? Labels { get; init; }

    /// <summary>Gets label keys to remove.</summary>
    public IReadOnlyList<string>? LabelsRemove { get; init; }

    /// <summary>Gets the desired working directory for future execs.</summary>
    public string? Workdir { get; init; }

    /// <summary>Gets desired secret specs keyed by stable secret name.</summary>
    public IReadOnlyDictionary<string, SandboxSecretModification>? Secrets { get; init; }

    /// <summary>Gets secret names to remove.</summary>
    public IReadOnlyList<string>? SecretsRemove { get; init; }

    internal object ToWire() => new PatchWire
    {
        CPUs = CPUs,
        MaxCPUs = MaxCPUs,
        MemoryMiB = MemoryMiB,
        MaxMemoryMiB = MaxMemoryMiB,
        RootDiskSizeMiB = RootDiskSizeMiB,
        Environment = Environment?.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new EnvironmentWire(item.Key, item.Value)).ToArray(),
        EnvironmentRemove = EnvironmentRemove,
        Labels = Labels?.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new[] { item.Key, item.Value }).ToArray(),
        LabelsRemove = LabelsRemove,
        Workdir = Workdir,
        Secrets = Secrets?.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => item.Value.ToWire(item.Key)).ToArray(),
        SecretsRemove = SecretsRemove,
    };

    internal void Validate()
    {
        if (CPUs == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(CPUs), "CPU count must be greater than zero.");
        }
        if (MaxCPUs == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxCPUs), "Maximum CPU count must be greater than zero.");
        }
        if (MemoryMiB == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MemoryMiB), "Memory must be greater than zero.");
        }
        if (MaxMemoryMiB == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxMemoryMiB), "Maximum memory must be greater than zero.");
        }
        if (RootDiskSizeMiB == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(RootDiskSizeMiB), "Root disk size must be greater than zero.");
        }

        if (Secrets is not null)
        {
            foreach (var (name, secret) in Secrets)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(name);
                secret.Validate(name);
            }
        }
    }

    private sealed record PatchWire
    {
        [JsonPropertyName("cpus")]
        public byte? CPUs { get; init; }
        [JsonPropertyName("max_cpus")]
        public byte? MaxCPUs { get; init; }
        [JsonPropertyName("memory_mib")]
        public uint? MemoryMiB { get; init; }
        [JsonPropertyName("max_memory_mib")]
        public uint? MaxMemoryMiB { get; init; }
        [JsonPropertyName("root_disk_size_mib")]
        public uint? RootDiskSizeMiB { get; init; }
        [JsonPropertyName("env")]
        public IReadOnlyList<EnvironmentWire>? Environment { get; init; }
        [JsonPropertyName("env_remove")]
        public IReadOnlyList<string>? EnvironmentRemove { get; init; }
        [JsonPropertyName("labels")]
        public IReadOnlyList<IReadOnlyList<string>>? Labels { get; init; }
        [JsonPropertyName("labels_remove")]
        public IReadOnlyList<string>? LabelsRemove { get; init; }
        [JsonPropertyName("workdir")]
        public string? Workdir { get; init; }
        [JsonPropertyName("secrets")]
        public IReadOnlyList<object>? Secrets { get; init; }
        [JsonPropertyName("secrets_remove")]
        public IReadOnlyList<string>? SecretsRemove { get; init; }
    }

    private sealed record EnvironmentWire(
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("value")] string Value);
}

/// <summary>Describes the desired state of one modified secret.</summary>
public sealed record SandboxSecretModification
{
    /// <summary>Gets a host environment variable used as secret material.</summary>
    public string? EnvironmentVariable { get; init; }

    /// <summary>Gets raw secret material held by the caller.</summary>
    public string? Value { get; init; }

    /// <summary>Gets a host secret-store reference.</summary>
    public string? StoreReference { get; init; }

    /// <summary>Gets an explicit guest-visible placeholder.</summary>
    public string? Placeholder { get; init; }

    /// <summary>Gets desired allowed host patterns.</summary>
    public IReadOnlyList<string>? AllowedHosts { get; init; }

    internal object ToWire(string name) => new SecretWire
    {
        Name = name,
        Source = !string.IsNullOrEmpty(EnvironmentVariable)
            ? new SourceWire { Kind = "env", Variable = EnvironmentVariable }
            : !string.IsNullOrEmpty(StoreReference)
                ? new SourceWire { Kind = "store", Reference = StoreReference }
                : null,
        Value = string.IsNullOrEmpty(Value) ? null : Value,
        Placeholder = string.IsNullOrEmpty(Placeholder) ? null : Placeholder,
        AllowedHosts = AllowedHosts,
    };

    internal void Validate(string name)
    {
        var sources = (string.IsNullOrEmpty(EnvironmentVariable) ? 0 : 1)
            + (string.IsNullOrEmpty(Value) ? 0 : 1)
            + (string.IsNullOrEmpty(StoreReference) ? 0 : 1);
        if (sources > 1)
        {
            throw new ArgumentException(
                $"Secret '{name}' environment variable, value, and store reference are mutually exclusive.",
                nameof(name));
        }
    }

    private sealed record SecretWire
    {
        [JsonPropertyName("name")]
        public required string Name { get; init; }
        [JsonPropertyName("source")]
        public SourceWire? Source { get; init; }
        [JsonPropertyName("value")]
        public string? Value { get; init; }
        [JsonPropertyName("placeholder")]
        public string? Placeholder { get; init; }
        [JsonPropertyName("allowed_hosts")]
        public IReadOnlyList<string>? AllowedHosts { get; init; }
    }

    private sealed record SourceWire
    {
        [JsonPropertyName("kind")]
        public required string Kind { get; init; }
        [JsonPropertyName("var")]
        public string? Variable { get; init; }
        [JsonPropertyName("reference")]
        public string? Reference { get; init; }
    }
}

/// <summary>Contains a dry-run or applied sandbox modification plan.</summary>
public sealed record SandboxModificationPlan
{
    /// <summary>Gets the sandbox name.</summary>
    [JsonPropertyName("sandbox")]
    public required string Sandbox { get; init; }

    /// <summary>Gets the lifecycle status used for classification.</summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>Gets whether the changes were applied.</summary>
    [JsonPropertyName("applied")]
    public bool Applied { get; init; }

    /// <summary>Gets the policy used to produce the plan.</summary>
    [JsonPropertyName("policy")]
    public ModificationPolicy Policy { get; init; }

    /// <summary>Gets planned config and secret changes.</summary>
    [JsonPropertyName("changes")]
    public IReadOnlyList<SandboxPlannedChange> Changes { get; init; } = [];

    /// <summary>Gets conflicts that block applying the patch.</summary>
    [JsonPropertyName("conflicts")]
    public IReadOnlyList<SandboxModificationMessage> Conflicts { get; init; } = [];

    /// <summary>Gets non-fatal planning warnings.</summary>
    [JsonPropertyName("warnings")]
    public IReadOnlyList<SandboxModificationMessage> Warnings { get; init; } = [];

    /// <summary>Gets live resource resize convergence results.</summary>
    [JsonPropertyName("resize_status")]
    public IReadOnlyList<SandboxResourceResizeStatus> ResizeStatus { get; init; } = [];

    internal static SandboxModificationPlan Parse(string json) =>
        JsonSerializer.Deserialize<SandboxModificationPlan>(json, JsonDefaults.Options)
        ?? throw new InvalidDataException("The native ABI returned an empty sandbox modification plan.");
}

/// <summary>Contains one planned config or secret change.</summary>
public sealed record SandboxPlannedChange
{
    /// <summary>Gets <c>config</c> or <c>secret</c>.</summary>
    [JsonPropertyName("kind")]
    public required string Kind { get; init; }
    /// <summary>Gets the modified field.</summary>
    [JsonPropertyName("field")]
    public required string Field { get; init; }
    /// <summary>Gets the secret name for secret changes.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }
    /// <summary>Gets the natural change classification.</summary>
    [JsonPropertyName("change")]
    public required string Change { get; init; }
    /// <summary>Gets the previous visible config state.</summary>
    [JsonPropertyName("before")]
    public string? Before { get; init; }
    /// <summary>Gets the desired visible config state.</summary>
    [JsonPropertyName("after")]
    public string? After { get; init; }
    /// <summary>Gets the previous guest-visible secret reference.</summary>
    [JsonPropertyName("before_ref")]
    public string? BeforeReference { get; init; }
    /// <summary>Gets the desired guest-visible secret reference.</summary>
    [JsonPropertyName("after_ref")]
    public string? AfterReference { get; init; }
    /// <summary>Gets when or whether the change can take effect.</summary>
    [JsonPropertyName("disposition")]
    public required string Disposition { get; init; }
    /// <summary>Gets allowed hosts after a secret change.</summary>
    [JsonPropertyName("allow_hosts")]
    public IReadOnlyList<string> AllowedHosts { get; init; } = [];
    /// <summary>Gets a human-readable classification reason.</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}

/// <summary>Contains a field-associated modification conflict or warning.</summary>
public sealed record SandboxModificationMessage(
    [property: JsonPropertyName("field")] string Field,
    [property: JsonPropertyName("message")] string Message);

/// <summary>Reports convergence for one live resource resize.</summary>
public sealed record SandboxResourceResizeStatus(
    [property: JsonPropertyName("resource")] string Resource,
    [property: JsonPropertyName("requested")] string Requested,
    [property: JsonPropertyName("actual")] string Actual,
    [property: JsonPropertyName("enforced")] string Enforced,
    [property: JsonPropertyName("state")] string State);
