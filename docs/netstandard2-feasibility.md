# .NET Standard 2.0 feasibility study

## Status and scope

This document evaluates whether the .NET SDK can support .NET Standard 2.0 without
discarding the existing .NET 8 implementation. It is a design study, not an
implementation plan approved for release.

The assessment covers only `sdk/dotnet`. It assumes that the SDK continues to call
the native C ABI shipped by Microsandbox and that the NuGet package continues to
carry the five currently supported native assets:

- Linux x64
- Linux ARM64
- macOS ARM64
- Windows x64
- Windows ARM64

Targeting .NET Standard broadens the set of managed applications that can reference
the SDK. It does not make the native runtime portable to platforms for which no
native asset exists. In particular, it would not by itself add support for Windows
x86, macOS x64, musl Linux, mobile, browser/WASM, Unity, or arbitrary Mono hosts.

## Executive summary

Supporting .NET Standard 2.0 is technically feasible. The public sandbox feature set
does not need to be reduced, but the implementation would need compatibility code in
four main areas:

1. Dynamic native-library loading, runtime identification, and UTF-8 marshaling.
2. Modern `Stream`, memory, and asynchronous-disposal APIs.
3. Compiler metadata needed by records, `init`, and `required` members.
4. Managed dependencies that .NET 8 currently supplies as part of the framework.

An experimental compile against `netstandard2.0`, after adding compiler-attribute
polyfills and `System.Text.Json`, stops first at the four modern `Stream` overrides in
`Filesystem.cs`. A complete source inventory identifies additional compatibility work
after those declarations are made conditional.

The recommended design is a single multi-targeted `Withakay.Microsandbox` package:

```xml
<TargetFrameworks>netstandard2.0;net8.0</TargetFrameworks>
```

This preserves the current implementation for .NET 8 and later while providing a
compatibility implementation for older hosts. NuGet selects the best assembly for
the consuming application. A separate `Withakay.Microsandbox.Core` package is
feasible, but it is only justified if contracts need to be consumed independently or
a second backend is planned.

## Current baseline

`src/Microsandbox/Microsandbox.csproj` currently targets `net8.0`, has no managed
package dependencies, and packs native libraries under `runtimes/<rid>/native`.

The SDK is not transport-neutral. `MicrosandboxClient`, `Sandbox`, the service
objects, streaming handles, filesystem streams, SSH/SFTP objects, and agent objects
all depend directly on the internal `NativeApi`. `NativeApi.cs` owns dynamic loading,
symbol resolution, marshaling, version validation, cancellation, and calls to the C
ABI.

The current public surface uses modern .NET features extensively:

- Records, `init` accessors, and `required` members are used throughout the option and
  result models.
- `IAsyncDisposable` and `ValueTask` are used for native-handle ownership.
- `Memory<byte>` and `ReadOnlyMemory<byte>` are used by streaming APIs.
- Filesystem streams override the modern memory-based `Stream` methods and
  `Stream.DisposeAsync`.
- `System.Text.Json` provides contract serialization and snake-case enum conversion.
- `NativeLibrary` dynamically loads the native ABI and resolves approximately one
  hundred exports.

The test project currently targets `net10.0`. It therefore exercises the `net8.0`
library asset and would not exercise a .NET Standard compatibility branch.

## What .NET Standard 2.0 would and would not provide

.NET Standard 2.0 is an API contract rather than a runtime. A `netstandard2.0`
assembly can be selected by older .NET, .NET Framework, and some Mono-family hosts,
but successful execution still depends on:

- A supported operating system and architecture.
- A compatible native Microsandbox library.
- Dynamic loading and marshaling behavior on that host.
- A sufficiently recent C# compiler for convenient use of the SDK's modern source
  model.

NuGet considers .NET Framework 4.6.1 compatible with .NET Standard 2.0, but .NET
Framework 4.7.2 or later should be the practical documented minimum. The dependency
graph required by modern JSON, memory, and async APIs is substantially less painful
there. Windows applications would also need to run as 64-bit processes because no
Windows x86 native asset exists.

## Concrete compatibility changes

### Project targeting and dependencies

Modify `src/Microsandbox/Microsandbox.csproj` to multi-target:

```xml
<TargetFrameworks>netstandard2.0;net8.0</TargetFrameworks>
<LangVersion>latest</LangVersion>
```

Add conditional package references only for `netstandard2.0`:

