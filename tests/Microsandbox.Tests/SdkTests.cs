using System.Text.Json;
using Microsandbox;

namespace Microsandbox.Tests;

public sealed class SdkTests
{
    [Test]
    public void SandboxOptionsUseTheNativeJsonContract()
    {
        var json = new SandboxOptions
        {
            Image = "alpine:3.20",
            MemoryMiB = 512,
            CPUs = 2,
            MaxMemoryMiB = 1024,
            MaxCPUs = 4,
            Replace = true,
            ReplaceTimeout = TimeSpan.Zero,
            Detached = true,
            Ephemeral = true,
            MaxDuration = TimeSpan.FromMilliseconds(1001),
            IdleTimeout = TimeSpan.FromSeconds(30),
            Environment = new Dictionary<string, string> { ["HELLO"] = "world" },
        }.ToJson();

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Equal("alpine:3.20", root.GetProperty("image").GetString());
        Equal(512U, root.GetProperty("memory_mib").GetUInt32());
        Equal(2, root.GetProperty("cpus").GetByte());
        Equal(1024U, root.GetProperty("max_memory_mib").GetUInt32());
        Equal(4, root.GetProperty("max_cpus").GetByte());
        True(root.GetProperty("replace").GetBoolean());
        Equal(0UL, root.GetProperty("replace_with_timeout_ms").GetUInt64());
        True(root.GetProperty("detached").GetBoolean());
        True(root.GetProperty("ephemeral").GetBoolean());
        Equal(2UL, root.GetProperty("max_duration_secs").GetUInt64());
        Equal(30UL, root.GetProperty("idle_timeout_secs").GetUInt64());
        Equal("world", root.GetProperty("env").GetProperty("HELLO").GetString());
        False(root.TryGetProperty("labels", out _));
    }

    [Test]
    public void ExtendedSandboxOptionsUseTheGoCreateContract()
    {
        var json = new SandboxOptions
        {
            ImageBind = "/host/rootfs",
            RootDisk = new SandboxRootDiskOptions
            {
                Kind = "disk-image",
                Path = "/host/root.qcow2",
                Format = "qcow2",
                FileSystem = "ext4",
            },
            SecurityProfile = "restricted",
            RegistryAuth = new SandboxRegistryAuthOptions { Username = "user", Password = "secret" },
            Ports = new Dictionary<ushort, ushort> { [8080] = 80 },
            UdpPorts = new Dictionary<ushort, ushort> { [5353] = 53 },
            PortBindings =
            [
                new SandboxPortBindingOptions
                {
                    BindAddress = "127.0.0.1",
                    HostPort = 8443,
                    GuestPort = 443,
                    Protocol = "tcp",
                },
            ],
            Network = new SandboxNetworkOptions
            {
                Policy = "public-only",
                Dns = new SandboxDnsOptions
                {
                    RebindProtection = false,
                    NameServers = ["1.1.1.1:53"],
                    QueryTimeoutMilliseconds = 250,
                },
                CustomPolicy = new SandboxCustomNetworkPolicy
                {
                    DefaultEgress = "deny",
                    Rules = [new SandboxNetworkRule { Action = "allow", Destination = "api.example.com" }],
                },
                Tls = new SandboxTlsOptions
                {
                    VerifyUpstream = true,
                    BlockQuic = false,
                    ScopedVerifyUpstream = [new SandboxScopedVerifyUpstream("internal.example.com", false)],
                },
                MaxConnections = 100,
            },
            Secrets =
            [
                new SandboxSecretOptions
                {
                    EnvironmentVariable = "API_KEY",
                    Value = "secret",
                    AllowedHosts = ["api.example.com"],
                    RequireTls = true,
                },
            ],
            Patches =
            [
                new SandboxPatchOptions
                {
                    Kind = "text",
                    Path = "/etc/banner",
                    Content = "hello",
                    Mode = 420,
                    Replace = true,
                },
            ],
            VolumeMounts = new Dictionary<string, SandboxMountOptions>
            {
                ["/data"] = new()
                {
                    Named = "data",
                    NamedMode = "ensure-exists",
                    NamedKind = "disk",
                    SizeMiB = 1024,
                    ReadOnly = true,
                    StatVirtualization = "strict",
                },
            },
        }.ToJson();

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Equal("/host/rootfs", root.GetProperty("image_bind").GetString());
        Equal("disk-image", root.GetProperty("root_disk").GetProperty("kind").GetString());
        Equal("qcow2", root.GetProperty("root_disk").GetProperty("format").GetString());
        Equal("restricted", root.GetProperty("security_profile").GetString());
        Equal("user", root.GetProperty("registry_auth").GetProperty("username").GetString());
        Equal(80, root.GetProperty("ports").GetProperty("8080").GetUInt16());
        Equal(53, root.GetProperty("ports_udp").GetProperty("5353").GetUInt16());
        Equal("127.0.0.1", root.GetProperty("port_bindings")[0].GetProperty("bind").GetString());
        False(root.GetProperty("network").GetProperty("dns").GetProperty("rebind_protection").GetBoolean());
        False(root.GetProperty("network").GetProperty("tls").GetProperty("scoped_verify_upstream")[0]
            .GetProperty("verify").GetBoolean());
        Equal("allow", root.GetProperty("network").GetProperty("custom_policy").GetProperty("rules")[0]
            .GetProperty("action").GetString());
        Equal("API_KEY", root.GetProperty("secrets")[0].GetProperty("env_var").GetString());
        Equal("text", root.GetProperty("patches")[0].GetProperty("kind").GetString());
        Equal("data", root.GetProperty("volumes").GetProperty("/data").GetProperty("named").GetString());
    }

