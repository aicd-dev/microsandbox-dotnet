using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsandbox;

/// <summary>Configures creation of a microsandbox.</summary>
public sealed record SandboxOptions
{
    /// <summary>Gets or initializes the OCI image reference.</summary>
    public string? Image { get; init; }

    /// <summary>Gets or initializes the filesystem type for a disk image.</summary>
    public string? ImageFileSystem { get; init; }

    /// <summary>Gets or initializes a host directory used directly as the root filesystem.</summary>
    public string? ImageBind { get; init; }

    /// <summary>Gets or initializes the writable root-disk configuration for an OCI image.</summary>
    public SandboxRootDiskOptions? RootDisk { get; init; }

    /// <summary>Gets or initializes a snapshot name or path to boot.</summary>
    public string? Snapshot { get; init; }

    /// <summary>Gets or initializes guest memory in MiB.</summary>
    public uint? MemoryMiB { get; init; }

    /// <summary>Gets or initializes the guest virtual CPU count.</summary>
    public byte? CPUs { get; init; }

    /// <summary>Gets or initializes the maximum hot-pluggable guest memory in MiB.</summary>
    public uint? MaxMemoryMiB { get; init; }

    /// <summary>Gets or initializes the maximum hot-pluggable virtual CPU count.</summary>
    public byte? MaxCPUs { get; init; }

    /// <summary>Gets or initializes the guest working directory.</summary>
    public string? Workdir { get; init; }

    /// <summary>Gets or initializes the guest shell path.</summary>
    public string? Shell { get; init; }

    /// <summary>Gets or initializes the in-guest security profile.</summary>
    public string? SecurityProfile { get; init; }

    /// <summary>Gets or initializes the guest hostname.</summary>
    public string? Hostname { get; init; }

    /// <summary>Gets or initializes the default guest user.</summary>
    public string? User { get; init; }

    /// <summary>Gets or initializes whether an existing sandbox is replaced.</summary>
    public bool Replace { get; init; }

    /// <summary>Gets or initializes the replacement grace period. Setting it implies replacement.</summary>
    public TimeSpan? ReplaceTimeout { get; init; }

    /// <summary>Gets or initializes whether the VM outlives release of its native handle.</summary>
    public bool Detached { get; init; }

    /// <summary>Gets or initializes whether persisted state is removed after shutdown.</summary>
    public bool Ephemeral { get; init; }

    /// <summary>Gets or initializes the maximum sandbox lifetime.</summary>
    public TimeSpan? MaxDuration { get; init; }

    /// <summary>Gets or initializes the inactivity timeout.</summary>
    public TimeSpan? IdleTimeout { get; init; }

    /// <summary>Gets or initializes the guest entrypoint.</summary>
    public IReadOnlyList<string>? Entrypoint { get; init; }

    /// <summary>Gets or initializes the guest PID 1 process.</summary>
    public SandboxInitOptions? Init { get; init; }

    /// <summary>Gets or initializes the OCI image pull policy.</summary>
    public string? PullPolicy { get; init; }

    /// <summary>Gets or initializes the sandbox log level.</summary>
    public string? LogLevel { get; init; }

    /// <summary>Gets or initializes whether guest output is suppressed from host logs.</summary>
    public bool QuietLogs { get; init; }

    /// <summary>Gets or initializes named lifecycle scripts.</summary>
    public IReadOnlyDictionary<string, string>? Scripts { get; init; }

    /// <summary>Gets or initializes guest environment variables.</summary>
    public IReadOnlyDictionary<string, string>? Environment { get; init; }

    /// <summary>Gets or initializes persistent sandbox labels.</summary>
    public IReadOnlyDictionary<string, string>? Labels { get; init; }

    /// <summary>Gets or initializes private OCI registry credentials.</summary>
    public SandboxRegistryAuthOptions? RegistryAuth { get; init; }

    /// <summary>Gets or initializes TCP host-to-guest port mappings.</summary>
    public IReadOnlyDictionary<ushort, ushort>? Ports { get; init; }

    /// <summary>Gets or initializes UDP host-to-guest port mappings.</summary>
    public IReadOnlyDictionary<ushort, ushort>? UdpPorts { get; init; }