```xml
<ItemGroup Condition="'$(TargetFramework)' == 'netstandard2.0'">
  <PackageReference Include="System.Text.Json" Version="8.0.5" />
  <PackageReference Include="Microsoft.Bcl.AsyncInterfaces" Version="8.0.0" />
  <PackageReference Include="System.Memory" Version="4.5.5" />
  <PackageReference Include="System.Threading.Tasks.Extensions" Version="4.5.4" />
</ItemGroup>
```

Versions shown here are representative known-compatible versions, not a final pinning
decision. Before implementation, use the latest compatible patch releases and inspect
their transitive dependencies and support policies.

The .NET Standard dependency group would also pull packages such as
`System.Text.Encodings.Web`, `System.Buffers`, and
`System.Runtime.CompilerServices.Unsafe`. .NET Framework applications may require
binding redirects. The `net8.0` asset should continue to use framework-provided APIs
without these package dependencies.

### Compiler metadata for modern C# models

The current records and `init` properties can compile for .NET Standard with a modern
compiler, but the target reference assemblies do not define every attribute emitted
for those features.

Add `src/Microsandbox/Compatibility/CompilerAttributes.cs`, compiled only for
`NETSTANDARD2_0`, with internal definitions for:

- `System.Runtime.CompilerServices.IsExternalInit`
- `System.Runtime.CompilerServices.RequiredMemberAttribute`
- `System.Runtime.CompilerServices.CompilerFeatureRequiredAttribute`
- `System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute`

This retains the existing source-level API. Consumers using older C# compilers can
reference the assembly, but those compilers will not provide the same source syntax
or `required`-member diagnostics.

### Dynamic native-library loading

`NativeApi.Load` currently calls `NativeLibrary.TryLoad` and `NativeLibrary.Free`.
`NativeApi.GetExport<T>` uses `NativeLibrary.GetExport`. These APIs are not available
in the .NET Standard 2.0 reference surface.

Add `src/Microsandbox/Compatibility/NativeLibraryCompat.cs` with a small internal
abstraction:

```csharp
internal interface INativeLibraryLoader
{
    bool TryLoad(string path, out IntPtr handle);
    IntPtr GetExport(IntPtr handle, string name);
    void Free(IntPtr handle);
}
```

Implement it as follows:

- `net8.0`: delegate directly to `NativeLibrary`.
- Windows .NET Standard: call `LoadLibraryExW`, `GetProcAddress`, and `FreeLibrary`.
- Linux/macOS .NET Standard: call `dlopen`, `dlsym`, `dlclose`, and `dlerror` through
  carefully isolated P/Invoke declarations.

Then change `NativeApi.Load`, its constructor/export initialization, and
`GetExport<T>` to use this abstraction. Preserve the existing behavior of unloading
libraries that fail symbol or version validation and retaining a compatible library
for the process lifetime.

This is the highest-risk compatibility change. Loader behavior must be validated on
actual Windows x64, Linux x64/ARM64, and macOS ARM64 hosts; a successful compile is
not sufficient.

### Runtime and RID detection

`NativeApi.CandidatePaths` uses `OperatingSystem.IsWindows`,
`OperatingSystem.IsMacOS`, and `RuntimeInformation.RuntimeIdentifier`. The first two
helpers and the runtime identifier are not uniformly available on .NET Standard 2.0
hosts.

Add `src/Microsandbox/Compatibility/RuntimePlatform.cs` that combines:

- `RuntimeInformation.IsOSPlatform(OSPlatform.Windows/Linux/OSX)`
- `RuntimeInformation.ProcessArchitecture`

Map only to the five supported RIDs. Unsupported OS/architecture combinations must
throw a clear `PlatformNotSupportedException`; they must not silently default to
Linux or x64. `OSPlatform` and `ProcessArchitecture` cannot distinguish glibc from
musl, so libc detection must either be added or musl must fail later during native
loading with an explicitly documented diagnostic.

Change `NativeApi.CandidatePaths` to use the compatibility mapper while retaining the
current lookup order:

1. Explicit path supplied to `MicrosandboxClient.Load`.
2. `MICROSANDBOX_FFI_LIBRARY`.
3. `runtimes/<rid>/native` beside the application.
4. Application base directory.
5. The platform loader's normal search path.

### UTF-8 marshaling

`NativeApi.Utf8String` uses `Marshal.StringToCoTaskMemUTF8`, and native result parsing
uses `Marshal.PtrToStringUTF8`. Those convenience APIs are unavailable from the .NET
Standard 2.0 reference surface.