    [Test]
    public void ExecOptionsRoundTimeoutUpToWholeSeconds()
    {
        var json = new ExecOptions
        {
            Arguments = ["-c", "echo hello"],
            Timeout = TimeSpan.FromMilliseconds(1001),
        }.ToJson();

        using var document = JsonDocument.Parse(json);
        Equal(2UL, document.RootElement.GetProperty("timeout_secs").GetUInt64());
    }

    [Test]
    public void StreamingExecOptionsUseTheExactStdinContract()
    {
        using var document = JsonDocument.Parse(new ExecOptions
        {
            StdinPipe = true,
            Tty = true,
        }.ToJson());

        True(document.RootElement.GetProperty("stdin_pipe").GetBoolean());
        True(document.RootElement.GetProperty("tty").GetBoolean());
    }

    [Test]
    public void StreamingExecEventsDecodeTypedBase64Payloads()
    {
        var started = NativeApi.ParseExecEvent("""{"event":"started","pid":42}""");
        var stdout = NativeApi.ParseExecEvent("""{"event":"stdout","data":"AAH/"}""");
        var failed = NativeApi.ParseExecEvent(
            """{"event":"failed","error":{"kind":"not_found","errno":2,"errno_name":"ENOENT","message":"missing","path":"/bin/nope"}}""");
        var done = NativeApi.ParseExecEvent("""{"event":"done"}""");
        var collected = NativeApi.ParseExecCollect(
            """{"stdout_b64":"aGVsbG8=","stderr_b64":"d2FybmluZw==","exit_code":7}""");

        Equal(42U, ((ExecStartedEvent)started).ProcessId);
        True(((ExecStandardOutputEvent)stdout).Data.SequenceEqual(new byte[] { 0, 1, 255 }));
        Equal("ENOENT", ((ExecFailedEvent)failed).Failure.ErrnoName);
        True(ReferenceEquals(ExecDoneEvent.Instance, done));
        Equal("hello", collected.StandardOutput);
        Equal("warning", collected.StandardError);
        Equal(7, collected.ExitCode);
    }

    [Test]
    public void SandboxNamesAreLimitedByUtf8Bytes()
    {
        MicrosandboxClient.ValidateName(new string('a', 128));
        Throws<ArgumentOutOfRangeException>(() => MicrosandboxClient.ValidateName(new string('é', 65)));
    }

    [Test]
    public void ExecResultReportsSuccessFromExitCode()
    {
        True(new ExecResult("ok", string.Empty, 0).IsSuccess);
        False(new ExecResult(string.Empty, "failed", 1).IsSuccess);
    }

