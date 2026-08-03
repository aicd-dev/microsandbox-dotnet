# .NET examples

These standalone SDK examples are [.NET 10 file-based apps](https://learn.microsoft.com/dotnet/csharp/fundamentals/tutorials/file-based-programs).
Each source file references the local SDK through a `#:project` directive.


| Example | Demonstrates |
| --- | --- |
| [`basic.cs`](basic.cs) | Create, exec, shell, filesystem, metrics, stop, and remove |
| [`streaming-exec.cs`](streaming-exec.cs) | Typed stdout/stderr events, exit status, and signals |
| [`filesystem.cs`](filesystem.cs) | Guest file operations, host transfer, and streaming I/O |
| [`detached.cs`](detached.cs) | Detach, list, reconnect, verify persistence, and clean up |
| [`metrics.cs`](metrics.cs) | Point, streaming, and all-sandbox metrics |
| [`snapshot-fork.cs`](snapshot-fork.cs) | Snapshot creation, verification, indexing, and forking |
| [`ports.cs`](ports.cs) | TCP host-to-guest port publishing |
| [`volumes.cs`](volumes.cs) | Named volume creation, listing, duplicate errors, and removal |
| [`secrets.cs`](secrets.cs) | Secret placeholders without guest-value exposure |
| [`patches.cs`](patches.cs) | Root filesystem text, append, mkdir, symlink, copy, and remove patches |

## Run

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), or
install it with [mise](https://mise.jdx.dev/lang/dotnet.html). Confirm that the
SDK is available:

```bash
dotnet --version
```

### Quick start with release binaries

From the repository root, download the latest release, verify its checksums, and load
the generated environment file. The version property compiles the checkout's
managed SDK against that exact native release:

```bash
dotnet run --file scripts/download-runtime.cs
source .runtime/env.sh
dotnet run --file examples/basic.cs -p:Version="$MICROSANDBOX_RELEASE_VERSION"
```

PowerShell uses the same downloader:

```powershell
dotnet run --file scripts/download-runtime.cs
. .\.runtime\env.ps1
dotnet run --file examples/basic.cs -p:Version=$env:MICROSANDBOX_RELEASE_VERSION
```

This downloads `msb`, `libkrunfw`, and the native FFI library into the ignored
`.runtime` directory. It does not require Rust, Go, Docker, `just`, or a
native build toolchain.

Run any example directly with the .NET CLI:

```bash
dotnet run --file examples/basic.cs -p:Version="$MICROSANDBOX_RELEASE_VERSION"
dotnet run --file examples/streaming-exec.cs -p:Version="$MICROSANDBOX_RELEASE_VERSION"
dotnet run --file examples/filesystem.cs -p:Version="$MICROSANDBOX_RELEASE_VERSION"
dotnet run --file examples/detached.cs -p:Version="$MICROSANDBOX_RELEASE_VERSION"
dotnet run --file examples/metrics.cs -p:Version="$MICROSANDBOX_RELEASE_VERSION"
dotnet run --file examples/snapshot-fork.cs -p:Version="$MICROSANDBOX_RELEASE_VERSION"
dotnet run --file examples/ports.cs -p:Version="$MICROSANDBOX_RELEASE_VERSION"
dotnet run --file examples/volumes.cs -p:Version="$MICROSANDBOX_RELEASE_VERSION"
dotnet run --file examples/secrets.cs -p:Version="$MICROSANDBOX_RELEASE_VERSION"
dotnet run --file examples/patches.cs -p:Version="$MICROSANDBOX_RELEASE_VERSION"
```

### Use the packaged FFI runtime

This repository does not build native code from source. `mise run native-download`
downloads and checksum-validates the five upstream release binaries. NuGet consumers
resolve their RID asset automatically; source-checkout examples can point at a staged
current-platform binary. `MICROSANDBOX_MSB_PATH` is optional when `msb` is discoverable:

```bash
mise run native-download
export MICROSANDBOX_FFI_LIBRARY="$PWD/src/Microsandbox/runtimes/osx-arm64/native/libmicrosandbox_go_ffi.dylib" # macOS arm64

dotnet run --file examples/basic.cs
```

On Linux, use the staged `linux-x64` or `linux-arm64` `.so`. The prebuilt assets
come from `superradcompany/microsandbox` releases and are not committed here.

Build every example without running a VM:

```bash
for example in examples/*.cs; do dotnet build "$example"; done
```