Add `src/Microsandbox/Compatibility/Utf8Marshal.cs`:

- Encoding a managed string should use `Encoding.UTF8.GetBytes`, allocate one extra
  byte with `Marshal.AllocCoTaskMem`, copy the bytes, and append a null terminator.
- Decoding a pointer should determine the byte count up to the null terminator, copy
  the bytes, and call `Encoding.UTF8.GetString`.
- Allocation ownership must remain explicit and pair with `Marshal.FreeCoTaskMem`.
- Impose a defensible maximum scan length and fail if no terminator is found. This
  limits accidental over-reads but cannot make an invalid native pointer safe; a
  length-bearing ABI would be required for that guarantee.

Use the compatibility helper only for `netstandard2.0`; preserve the current framework
helpers for `net8.0`.

### Filesystem stream implementation

`SandboxFileReadStream` currently overrides:

```csharp
ValueTask<int> ReadAsync(Memory<byte>, CancellationToken)
ValueTask DisposeAsync()
```

`SandboxFileWriteStream` currently overrides:

```csharp
ValueTask WriteAsync(ReadOnlyMemory<byte>, CancellationToken)
ValueTask DisposeAsync()
```

Those virtual members do not exist on the .NET Standard 2.0 `Stream` type. This is the
only area where identical source declarations cannot produce identical virtual
dispatch metadata across the two targets.

For `netstandard2.0`:

- Override `Task<int> ReadAsync(byte[], int, int, CancellationToken)`.
- Override `Task WriteAsync(byte[], int, int, CancellationToken)`.
- Keep public memory-based overloads on the concrete stream types where useful.
- Implement `IAsyncDisposable.DisposeAsync` explicitly or as a non-override public
  member using `Microsoft.Bcl.AsyncInterfaces`.
- Retain `Dispose(bool)` as the synchronous fallback.
- Replace `ValueTask.CompletedTask` with `default(ValueTask)` or an equivalent
  compatible construction.
- Replace `CopyToAsync(output, cancellationToken)` with
  `CopyToAsync(output, 81920, cancellationToken)`.

For `net8.0`, retain the current overrides unchanged.

Consequences for the .NET Standard assembly:

- Calls through a `Stream` reference use the older array-based virtual members.
- Direct calls on `SandboxFileReadStream` and `SandboxFileWriteStream` can retain the
  memory-based convenience overloads.
- Some paths may allocate temporary arrays or perform additional copies.
- Async disposal remains available when the static type exposes `IAsyncDisposable`,
  but `Stream.DisposeAsync` virtual dispatch is unavailable on old runtimes.

No sandbox capability is lost, but the older target has weaker stream ergonomics and
potentially lower throughput.

### Guard and convenience APIs

Replace newer guard helpers in `MicrosandboxClient.cs`, `Filesystem.cs`, `Images.cs`,
`Sandbox.cs`, `SandboxHandle.cs`, `SandboxModification.cs`, `Snapshots.cs`, `Ssh.cs`,
and `Volumes.cs`:

```csharp
ArgumentNullException.ThrowIfNull(value);
ArgumentException.ThrowIfNullOrWhiteSpace(value);
ObjectDisposedException.ThrowIf(condition, instance);
```

Use shared internal helpers or explicit checks so behavior and parameter names remain
consistent. For example:

```csharp
if (value is null)
{
    throw new ArgumentNullException(nameof(value));
}
```

This is mechanical work and should not alter the public API.

### Other BCL substitutions

The following smaller changes are also required in the .NET Standard build:

- Replace span-based `Convert.ToBase64String` calls in `NativeApi.cs` with array-based
  overloads, accepting an allocation where necessary.
- Replace `KeyValuePair<TKey,TValue>` deconstruction in `SandboxModification.cs` with
  explicit `.Key` and `.Value` access.
- Audit all `Task.Run`, cancellation, semaphore, and interlocked paths on .NET
  Framework and Mono even where the APIs compile; scheduler and shutdown behavior can
  differ from modern .NET.
- Keep JSON behavior tests around `JsonDefaults` in `SandboxOptions.cs`, especially
  `DefaultJsonTypeInfoResolver`, `JsonNamingPolicy.SnakeCaseLower`, null omission, and
  fields where explicit zero values matter.

### File-level change summary

