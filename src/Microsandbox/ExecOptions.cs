using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsandbox;

/// <summary>Configures a command executed inside a sandbox.</summary>
public sealed record ExecOptions
{
    /// <summary>Gets or initializes command arguments.</summary>
    public IReadOnlyList<string>? Arguments { get; init; }

    /// <summary>Gets or initializes the command working directory.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Gets or initializes the command timeout.</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>Gets or initializes whether a pseudo-terminal is allocated.</summary>
    public bool Tty { get; init; }

    /// <summary>Gets or initializes the guest user.</summary>
    public string? User { get; init; }

    /// <summary>Gets or initializes command-specific environment variables.</summary>
    public IReadOnlyDictionary<string, string>? Environment { get; init; }

    /// <summary>Gets or initializes whether a writable stdin pipe is created for streaming execution.</summary>
    public bool StdinPipe { get; init; }

    internal string ToJson()
    {
        if (Timeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(Timeout), "Timeout cannot be negative.");
        }

        var timeoutSeconds =
            Timeout is { } timeout && timeout > TimeSpan.Zero
                ? checked((ulong)Math.Ceiling(timeout.TotalSeconds))
                : (ulong?)null;

        return JsonSerializer.Serialize(
            new ExecPayload
            {
                Arguments = Arguments,
                WorkingDirectory = WorkingDirectory,
                TimeoutSeconds = timeoutSeconds,
                Tty = Tty,
                User = User,
                Environment = Environment,
                StdinPipe = StdinPipe,
            },
            JsonDefaults.Options
        );
    }

    private sealed record ExecPayload
    {
        [JsonPropertyName("args")]
        public IReadOnlyList<string>? Arguments { get; init; }

        [JsonPropertyName("cwd")]
        public string? WorkingDirectory { get; init; }

        [JsonPropertyName("timeout_secs")]
        public ulong? TimeoutSeconds { get; init; }

        [JsonPropertyName("tty")]
        public bool Tty { get; init; }

        [JsonPropertyName("user")]
        public string? User { get; init; }

        [JsonPropertyName("env")]
        public IReadOnlyDictionary<string, string>? Environment { get; init; }

        [JsonPropertyName("stdin_pipe")]
        public bool StdinPipe { get; init; }
    }
}

/// <summary>Contains collected output from a completed command.</summary>
public sealed record ExecResult(string StandardOutput, string StandardError, int ExitCode)
{
    /// <summary>Gets whether the command exited successfully.</summary>
    public bool IsSuccess => ExitCode == 0;
}