    /// <summary>Gets or initializes explicit host address and port bindings.</summary>
    public IReadOnlyList<SandboxPortBindingOptions>? PortBindings { get; init; }

    /// <summary>Gets or initializes sandbox network configuration.</summary>
    public SandboxNetworkOptions? Network { get; init; }

    /// <summary>Gets or initializes transport-substituted secrets.</summary>
    public IReadOnlyList<SandboxSecretOptions>? Secrets { get; init; }

    /// <summary>Gets or initializes root-filesystem patches applied before boot.</summary>
    public IReadOnlyList<SandboxPatchOptions>? Patches { get; init; }

    /// <summary>Gets or initializes volume mounts keyed by guest path.</summary>
    public IReadOnlyDictionary<string, SandboxMountOptions>? VolumeMounts { get; init; }

    internal string ToJson()
    {
        ValidateDuration(ReplaceTimeout, nameof(ReplaceTimeout));
        ValidateDuration(MaxDuration, nameof(MaxDuration));
        ValidateDuration(IdleTimeout, nameof(IdleTimeout));

        return JsonSerializer.Serialize(new CreatePayload
        {
            Image = Image,
            ImageFileSystem = ImageFileSystem,
            ImageBind = ImageBind,
            RootDisk = RootDisk,
            Snapshot = Snapshot,
            MemoryMiB = MemoryMiB,
            CPUs = CPUs,
            MaxMemoryMiB = MaxMemoryMiB,
            MaxCPUs = MaxCPUs,
            Workdir = Workdir,
            Shell = Shell,
            SecurityProfile = SecurityProfile,
            Hostname = Hostname,
            User = User,
            Replace = Replace,
            ReplaceTimeoutMilliseconds = ToMilliseconds(ReplaceTimeout),
            Detached = Detached,
            Ephemeral = Ephemeral,
            MaxDurationSeconds = ToSeconds(MaxDuration),
            IdleTimeoutSeconds = ToSeconds(IdleTimeout),
            Entrypoint = Entrypoint,
            Init = Init,
            PullPolicy = PullPolicy,
            LogLevel = LogLevel,
            QuietLogs = QuietLogs,
            Scripts = Scripts,
            Environment = Environment,
            Labels = Labels,
            RegistryAuth = RegistryAuth,
            Ports = Ports,
            UdpPorts = UdpPorts,
            PortBindings = PortBindings,
            Network = Network,
            Secrets = Secrets,
            Patches = Patches,
            VolumeMounts = VolumeMounts,
        }, JsonDefaults.Options);
    }

    private static void ValidateDuration(TimeSpan? value, string parameterName)
    {
        if (value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Duration cannot be negative.");
        }
    }

    private static ulong? ToMilliseconds(TimeSpan? value) => value is { } duration
        ? checked((ulong)Math.Ceiling(duration.TotalMilliseconds))
        : null;

    private static ulong? ToSeconds(TimeSpan? value) => value is { } duration
        ? checked((ulong)Math.Ceiling(duration.TotalSeconds))
        : null;

    private sealed record CreatePayload
    {
        [JsonPropertyName("image")]
        public string? Image { get; init; }

        [JsonPropertyName("image_fstype")]
        public string? ImageFileSystem { get; init; }

        [JsonPropertyName("image_bind")]
        public string? ImageBind { get; init; }

        [JsonPropertyName("root_disk")]
        public SandboxRootDiskOptions? RootDisk { get; init; }

        [JsonPropertyName("snapshot")]
        public string? Snapshot { get; init; }

        [JsonPropertyName("memory_mib")]
        public uint? MemoryMiB { get; init; }

        [JsonPropertyName("cpus")]
        public byte? CPUs { get; init; }

        [JsonPropertyName("max_memory_mib")]
        public uint? MaxMemoryMiB { get; init; }

        [JsonPropertyName("max_cpus")]
        public byte? MaxCPUs { get; init; }

        [JsonPropertyName("workdir")]
        public string? Workdir { get; init; }

        [JsonPropertyName("shell")]
        public string? Shell { get; init; }

        [JsonPropertyName("security_profile")]
        public string? SecurityProfile { get; init; }

        [JsonPropertyName("hostname")]
        public string? Hostname { get; init; }

        [JsonPropertyName("user")]
        public string? User { get; init; }

        [JsonPropertyName("replace")]
        public bool Replace { get; init; }