| File or new component | Required change |
| --- | --- |
| `Microsandbox.csproj` | Multi-target, add conditional dependencies, and ensure pack validation behaves correctly with two TFMs |
| `Compatibility/CompilerAttributes.cs` | Supply compiler-recognized metadata for records, `init`, and `required` on .NET Standard |
| `Compatibility/Guard.cs` | Centralize replacements for unavailable argument and disposal guard helpers |
| `Compatibility/NativeLibraryCompat.cs` | Provide load, symbol lookup, error reporting, and unload operations on older hosts |
| `Compatibility/RuntimePlatform.cs` | Derive supported OS/architecture combinations and define the glibc/musl policy |
| `Compatibility/Utf8Marshal.cs` | Encode/decode null-terminated UTF-8 without modern `Marshal` conveniences |
| `NativeApi.cs` | Route loading, exports, RIDs, UTF-8, and Base64 through compatibility implementations |
| `Filesystem.cs` | Compile different `Stream` overrides per TFM and preserve concrete memory-based convenience methods |
| `MicrosandboxClient.cs` | Replace modern guards; keep the public factory and native-version behavior unchanged |
| `Sandbox.cs`, `SandboxHandle.cs` | Replace modern argument/disposal guards without changing lifecycle semantics |
| `Images.cs`, `Snapshots.cs`, `Ssh.cs`, `Volumes.cs` | Replace modern guards and retain existing contracts |
| `SandboxModification.cs` | Replace modern guards and `KeyValuePair` deconstruction |
| `SandboxOptions.cs` | Retain JSON behavior through the package-provided .NET Standard build of `System.Text.Json` |
| `Microsandbox.Tests.csproj` | Add target-specific test hosts instead of relying only on the current `net10.0` runner |
| `mise.toml` | Add compatibility build, package-consumer, and target-specific test tasks |

### Why not .NET Standard 2.1

.NET Standard 2.1 includes more of the memory-based and asynchronous APIs and would
reduce the filesystem-stream compatibility work. It is not implemented by .NET
Framework, however, so it does not address the most common reason to request a .NET
Standard target. If the actual requirement is only older modern .NET, directly adding
`net6.0` or retaining `net8.0` is clearer and carries a more concrete runtime contract.
Use `netstandard2.1` only when a specific Mono-family host requires it and can run the
native ABI.

## Architecture options

### Option A: replace `net8.0` with `netstandard2.0`

The entire package would contain only
`lib/netstandard2.0/Withakay.Microsandbox.dll` plus the existing RID assets.

Advantages:

- Smallest project-file surface.
- One managed implementation to reason about.
- Maximum nominal framework reach.

Disadvantages:

- Every .NET 8, 9, and 10 consumer receives the compatibility loader and older stream
  implementation.
- Modern consumers inherit extra package dependencies.
- The weakest runtime contract determines implementation quality for everyone.
- The package appears broadly portable even though native support remains narrow.

This option is feasible but not recommended.

### Option B: one multi-targeted package

The package would contain:

```text
lib/netstandard2.0/Withakay.Microsandbox.dll
lib/net8.0/Withakay.Microsandbox.dll
runtimes/linux-x64/native/libmicrosandbox_go_ffi.so
runtimes/linux-arm64/native/libmicrosandbox_go_ffi.so
runtimes/osx-arm64/native/libmicrosandbox_go_ffi.dylib
runtimes/win-x64/native/microsandbox_go_ffi.dll
runtimes/win-arm64/native/microsandbox_go_ffi.dll
```

Advantages:

- Existing .NET 8 consumers retain current behavior, performance, and dependency
  profile.
- Older consumers gain a compatibility assembly from the same package ID.
- Native assets are packed once and shared by both target-framework groups.
- No type movement or assembly-identity migration is required.
- NuGet automatically selects the best compatible managed assembly.

Disadvantages:

- Conditional code in `NativeApi.cs` and `Filesystem.cs` can drift.
- Both implementations require dedicated tests.
- Public API validation is needed to detect accidental differences between assemblies.

This option offers the best compatibility-to-risk ratio and is recommended.

### Option C: .NET Standard core plus .NET 8 wrapper

Two interpretations are possible.

#### Contracts-only core

Create `Withakay.Microsandbox.Core` targeting .NET Standard 2.0. Move native-independent
records, enums, options, events, and result types into it. Keep the current
`Withakay.Microsandbox` package as the .NET 8 operational SDK containing
`MicrosandboxClient`, `NativeApi`, services, streams, and native assets. The public
C# namespace can remain `Microsandbox` in both assemblies.

Files that currently mix contracts and behavior would be split into model and service
files. Examples include `Images.cs`, `Logs.cs`, `Metrics.cs`, `Filesystem.cs`, and
`Ssh.cs`.