    [Test]
    public void NativeLifecycleResponsesUseTheExactJsonContract()
    {
        var metadata = NativeApi.ParseMetadata(
            """{"name":"demo","status":"running","config_json":"{}","created_at_unix":10,"updated_at_unix":null}""");
        var stopped = NativeApi.ParseStopResult(
            """{"name":"demo","status":"stopped","exit_code":0,"signal":null,"observed_at_unix":20,"source":"wait"}""");
        var ping = NativeApi.ParsePingResult("""{"name":"demo","latency_ms":1.25}""");
        var touch = NativeApi.ParseTouchResult("""{"name":"demo","activity_seq":42}""");

        Equal(SandboxStatus.Running, metadata.Status);
        Equal(DateTimeOffset.FromUnixTimeSeconds(10), metadata.CreatedAt);
        Equal(SandboxStatus.Stopped, stopped.Status);
        Equal(0, stopped.ExitCode);
        Equal(TimeSpan.FromMilliseconds(1.25), ping.Latency);
        Equal(42UL, touch.ActivitySequence);
    }

    [Test]
    public void NativeVersionResponseUsesTheExactJsonContract()
    {
        Equal("1.2.3", NativeApi.ParseVersion("""{"version":"1.2.3"}"""));
        NativeApi.ValidateVersion("0.6.6", "0.6.6");
        Throws<InvalidDataException>(() => NativeApi.ValidateVersion("0.6.5", "0.6.6"));
    }

    [Test]
    [Arguments("0.6.7", "0.6.7")]
    [Arguments("0.6.7+abcdef", "0.6.7")]
    [Arguments("0.6.7-rc.1+abcdef", "0.6.7-rc.1")]
    public void ManagedVersionPreservesPrereleaseIdentifiers(string informationalVersion, string expected) =>
        Equal(expected, NativeApi.NormalizeManagedVersion(informationalVersion));

    [Test]
    public async Task RetryableCompletionStateRestoresOpenAfterNativeFailure()
    {
        var state = new RetryableCompletionState();
        var calls = 0;

        try
        {
            await state.CompleteAsync(_ =>
            {
                calls++;
                throw new MicrosandboxException("io", "close failed");
            }, CancellationToken.None);
            throw new InvalidOperationException("Expected close failure.");
        }
        catch (MicrosandboxException)
        {
        }

        True(state.IsOpen);
        state.EnsureOpen("test");
        await state.CompleteAsync(token =>
        {
            calls++;
            False(token.CanBeCanceled);
            return Task.CompletedTask;
        }, CancellationToken.None);

        Equal(2, calls);
        False(state.IsOpen);
        Throws<ObjectDisposedException>(() => state.EnsureOpen("test"));
    }

    [Test]
    public async Task RetryableCompletionStateHonorsCancellationBeforeDispatch()
    {
        var state = new RetryableCompletionState();
        var calls = 0;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        try
        {
            await state.CompleteAsync(_ =>
            {
                calls++;
                return Task.CompletedTask;
            }, cancellation.Token);
            throw new InvalidOperationException("Expected cancellation.");
        }
        catch (OperationCanceledException)
        {
        }

        Equal(0, calls);
        True(state.IsOpen);
    }

    [Test]
    public async Task RetryableCompletionStateSerializesConcurrentCompletion()
    {
        var state = new RetryableCompletionState();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;

        async Task Complete(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            False(cancellationToken.CanBeCanceled);
            entered.SetResult();
            await release.Task;
        }

        var first = state.CompleteAsync(Complete, CancellationToken.None);
        await entered.Task;
        var second = state.CompleteAsync(Complete, CancellationToken.None);
        release.SetResult();
        await Task.WhenAll(first, second);

        Equal(1, calls);
    }

    [Test]
    public async Task ConsumingCloseStateDoesNotRestoreOwnershipAfterFailure()
    {
        var state = new ConsumingCloseState(42);
        var calls = 0;

        try
        {
            await state.CloseAsync((handle, _) =>
            {
                Equal(42UL, handle);
                calls++;
                throw new InvalidOperationException("close failed");
            }, CancellationToken.None);
            throw new InvalidOperationException("Expected close failure.");
        }
        catch (InvalidOperationException exception) when (exception.Message == "close failed")
        {
        }

        await state.CloseAsync((handle, _) =>
        {
            calls++;
            return Task.CompletedTask;
        }, CancellationToken.None);

        Equal(1, calls);
        False(state.IsOpen);
        Throws<ObjectDisposedException>(() => state.GetHandle("test"));
    }