        [JsonPropertyName("replace_with_timeout_ms")]
        public ulong? ReplaceTimeoutMilliseconds { get; init; }

        [JsonPropertyName("detached")]
        public bool Detached { get; init; }

        [JsonPropertyName("ephemeral")]
        public bool Ephemeral { get; init; }

        [JsonPropertyName("max_duration_secs")]
        public ulong? MaxDurationSeconds { get; init; }

        [JsonPropertyName("idle_timeout_secs")]
        public ulong? IdleTimeoutSeconds { get; init; }

        [JsonPropertyName("entrypoint")]
        public IReadOnlyList<string>? Entrypoint { get; init; }

        [JsonPropertyName("init")]
        public SandboxInitOptions? Init { get; init; }

        [JsonPropertyName("pull_policy")]
        public string? PullPolicy { get; init; }

        [JsonPropertyName("log_level")]
        public string? LogLevel { get; init; }

        [JsonPropertyName("quiet_logs")]
        public bool QuietLogs { get; init; }

        [JsonPropertyName("scripts")]
        public IReadOnlyDictionary<string, string>? Scripts { get; init; }

        [JsonPropertyName("env")]
        public IReadOnlyDictionary<string, string>? Environment { get; init; }

        [JsonPropertyName("labels")]
        public IReadOnlyDictionary<string, string>? Labels { get; init; }

        [JsonPropertyName("registry_auth")]
        public SandboxRegistryAuthOptions? RegistryAuth { get; init; }

        [JsonPropertyName("ports")]
        public IReadOnlyDictionary<ushort, ushort>? Ports { get; init; }

        [JsonPropertyName("ports_udp")]
        public IReadOnlyDictionary<ushort, ushort>? UdpPorts { get; init; }

        [JsonPropertyName("port_bindings")]
        public IReadOnlyList<SandboxPortBindingOptions>? PortBindings { get; init; }

        [JsonPropertyName("network")]
        public SandboxNetworkOptions? Network { get; init; }

        [JsonPropertyName("secrets")]
        public IReadOnlyList<SandboxSecretOptions>? Secrets { get; init; }

        [JsonPropertyName("patches")]
        public IReadOnlyList<SandboxPatchOptions>? Patches { get; init; }

        [JsonPropertyName("volumes")]
        public IReadOnlyDictionary<string, SandboxMountOptions>? VolumeMounts { get; init; }
    }
}

/// <summary>Configures the writable root layer of an OCI image.</summary>
public sealed record SandboxRootDiskOptions
{
    /// <summary>Gets <c>managed</c>, <c>tmpfs</c>, or <c>disk-image</c>.</summary>
    [JsonPropertyName("kind")]
    public required string Kind { get; init; }
    /// <summary>Gets the managed or tmpfs size in MiB.</summary>
    [JsonPropertyName("size_mib")]
    public uint? SizeMiB { get; init; }
    /// <summary>Gets the host path for a disk-image root.</summary>
    [JsonPropertyName("path")]
    public string? Path { get; init; }
    /// <summary>Gets the disk image format hint.</summary>
    [JsonPropertyName("format")]
    public string? Format { get; init; }
    /// <summary>Gets the inner filesystem type hint.</summary>
    [JsonPropertyName("fstype")]
    public string? FileSystem { get; init; }
}

/// <summary>Contains credentials for a private OCI registry.</summary>
public sealed record SandboxRegistryAuthOptions
{
    /// <summary>Gets the registry username.</summary>
    [JsonPropertyName("username")]
    public required string Username { get; init; }
    /// <summary>Gets the registry password.</summary>
    [JsonPropertyName("password")]
    public required string Password { get; init; }
}