Advantages:

- Other transports, tooling, serializers, or test projects can consume contracts
  without native loading.
- The operational SDK keeps its current modern implementation.
- The core has a clear and relatively small compatibility burden.

Disadvantages:

- Core-only consumers cannot create or control sandboxes.
- Moving public types changes assembly identity; binary compatibility may require
  `TypeForwardedTo` declarations.
- Two packages must be versioned and released together.
- Users may reasonably expect a package named Core to provide useful execution
  behavior when it only provides contracts.

This design is useful only if independent contract consumption is a product goal.

#### Operational transport-neutral core

Move client behavior into a .NET Standard core and define backend interfaces for
sandbox lifecycle, filesystem, logs, metrics, images, volumes, snapshots, SSH, and
agent operations. The .NET 8 wrapper would provide the native backend.

This would require replacing the direct `NativeApi` fields and constructor parameters
across most operational classes. A single `INativeApi` interface would mirror roughly
one hundred ABI methods and would be difficult to maintain; smaller capability-based
interfaces would be preferable.

Advantages:

- Enables a future RPC, remote-service, fake/test, Unity-specific, or alternative
  native backend.
- Makes domain behavior testable independently of dynamic loading.

Disadvantages:

- It is an architectural rewrite rather than a targeting change.
- The filesystem stream compromise moves into the core unless streams remain
  backend-specific.
- There is currently no second backend to validate the abstraction.
- More interfaces increase API and lifecycle complexity around handles, cancellation,
  and ownership.

This design should not be undertaken solely to add .NET Standard support.

## Recommended source layout

For the recommended multi-target design:

```text
sdk/dotnet/src/Microsandbox/
├── Compatibility/
│   ├── CompilerAttributes.cs
│   ├── Guard.cs
│   ├── NativeLibraryCompat.cs
│   ├── RuntimePlatform.cs
│   └── Utf8Marshal.cs
├── Filesystem.cs
├── NativeApi.cs
├── Microsandbox.csproj
└── ...existing files
```

Keep compatibility code internal and narrowly scoped. Prefer small abstractions around
specific unavailable framework facilities rather than broad conditional compilation
throughout the SDK. Conditional sections should be concentrated in the compatibility
files, native loading, and filesystem stream declarations.

## Packaging implications

The package ID can remain `Withakay.Microsandbox`. Multi-targeting adds a second
managed assembly but does not duplicate native assets. Package size growth should be
small relative to the native libraries.

`ValidateNativeAssets` in `Microsandbox.csproj` can continue checking the same five
files. It should execute once per pack rather than produce duplicate validation side
effects for each target framework. The release-version check must continue to compare
the staged native ABI version with `PackageVersion`.

Package inspection must verify:

- Both `lib/netstandard2.0/Withakay.Microsandbox.dll` and
  `lib/net8.0/Withakay.Microsandbox.dll` are present.
- Only the .NET Standard dependency group contains compatibility packages.
- Each RID asset appears once.
- A fresh .NET 8 consumer selects `lib/net8.0/Withakay.Microsandbox.dll`.
- A compatible older consumer selects
  `lib/netstandard2.0/Withakay.Microsandbox.dll`.
- RID-specific publish places the correct native library where the loader can find it.

## Compatibility and support matrix

| Managed host | Expected managed asset | Native execution expectation |
| --- | --- | --- |
| .NET 8, 9, or 10 | `net8.0` | Supported on the five shipped OS/architecture combinations |
| .NET 6 or 7 | `netstandard2.0` | Potentially supported; requires explicit native-loader tests |
| .NET Framework 4.7.2/4.8 x64 | `netstandard2.0` | Potentially supported on Windows x64; binding redirects may be required |
| .NET Framework AnyCPU running x86 | `netstandard2.0` | Unsupported because no win-x86 native asset exists |
| Mono on supported CPU/OS | `netstandard2.0` | Unconfirmed until loader, marshaling, and runtime behavior are tested |
| Unity/IL2CPP | `netstandard2.0` may compile | Not implied; dynamic loading and marshaled delegates require separate design/testing |
| Browser/WASM or mobile | `netstandard2.0` may restore | Unsupported by the native runtime model |
| macOS x64 or musl Linux | `netstandard2.0` | Unsupported until matching native assets are released |

The package documentation should describe this as managed compatibility plus a
smaller tested native-runtime matrix, not as universal .NET Standard support.

