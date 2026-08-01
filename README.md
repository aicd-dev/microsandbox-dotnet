# microsandbox for .NET

.NET 10 SDK over the same native C ABI used by the Go SDK. The SDK covers
native loading, creation and detached creation, collected and streaming command execution,
name-addressed lookup and lifecycle operations, cancellation, and explicit
native handle ownership.

## Features

- Sandbox lifecycle, lookup/list, request-style stop/kill/drain, stop-and-wait, persisted-state removal, collected and streaming command execution, cancellation, and explicit handle ownership.
- Streaming exec events, collection/wait/control, PTY resize, and a single-take asynchronous stdin sink.
- `Sandbox.Filesystem`: byte/string and `Stream`-compatible read/write, list, stat, host copy in both directions, mkdir, remove/remove-directory, guest copy/rename, and exists.
- Collected and streaming sandbox logs with sources, timestamps, cursors, follow mode, and 48 MiB receive buffers.
- Point-in-time and streaming sandbox metrics plus client-wide metrics keyed by sandbox name.
- Client image cache get/list/inspect/remove/prune/load/save operations with typed OCI metadata.
- Named volume create/get/list/remove operations with typed directory/disk metadata.
- Snapshot create/open/verify/get/list/list-directory/remove/reindex/export/import operations.
- Native SSH client/server handles, collected SSH exec, command/default-shell attach, stdio serving with compatibility aliases, and collected SFTP file operations.
- Raw agent connections by sandbox or socket path, byte-exact request/send/ready operations, and disposable raw streams.
- Live-handle and name-addressed sandbox modification with typed patch and plan contracts.
- Name-addressed collected metrics and snapshot convenience methods on `SandboxHandle`.
- Go-compatible creation options for root disks, bind rootfs, security profiles, registry auth, ports, network policy/DNS/TLS, secrets, patches, and volume mounts.

Direct host-side volume filesystem access is intentionally excluded because lexical path
rooting alone does not prevent symlink traversal outside a volume root.

## Development

[mise](https://mise.jdx.dev/) pins .NET 10 and provides the local workflow:

```bash
cd sdk/dotnet
mise install
mise run check
```

Runnable [.NET 10 file-based app examples](examples/README.md) cover basic
lifecycle, streaming exec, filesystem I/O, detached sandboxes, and metrics:

```bash
dotnet run --file examples/basic.cs
```

Build the Rust C ABI and exercise native loading from TUnit:

```bash
mise run test-native
```

Create a managed-only development package:

```bash
mise run pack-local
mise run inspect
```

Ordinary `dotnet pack` is release-shaped: it requires and includes all five native
runtime assets. Use `mise run pack-local` only when an explicit managed-only package
is needed for local development.

For a release-shaped package, download or copy the five Go release assets and
the release's `checksums.sha256` into one directory, then stage and validate them:

```bash
mise run stage-native -- /path/to/release-assets 0.6.6
mise run pack-release
mise run inspect
```

`stage-native` maps the Linux x64/arm64, macOS arm64, and Windows x64/arm64
release filenames to canonical libraries under `runtimes/<rid>/native`. It verifies
every SHA-256 checksum and platform binary header before transactionally replacing
the staged runtime tree and recording the source release version. `pack-release`
requires that version to match the NuGet
package version; override both when preparing a different release with
`-p:PackageVersion=...` and the matching `stage-native` version argument.
Staged binaries and packages are ignored by git.

The SDK resolves the native library in this order:

1. The explicit path passed to `MicrosandboxClient.Load(path)`.
2. `MICROSANDBOX_FFI_LIBRARY`.
3. A NuGet-style `runtimes/<rid>/native` asset beside the application.
4. The platform loader's normal search path.

Each candidate must export the complete ABI and report the same `msb_version` as
the managed package. Incompatible candidates are unloaded before resolution falls
back to the next candidate.

## Usage

```csharp
using Microsandbox;

var client = MicrosandboxClient.Load();
client.SetMsbPath("/path/to/msb");

await using var sandbox = await client.CreateAsync("dotnet-demo", new SandboxOptions
{
    Image = "alpine:3.20",
    MemoryMiB = 512,
    CPUs = 1,
    Replace = true,
    Detached = true,
});

var result = await sandbox.ShellAsync("echo 'Hello from microsandbox!'");
Console.WriteLine(result.StandardOutput);
var ping = await sandbox.PingAsync();
Console.WriteLine($"agent latency: {ping.Latency}");

await using (var exec = await sandbox.ShellStreamingAsync("read value; echo $value", new ExecOptions
{
    StdinPipe = true,
}))
{
    await using var stdin = exec.TakeStdin()!;
    await stdin.WriteAsync("hello\n"u8.ToArray());
    await stdin.CompleteAsync();

    while (true)
    {
        var message = await exec.ReceiveAsync();
        if (message is ExecDoneEvent)
        {
            break;
        }

        if (message is ExecStandardOutputEvent output)
        {
            Console.Write(System.Text.Encoding.UTF8.GetString(output.Data));
        }
    }
}

await using (var writer = await sandbox.Filesystem.WriteStreamAsync("/tmp/large.bin"))
{
    await writer.WriteAsync(new byte[1024]);
} // DisposeAsync sends EOF and waits for write confirmation.

await using var logs = await sandbox.LogStreamAsync(new LogStreamOptions { Follow = true });
var firstLog = await logs.ReceiveAsync();

await using var metrics = await sandbox.MetricsStreamAsync(TimeSpan.FromSeconds(1));
var firstSnapshot = await metrics.ReceiveAsync();

await using (var ssh = await sandbox.Ssh.OpenClientAsync(new SshClientOptions { EnableSftp = true }))
{
    var sshResult = await ssh.ExecuteAsync("printf hello");
    Console.WriteLine(sshResult.StandardOutputText);

    await using var sftp = await ssh.OpenSftpAsync();
    await sftp.WriteStringAsync("/tmp/message", "hello");
}

await using (var agent = await sandbox.ConnectAgentAsync())
{
    // Bodies are byte-exact CBOR protocol payloads; decoding is caller-owned.
    var readyBody = agent.GetReadyBytes();
}

// Detach consumes the native handle while leaving this detached VM running.
await sandbox.DetachAsync();

var handle = await client.LookupAsync("dotnet-demo");
await using var connected = await handle.ConnectAsync();
await connected.KillAsync();
```

`DisposeAsync` closes any handle that has not been detached. For a
lifecycle-owning sandbox, native close stops the VM; call `DetachAsync` only
when the sandbox was created or started in detached mode and should continue
running.

## Parity and limitations

- SSH exec and SFTP operations are collected because the C ABI exposes collected results. `AttachAsync`, `AttachShellAsync`, SSH client attach, and `ServeStdioAsync` bridge the current process terminal and block until completion. `ServeConnectionAsync` and `ServeConsoleConnectionAsync` are compatibility aliases for the same native stdio behavior.
- Raw agent frame bodies remain CBOR bytes; the package does not select or bundle a CBOR object model.
- Agent streams depend on their parent client remaining open. Dispose streams before disposing the client.
- `SandboxHandle` supports request stop/kill/drain, collected and streaming logs, collected metrics, and named snapshot creation where the ABI has name-addressed exports; stop-and-wait, synchronous drain, persisted-state removal, attach, and metrics streaming require a live sandbox handle.
- Direct host-side volume filesystem access remains excluded because lexical rooting alone does not prevent symlink traversal outside a volume root.