/// <summary>Publishes one guest port on a host address and port.</summary>
public sealed record SandboxPortBindingOptions
{
    /// <summary>Gets the host bind address.</summary>
    [JsonPropertyName("bind")]
    public string? BindAddress { get; init; }
    /// <summary>Gets the host port.</summary>
    [JsonPropertyName("host_port")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public ushort HostPort { get; init; }
    /// <summary>Gets the guest port.</summary>
    [JsonPropertyName("guest_port")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public ushort GuestPort { get; init; }
    /// <summary>Gets <c>tcp</c> or <c>udp</c>.</summary>
    [JsonPropertyName("protocol")]
    public string? Protocol { get; init; }
}

/// <summary>Configures the sandbox network stack.</summary>
public sealed record SandboxNetworkOptions
{
    /// <summary>Gets a preset such as <c>public-only</c> or <c>allow-all</c>.</summary>
    [JsonPropertyName("policy")]
    public string? Policy { get; init; }
    /// <summary>Gets an ordered custom policy.</summary>
    [JsonPropertyName("custom_policy")]
    public SandboxCustomNetworkPolicy? CustomPolicy { get; init; }
    /// <summary>Gets DNS proxy configuration.</summary>
    [JsonPropertyName("dns")]
    public SandboxDnsOptions? Dns { get; init; }
    /// <summary>Gets the legacy flat DNS rebinding switch.</summary>
    [JsonPropertyName("dns_rebind_protection")]
    public bool? DnsRebindProtection { get; init; }
    /// <summary>Gets exact domains denied by DNS.</summary>
    [JsonPropertyName("deny_domains")]
    public IReadOnlyList<string>? DenyDomains { get; init; }
    /// <summary>Gets domain suffixes denied by DNS.</summary>
    [JsonPropertyName("deny_domain_suffixes")]
    public IReadOnlyList<string>? DenyDomainSuffixes { get; init; }
    /// <summary>Gets transparent TLS interception configuration.</summary>
    [JsonPropertyName("tls")]
    public SandboxTlsOptions? Tls { get; init; }
    /// <summary>Gets nested TCP port mappings.</summary>
    [JsonPropertyName("ports")]
    public IReadOnlyDictionary<ushort, ushort>? Ports { get; init; }
    /// <summary>Gets nested explicit port bindings.</summary>
    [JsonPropertyName("port_bindings")]
    public IReadOnlyList<SandboxPortBindingOptions>? PortBindings { get; init; }
    /// <summary>Gets the IPv4 allocation pool.</summary>
    [JsonPropertyName("ipv4_pool")]
    public string? IPv4Pool { get; init; }
    /// <summary>Gets the IPv6 allocation pool.</summary>
    [JsonPropertyName("ipv6_pool")]
    public string? IPv6Pool { get; init; }
    /// <summary>Gets the concurrent connection limit.</summary>
    [JsonPropertyName("max_connections")]
    public uint? MaxConnections { get; init; }
    /// <summary>Gets the sandbox-wide secret-violation action.</summary>
    [JsonPropertyName("on_secret_violation")]
    public string? OnSecretViolation { get; init; }
    /// <summary>Gets whether host extra CA bundles are trusted in the guest.</summary>
    [JsonPropertyName("trust_host_cas")]
    public bool? TrustHostCertificateAuthorities { get; init; }
}

/// <summary>Configures the in-VM DNS proxy.</summary>
public sealed record SandboxDnsOptions
{
    /// <summary>Gets whether DNS rebinding protection is enabled.</summary>
    [JsonPropertyName("rebind_protection")]
    public bool? RebindProtection { get; init; }
    /// <summary>Gets upstream resolver addresses.</summary>
    [JsonPropertyName("nameservers")]
    public IReadOnlyList<string>? NameServers { get; init; }
    /// <summary>Gets the DNS query timeout in milliseconds.</summary>
    [JsonPropertyName("query_timeout_ms")]
    public ulong? QueryTimeoutMilliseconds { get; init; }
}

/// <summary>Configures an explicit ordered network policy.</summary>
public sealed record SandboxCustomNetworkPolicy
{
    /// <summary>Gets the unmatched egress action.</summary>
    [JsonPropertyName("default_egress")]
    public string? DefaultEgress { get; init; }
    /// <summary>Gets the unmatched ingress action.</summary>
    [JsonPropertyName("default_ingress")]
    public string? DefaultIngress { get; init; }
    /// <summary>Gets ordered firewall rules.</summary>
    [JsonPropertyName("rules")]
    public IReadOnlyList<SandboxNetworkRule>? Rules { get; init; }
}

/// <summary>Describes one firewall rule.</summary>
public sealed record SandboxNetworkRule
{
    /// <summary>Gets <c>allow</c> or <c>deny</c>.</summary>
    [JsonPropertyName("action")]
    public required string Action { get; init; }
    /// <summary>Gets <c>egress</c>, <c>ingress</c>, or <c>any</c>.</summary>
    [JsonPropertyName("direction")]
    public string? Direction { get; init; }
    /// <summary>Gets the destination matcher.</summary>
    [JsonPropertyName("destination")]
    public string? Destination { get; init; }
    /// <summary>Gets a legacy single protocol.</summary>
    [JsonPropertyName("protocol")]
    public string? Protocol { get; init; }
    /// <summary>Gets multiple protocol matchers.</summary>
    [JsonPropertyName("protocols")]
    public IReadOnlyList<string>? Protocols { get; init; }
    /// <summary>Gets a single port or range.</summary>
    [JsonPropertyName("port")]
    public string? Port { get; init; }
    /// <summary>Gets multiple ports or ranges.</summary>
    [JsonPropertyName("ports")]
    public IReadOnlyList<string>? Ports { get; init; }
}

/// <summary>Configures transparent TLS interception.</summary>
public sealed record SandboxTlsOptions
{
    /// <summary>Gets hosts that bypass interception.</summary>
    [JsonPropertyName("bypass")]
    public IReadOnlyList<string>? Bypass { get; init; }
    /// <summary>Gets whether upstream certificates are verified.</summary>
    [JsonPropertyName("verify_upstream")]
    public bool? VerifyUpstream { get; init; }
    /// <summary>Gets intercepted TCP ports.</summary>
    [JsonPropertyName("intercepted_ports")]
    public IReadOnlyList<ushort>? InterceptedPorts { get; init; }
    /// <summary>Gets whether QUIC is blocked on intercepted ports.</summary>
    [JsonPropertyName("block_quic")]
    public bool? BlockQuic { get; init; }
    /// <summary>Gets the interception CA certificate path.</summary>
    [JsonPropertyName("ca_cert")]
    public string? CertificateAuthorityCertificate { get; init; }
    /// <summary>Gets the interception CA private-key path.</summary>
    [JsonPropertyName("ca_key")]
    public string? CertificateAuthorityKey { get; init; }
    /// <summary>Gets extra upstream CA bundle paths.</summary>
    [JsonPropertyName("upstream_ca_certs")]
    public IReadOnlyList<string>? UpstreamCertificateAuthorities { get; init; }
    /// <summary>Gets host-scoped upstream CA bundles.</summary>
    [JsonPropertyName("scoped_upstream_ca_certs")]
    public IReadOnlyList<SandboxScopedUpstreamCertificateAuthority>? ScopedUpstreamCertificateAuthorities { get; init; }
    /// <summary>Gets host-scoped upstream verification overrides.</summary>
    [JsonPropertyName("scoped_verify_upstream")]
    public IReadOnlyList<SandboxScopedVerifyUpstream>? ScopedVerifyUpstream { get; init; }
}

/// <summary>Associates an upstream CA bundle with a host pattern.</summary>
public sealed record SandboxScopedUpstreamCertificateAuthority(
    [property: JsonPropertyName("pattern")] string Pattern,
    [property: JsonPropertyName("path")] string Path);

/// <summary>Overrides upstream certificate verification for a host pattern.</summary>
public sealed record SandboxScopedVerifyUpstream(
    [property: JsonPropertyName("pattern")] string Pattern,
    [property: JsonPropertyName("verify")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] bool Verify);

/// <summary>Configures one transport-substituted secret.</summary>
public sealed record SandboxSecretOptions
{
    /// <summary>Gets the guest environment variable containing the placeholder.</summary>
    [JsonPropertyName("env_var")]
    public required string EnvironmentVariable { get; init; }
    /// <summary>Gets the host-side secret value.</summary>
    [JsonPropertyName("value")]
    public required string Value { get; init; }
    /// <summary>Gets exact allowed hosts.</summary>
    [JsonPropertyName("allow_hosts")]
    public IReadOnlyList<string>? AllowedHosts { get; init; }
    /// <summary>Gets wildcard allowed-host patterns.</summary>
    [JsonPropertyName("allow_host_patterns")]
    public IReadOnlyList<string>? AllowedHostPatterns { get; init; }
    /// <summary>Gets an explicit guest-visible placeholder.</summary>
    [JsonPropertyName("placeholder")]
    public string? Placeholder { get; init; }
    /// <summary>Gets whether verified TLS is required for substitution.</summary>
    [JsonPropertyName("require_tls")]
    public bool? RequireTls { get; init; }
    /// <summary>Gets the secret-violation action.</summary>
    [JsonPropertyName("on_violation")]
    public string? OnViolation { get; init; }
}

/// <summary>Describes one root-filesystem patch.</summary>
public sealed record SandboxPatchOptions
{
    /// <summary>Gets the patch discriminator.</summary>
    [JsonPropertyName("kind")]
    public required string Kind { get; init; }
    /// <summary>Gets the target path for text, append, mkdir, or remove.</summary>
    [JsonPropertyName("path")]
    public string? Path { get; init; }
    /// <summary>Gets text patch content.</summary>
    [JsonPropertyName("content")]
    public string? Content { get; init; }
    /// <summary>Gets an optional Unix mode.</summary>
    [JsonPropertyName("mode")]
    public uint? Mode { get; init; }
    /// <summary>Gets whether an existing target is replaced.</summary>
    [JsonPropertyName("replace")]
    public bool Replace { get; init; }
    /// <summary>Gets a host copy source.</summary>
    [JsonPropertyName("src")]
    public string? Source { get; init; }
    /// <summary>Gets a guest copy destination.</summary>
    [JsonPropertyName("dst")]
    public string? Destination { get; init; }
    /// <summary>Gets a symbolic-link target.</summary>
    [JsonPropertyName("target")]
    public string? Target { get; init; }
    /// <summary>Gets a symbolic-link path.</summary>
    [JsonPropertyName("link")]
    public string? Link { get; init; }
}

/// <summary>Describes one bind, named, tmpfs, or disk volume mount.</summary>
public sealed record SandboxMountOptions
{
    /// <summary>Gets a host bind path.</summary>
    [JsonPropertyName("bind")]
    public string? Bind { get; init; }
    /// <summary>Gets a named volume.</summary>
    [JsonPropertyName("named")]
    public string? Named { get; init; }
    /// <summary>Gets named-volume creation behavior.</summary>
    [JsonPropertyName("named_mode")]
    public string? NamedMode { get; init; }
    /// <summary>Gets the named-volume kind.</summary>
    [JsonPropertyName("named_kind")]
    public string? NamedKind { get; init; }
    /// <summary>Gets whether this is a tmpfs mount.</summary>
    [JsonPropertyName("tmpfs")]
    public bool Tmpfs { get; init; }
    /// <summary>Gets a host disk-image path.</summary>
    [JsonPropertyName("disk")]
    public string? Disk { get; init; }
    /// <summary>Gets the disk image format hint.</summary>
    [JsonPropertyName("format")]
    public string? Format { get; init; }
    /// <summary>Gets the inner filesystem type hint.</summary>
    [JsonPropertyName("fstype")]
    public string? FileSystem { get; init; }
    /// <summary>Gets whether the mount is read-only.</summary>
    [JsonPropertyName("readonly")]
    public bool ReadOnly { get; init; }
    /// <summary>Gets whether executable files are disabled.</summary>
    [JsonPropertyName("noexec")]
    public bool NoExec { get; init; }
    /// <summary>Gets whether set-user-ID and set-group-ID are disabled.</summary>
    [JsonPropertyName("nosuid")]
    public bool NoSuid { get; init; }
    /// <summary>Gets whether device nodes are disabled.</summary>
    [JsonPropertyName("nodev")]
    public bool NoDev { get; init; }
    /// <summary>Gets the requested size in MiB.</summary>
    [JsonPropertyName("size_mib")]
    public uint? SizeMiB { get; init; }
    /// <summary>Gets the guest-write quota in MiB.</summary>
    [JsonPropertyName("quota_mib")]
    public uint? QuotaMiB { get; init; }
    /// <summary>Gets <c>strict</c>, <c>relaxed</c>, or <c>off</c>.</summary>
    [JsonPropertyName("stat_virtualization")]
    public string? StatVirtualization { get; init; }
    /// <summary>Gets <c>private</c> or <c>mirror</c>.</summary>
    [JsonPropertyName("host_permissions")]
    public string? HostPermissions { get; init; }
}

internal static class JsonDefaults
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };
}