## Required validation

### Build and API validation

- Build each target independently from a clean restore.
- Run API compatibility checks against the current `net8.0` public surface.
- Compare the intended public API between the `netstandard2.0` and `net8.0`
  assemblies.
- Treat differences in `Stream` override metadata as reviewed exceptions.
- Enable warnings-as-errors for compatibility sources where practical.

### Unit and serialization tests

- Preserve all current unit tests against `net8.0`.
- Add a test host that is forced to consume the `netstandard2.0` assembly.
- Verify exact option JSON for null omission, explicit zero values, enums, secrets,
  patches, networking, ports, and volumes.
- Verify deserialization of every result/event model on both targets.
- Test guard exceptions and parameter names on both targets.

### Stream tests

- Read and write through concrete stream types.
- Read and write through variables typed as `Stream`.
- Exercise array-based and memory-based overloads.
- Verify cancellation before and during native operations.
- Verify synchronous disposal, asynchronous disposal, repeated disposal, EOF, and
  error paths.
- Measure allocations and throughput to quantify the compatibility cost.

### Native loading tests

- Explicit path loading.
- Environment-variable loading.
- Portable `runtimes/<rid>/native` layout.
- RID-specific publish output.
- Application-base-directory fallback.
- Missing library, missing symbol, wrong architecture, and version mismatch.
- Correct unloading after a rejected candidate.
- Deterministic rejection of unsupported OS/architecture combinations, plus a clear
  glibc-versus-musl policy.

Run native tests on at least Windows x64, Linux x64, and macOS ARM64. Linux ARM64 and
Windows ARM64 should receive native execution coverage when CI runners are available;
until then, cross-build and package inspection are insufficient to claim full support.

### Consumer-package tests

Project references do not validate NuGet asset selection. Pack the SDK and install it
into clean consumer projects for:

- `net8.0`
- `net6.0` or `net7.0`
- `net48` on Windows x64

Confirm the chosen assembly, dependency graph, native asset placement, and a real
sandbox lifecycle call.

## Proposed implementation phases and effort

### Phase 1: compile compatibility assembly — 2 to 3 engineer-days

- Multi-target the project.
- Add conditional dependencies and compiler attributes.
- Replace guard/convenience APIs.
- Add conditional filesystem stream implementations.
- Reach clean builds for both target frameworks.

### Phase 2: native compatibility layer — 2 to 4 engineer-days

- Implement native loading, symbol lookup, and unloading.
- Add RID detection and UTF-8 marshaling helpers.
- Preserve candidate fallback and version validation.
- Add focused unit tests around unsupported platforms and loader failures.

### Phase 3: package and runtime validation — 2 to 4 engineer-days

- Add target-specific test hosts.
- Add JSON and API parity tests.
- Add clean-consumer package tests.
- Validate Windows x64, Linux x64, and macOS ARM64 native execution.
- Document practical runtime support and known exclusions.

The recommended multi-target implementation is approximately 6 to 10 engineer-days
for production quality. Replacing the package with a single .NET Standard assembly is
approximately 5 to 8 engineer-days but produces a worse result for modern consumers.
A contracts-only core split is approximately 7 to 12 engineer-days. A genuinely
operational transport-neutral core is likely 15 to 25 engineer-days and should be
treated as a separate architecture initiative.

These estimates assume the native ABI remains unchanged and compatible binaries are
already available. New native platforms, Unity/AOT support, strong-name requirements,
or comprehensive Mono support would increase the scope materially.

## Decision criteria

Proceed with multi-targeting when at least one supported customer scenario requires
.NET Framework, .NET 6/7, or another host that cannot consume `net8.0`, and when CI can
exercise its actual native runtime.

Do not target .NET Standard merely to increase NuGet's compatibility badge. Without a
tested native host, the broader managed target would advertise compatibility that the
package cannot substantiate.

Create a separate core package only when there is a concrete consumer for contracts
without native execution or a planned second backend. Otherwise it adds package,
versioning, and binary-compatibility costs without solving more than multi-targeting.

## Recommendation

If broader managed compatibility becomes a product requirement, ship one
multi-targeted `Withakay.Microsandbox` package with `netstandard2.0` and `net8.0`
assets.
Concentrate compatibility code behind internal helpers, retain the current modern
implementation for `net8.0`, and describe native support separately from managed TFM
compatibility.

Do not replace the current implementation with a .NET Standard-only assembly, and do
not introduce a core/wrapper split solely for framework reach.