    [Test]
    public async Task ConsumingCloseStateHonorsCancellationBeforeDispatch()
    {
        var state = new ConsumingCloseState(42);
        var calls = 0;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        try
        {
            await state.CloseAsync((_, _) =>
            {
                calls++;
                return Task.CompletedTask;
            }, cancellation.Token);
            throw new InvalidOperationException("Expected cancellation.");
        }
        catch (OperationCanceledException)
        {
        }

        Equal(0, calls);
        Equal(42UL, state.GetHandle("test"));
    }

    [Test]
    public async Task ConsumingCloseStateSerializesConcurrentCloseCalls()
    {
        var state = new ConsumingCloseState(7);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;

        async Task Close(ulong handle, CancellationToken cancellationToken)
        {
            Equal(7UL, handle);
            Interlocked.Increment(ref calls);
            entered.SetResult();
            False(cancellationToken.CanBeCanceled);
            await release.Task;
        }

        var first = state.CloseAsync(Close, CancellationToken.None);
        await entered.Task;
        var second = state.CloseAsync(Close, CancellationToken.None);
        release.SetResult();
        await Task.WhenAll(first, second);

        Equal(1, calls);
    }

    [Test]
    public void SandboxStopAndKillUseDifferentDefaultTimeouts()
    {
        Equal(10_000UL, Sandbox.StopTimeoutMilliseconds(null));
        Equal(5_000UL, Sandbox.KillTimeoutMilliseconds(null));
        Equal(10_000UL, SandboxHandle.StopTimeoutMilliseconds(null));
        Equal(5_000UL, SandboxHandle.KillTimeoutMilliseconds(null));
    }

    [Test]
    public void MalformedHandleResponsesCanBeSalvagedForCleanup()
    {
        Equal(123UL, NativeApi.SalvageHandle("""{"handle":123,"broken":}"""));
        Equal(0UL, NativeApi.SalvageHandle("""{"not_a_handle":123}"""));
    }

    [Test]
    public void NativeHandleOwnershipCanOnlyBeConsumedOnce()
    {
        long handle = 99;
        var first = Sandbox.ConsumeHandle(ref handle);
        var second = Sandbox.ConsumeHandle(ref handle);

        Equal(99L, first);
        Equal(0L, second);
    }

    [Test]
    public void StreamingNativeHandleOwnershipCanOnlyBeConsumedOnce()
    {
        long exec = 1;
        long logs = 2;
        long metrics = 3;

        Equal(1L, ExecHandle.ConsumeHandle(ref exec));
        Equal(0L, ExecHandle.ConsumeHandle(ref exec));
        Equal(2L, LogStream.ConsumeHandle(ref logs));
        Equal(0L, LogStream.ConsumeHandle(ref logs));
        Equal(3L, MetricsStream.ConsumeHandle(ref metrics));
        Equal(0L, MetricsStream.ConsumeHandle(ref metrics));
        Equal(9UL, NativeApi.SalvageHandle("""{"stream_handle":9,"broken":}""", "stream_handle"));
    }

    [Test]
    public void SshContractsDecodeBinaryOutputAndConsumeHandlesOnce()
    {
        using var clientOptions = JsonDocument.Parse(new SshClientOptions
        {
            User = "root",
            Terminal = "xterm-256color",
            EnableSftp = false,
        }.ToJson());
        using var serverOptions = JsonDocument.Parse(new SshServerOptions
        {
            HostKeyPath = "/tmp/host-key",
            AuthorizedKeysPath = "/tmp/authorized_keys",
            EnableSftp = true,
        }.ToJson());
        var output = NativeApi.ParseSshOutput("""{"status":7,"stdout":"AAH/","stderr":"d2FybmluZw=="}""");

        Equal("xterm-256color", clientOptions.RootElement.GetProperty("term").GetString());
        False(clientOptions.RootElement.GetProperty("sftp").GetBoolean());
        Equal("/tmp/host-key", serverOptions.RootElement.GetProperty("host_key_path").GetString());
        True(serverOptions.RootElement.GetProperty("sftp").GetBoolean());
        Equal(7, output.Status);
        True(output.StandardOutput.SequenceEqual(new byte[] { 0, 1, 255 }));
        Equal("warning", output.StandardErrorText);
        False(output.IsSuccess);

        long client = 1;
        long server = 2;
        long sftp = 3;
        Equal(1L, SshClient.ConsumeHandle(ref client));
        Equal(0L, SshClient.ConsumeHandle(ref client));
        Equal(2L, SshServer.ConsumeHandle(ref server));
        Equal(0L, SshServer.ConsumeHandle(ref server));
        Equal(3L, SftpClient.ConsumeHandle(ref sftp));
        Equal(0L, SftpClient.ConsumeHandle(ref sftp));
    }

