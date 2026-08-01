# .NET file-based app examples

These [.NET 10 file-based apps](https://learn.microsoft.com/dotnet/csharp/fundamentals/tutorials/file-based-programs)
reinterpret representative Go SDK examples without adding a project file per example.
Each source file references the local SDK through a `#:project` directive.

| Example | Demonstrates |
| --- | --- |
| [`basic.cs`](basic.cs) | Create, exec, shell, filesystem, metrics, stop, and remove |
| [`streaming-exec.cs`](streaming-exec.cs) | Typed stdout/stderr events, exit status, and signals |
| [`filesystem.cs`](filesystem.cs) | Guest file operations, host transfer, and streaming I/O |
| [`detached.cs`](detached.cs) | Detach, list, reconnect, verify persistence, and clean up |
| [`metrics.cs`](metrics.cs) | Point, streaming, and all-sandbox metrics |

## Run

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), or
install it with [mise](https://mise.jdx.dev/lang/dotnet.html). Confirm that the
SDK is available:

```bash
dotnet --version
```

From `sdk/dotnet`, run any example directly with the .NET CLI:

```bash
dotnet run --file examples/basic.cs
dotnet run --file examples/streaming-exec.cs
dotnet run --file examples/filesystem.cs
dotnet run --file examples/detached.cs
dotnet run --file examples/metrics.cs
```

When working from a source checkout, build the repository's native C ABI first,
then point the example at it. `MICROSANDBOX_MSB_PATH` is optional when `msb` is
already discoverable:

```bash
export MICROSANDBOX_FFI_LIBRARY="$(git rev-parse --show-toplevel)/target/release/libmicrosandbox_go_ffi.dylib" # macOS
export MICROSANDBOX_MSB_PATH="$(git rev-parse --show-toplevel)/build/msb"

dotnet run --file examples/basic.cs
```

On Linux, use `libmicrosandbox_go_ffi.so`. A packaged SDK resolves its native
RID asset automatically, so consumers do not normally set
`MICROSANDBOX_FFI_LIBRARY`.

Build every example without running a VM:

```bash
for example in examples/*.cs; do dotnet build "$example"; done
```