    [Test]
    public void RawAgentContractsPreserveBytesFlagsAndAtomicOwnership()
    {
        var frame = new RawFrame(42, AgentFrameFlags.Terminal | AgentFrameFlags.SessionStart, [0, 1, 255]);
        Equal(42U, frame.Id);
        Equal((byte)3, frame.Flags);
        True(frame.Body.SequenceEqual(new byte[] { 0, 1, 255 }));
        Equal((byte)4, AgentFrameFlags.Shutdown);
        True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(AgentClient)));
        True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(AgentStream)));

        long client = 10;
        long stream = 11;
        Equal(10L, AgentClient.ConsumeHandle(ref client));
        Equal(0L, AgentClient.ConsumeHandle(ref client));
        Equal(11L, AgentStream.ConsumeHandle(ref stream));
        Equal(0L, AgentStream.ConsumeHandle(ref stream));
    }

    [Test]
    public void SandboxModificationUsesTheCanonicalPatchAndPlanContracts()
    {
        var options = new SandboxModificationOptions
        {
            Policy = ModificationPolicy.Restart,
            DryRun = true,
            Patch = new SandboxModificationPatch
            {
                CPUs = 2,
                RootDiskSizeMiB = 8192,
                Environment = new Dictionary<string, string> { ["Z"] = "last", ["A"] = "first" },
                Labels = new Dictionary<string, string> { ["env"] = "test" },
                Workdir = "/workspace",
                Secrets = new Dictionary<string, SandboxSecretModification>
                {
                    ["API_KEY"] = new()
                    {
                        EnvironmentVariable = "HOST_API_KEY",
                        Placeholder = "ref:api",
                        AllowedHosts = ["api.example.com"],
                    },
                },
            },
        };

        using var request = JsonDocument.Parse(options.ToJson());
        var patch = request.RootElement.GetProperty("patch");
        Equal("restart", request.RootElement.GetProperty("policy").GetString());
        True(request.RootElement.GetProperty("dry_run").GetBoolean());
        Equal(2, patch.GetProperty("cpus").GetByte());
        Equal(8192U, patch.GetProperty("root_disk_size_mib").GetUInt32());
        Equal("A", patch.GetProperty("env")[0].GetProperty("key").GetString());
        Equal("env", patch.GetProperty("labels")[0][0].GetString());
        Equal("API_KEY", patch.GetProperty("secrets")[0].GetProperty("name").GetString());
        Equal("env", patch.GetProperty("secrets")[0].GetProperty("source").GetProperty("kind").GetString());

        var plan = SandboxModificationPlan.Parse(
            """{"sandbox":"demo","status":"running","applied":false,"policy":"no_restart","changes":[{"kind":"config","field":"cpus","change":"updated","before":"1","after":"2","disposition":"live"}],"conflicts":[],"warnings":[],"resize_status":[{"resource":"cpus","requested":"2","actual":"2","enforced":"2","state":"applied"}]}""");
        Equal(ModificationPolicy.NoRestart, plan.Policy);
        Equal("config", plan.Changes[0].Kind);
        Equal("2", plan.Changes[0].After);
        Equal("cpus", plan.ResizeStatus[0].Resource);

        Throws<ArgumentException>(() => new SandboxModificationOptions
        {
            Patch = new SandboxModificationPatch
            {
                Secrets = new Dictionary<string, SandboxSecretModification>
                {
                    ["bad"] = new() { EnvironmentVariable = "ENV", Value = "secret" },
                },
            },
        }.ToJson());
    }

    [Test]
    public void LogOptionsAndEntriesUseTheExactJsonContract()
    {
        var options = new LogOptions
        {
            Tail = 50,
            Since = DateTimeOffset.FromUnixTimeMilliseconds(1000),
            Until = DateTimeOffset.FromUnixTimeMilliseconds(2000),
            Sources = [LogSource.Stdout, LogSource.System],
        }.ToJson();
        using var document = JsonDocument.Parse(options);
        Equal(50UL, document.RootElement.GetProperty("tail").GetUInt64());
        Equal(1000L, document.RootElement.GetProperty("since_ms").GetInt64());
        Equal("stdout", document.RootElement.GetProperty("sources")[0].GetString());

        var entries = NativeApi.ParseLogEntries(
            """[{"source":"stderr","session_id":null,"timestamp_ms":1500,"data_b64":"aGVsbG8=","cursor":"c1"}]""");
        Equal(LogSource.Stderr, entries[0].Source);
        Equal(null, entries[0].SessionId);
        Equal("hello", entries[0].Text);
        Equal(DateTimeOffset.FromUnixTimeMilliseconds(1500), entries[0].Timestamp);
    }

    [Test]
    public void LogStreamOptionsAndCursorEntriesUseTheExactJsonContract()
    {
        var options = new LogStreamOptions
        {
            Sources = [LogSource.Output],
            FromCursor = "cursor-1",
            Until = DateTimeOffset.FromUnixTimeMilliseconds(3000),
            Follow = true,
        }.ToJson();
        using var document = JsonDocument.Parse(options);

        Equal("output", document.RootElement.GetProperty("sources")[0].GetString());
        Equal("cursor-1", document.RootElement.GetProperty("from_cursor").GetString());
        Equal(3000L, document.RootElement.GetProperty("until_ms").GetInt64());
        True(document.RootElement.GetProperty("follow").GetBoolean());

        var entry = NativeApi.ParseLogEntry(
            """{"source":"output","session_id":7,"timestamp_ms":2500,"data_b64":"AAE=","cursor":"cursor-2"}""");
        Equal("cursor-2", entry?.Cursor);
        True(entry!.Data.SequenceEqual(new byte[] { 0, 1 }));
        Equal(null, NativeApi.ParseLogEntry("""{"done":true}"""));
    }

    [Test]
    public void FilesystemStreamingTypesAreStreamCompatible()
    {
        True(typeof(Stream).IsAssignableFrom(typeof(SandboxFileReadStream)));
        True(typeof(Stream).IsAssignableFrom(typeof(SandboxFileWriteStream)));
        True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(SandboxFileReadStream)));
        True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(SandboxFileWriteStream)));
    }

    [Test]
    public void FilesystemDtosParseNativeKindsAndNullableTimestamp()
    {
        var entries = NativeApi.ParseFilesystemEntries(
            """[{"path":"/tmp/a","kind":"dir","size":0,"mode":493}]""");
        var stat = NativeApi.ParseFilesystemStat(
            """{"kind":"file","size":4,"mode":420,"readonly":true,"modified_unix":null}""");

        Equal(FilesystemEntryKind.Dir, entries[0].Kind);
        Equal(493U, entries[0].Mode);
        Equal(FilesystemEntryKind.File, stat.Kind);
        True(stat.IsReadOnly);
        Equal(null, stat.ModifiedAt);
    }

    [Test]
    public void MetricsPreserveEveryNullableNativeField()
    {
        var metrics = NativeApi.ParseMetrics(
            """{"cpu_percent":1.5,"vcpu_time_ns":2,"memory_bytes":3,"memory_available_bytes":null,"memory_host_resident_bytes":4,"memory_limit_bytes":5,"disk_read_bytes":6,"disk_write_bytes":7,"net_rx_bytes":8,"net_tx_bytes":9,"upper_used_bytes":null,"upper_free_bytes":10,"upper_host_allocated_bytes":null,"uptime_secs":11}""");

        Equal(1.5, metrics.CpuPercent);
        Equal(null, metrics.MemoryAvailableBytes);
        Equal(4UL, metrics.MemoryHostResidentBytes);
        Equal(null, metrics.UpperUsedBytes);
        Equal(TimeSpan.FromSeconds(11), metrics.Uptime);
    }

    [Test]
    public void ImageVolumeAndSnapshotDtosParseNativeJson()
    {
        var image = NativeApi.ParseImageDetail(
            """{"reference":"alpine:latest","manifest_digest":"sha256:a","architecture":"arm64","os":"linux","layer_count":1,"size_bytes":null,"created_at_unix":10,"last_used_at_unix":null,"config":{"digest":"sha256:c","env":[],"cmd":["sh"],"entrypoint":[],"working_dir":"/","user":"root","labels":{},"stop_signal":""},"layers":[{"diff_id":"sha256:d","blob_digest":"sha256:b","media_type":"x","compressed_size_bytes":null,"erofs_size_bytes":20,"position":0}]}""");
        var volume = NativeApi.ParseVolumeInfo(
            """{"name":"data","path":"/vol/data","kind":"disk","quota_mib":null,"used_bytes":12,"capacity_bytes":1024,"disk_format":"raw","disk_fstype":"ext4","labels":{},"created_at_unix":null}""");
        var artifact = NativeApi.ParseSnapshotArtifact(
            """{"path":"/snap/a","digest":"sha256:s","size_bytes":9,"image_ref":"alpine","image_manifest_digest":"sha256:a","scope":"disk","format":"raw","fstype":"ext4","parent":null,"created_at":"2026-01-01T00:00:00Z","labels":{},"source_sandbox":null}""");
        var handle = NativeApi.ParseSnapshotInfo(
            """{"digest":"sha256:s","name":null,"parent_digest":null,"image_ref":"alpine","scope":"disk","format":"raw","size_bytes":null,"created_at_unix":20,"path":"/snap/a"}""");

        Equal("linux", image.OperatingSystem);
        Equal(null, image.SizeBytes);
        Equal(null, image.Layers[0].CompressedSizeBytes);
        Equal(VolumeKind.Disk, volume.Kind);
        Equal(null, volume.QuotaMiB);
        Equal(null, artifact.Parent);
        Equal(null, artifact.SourceSandbox);
        Equal(null, handle.Name);
        Equal(null, handle.SizeBytes);
    }

    [Test]
    public void VolumeAndSnapshotOptionsOmitDefaultsAndUseNativeNames()
    {
        using var volume = JsonDocument.Parse(new VolumeCreateOptions
        {
            Kind = VolumeKind.Dir,
            QuotaMiB = 100,
            Labels = new Dictionary<string, string> { ["env"] = "test" },
        }.ToJson());
        Equal("dir", volume.RootElement.GetProperty("kind").GetString());
        Equal(100U, volume.RootElement.GetProperty("quota_mib").GetUInt32());
        False(volume.RootElement.TryGetProperty("size_mib", out _));

        using var snapshot = JsonDocument.Parse(new SnapshotCreateOptions
        {
            Name = "snap",
            SourceSandbox = "box",
            DestinationDirectory = "/tmp",
            RecordIntegrity = true,
            Resumable = true,
        }.ToJson());
        Equal("snap", snapshot.RootElement.GetProperty("name").GetString());
        Equal("/tmp", snapshot.RootElement.GetProperty("dest_dir").GetString());
        True(snapshot.RootElement.GetProperty("record_integrity").GetBoolean());
        True(snapshot.RootElement.GetProperty("resumable").GetBoolean());
        False(snapshot.RootElement.TryGetProperty("source_sandbox", out _));
    }

    [Test]
    public void NativeAbiSymbolsAndVersionCanBeReadWhenLibraryIsProvided()
    {
        var libraryPath = Environment.GetEnvironmentVariable("MICROSANDBOX_FFI_LIBRARY");
        if (string.IsNullOrWhiteSpace(libraryPath))
        {
            return;
        }

        var client = MicrosandboxClient.Load(libraryPath);
        False(string.IsNullOrWhiteSpace(client.RuntimeVersion));
        False(string.IsNullOrWhiteSpace(client.GetAgentSocketPath("native-symbol-test")));

        var native = NativeApi.Load(libraryPath);
        Throws<MicrosandboxException>(() => native.AgentReadyBytes(0));
        Throws<MicrosandboxException>(() => native.AgentRequestAsync(0, 0, Array.Empty<byte>(), CancellationToken.None)
            .GetAwaiter().GetResult());
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
        }
    }

    private static void True(bool value)
    {
        if (!value)
        {
            throw new InvalidOperationException("Expected true.");
        }
    }

    private static void False(bool value) => True(!value);

    private static void Throws<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }
}
