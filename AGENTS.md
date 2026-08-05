# Repository guide

- This is the standalone, independently maintained .NET SDK. Do not add Rust, Cargo, or Go source-build dependencies.
- Run `dotnet restore Microsandbox.slnx`, `dotnet build Microsandbox.slnx --no-restore`, and `dotnet test Microsandbox.slnx --no-build --no-restore`.
- Run `mise run format-check` to verify CSharpier and .NET formatting before submitting changes.
- Run `mise run hooks-install` once to install the Git/prek pre-commit hook; it formats and re-stages staged C# files automatically.
- Use Worktrunk (`wt switch`, `wt list`, and `wt merge`) for repository worktrees; its project hooks run prek before commits and the full check before merges.
- Follow `docs/agents/dotnet-style-guide.md` for SDK design and testing conventions.
- Run `mise run examples-build` to compile every .NET 10 file-based example.
- Run `mise run native-download` to download, checksum, and stage all five upstream FFI release assets for the project version.
- `native-assets.sha256` must contain exactly five reviewed digests and a `version` matching the project version.
- Run `mise run native-test` to test the current platform's downloaded ABI.
- `mise run pack-local` creates a managed-only package; `mise run pack-release` requires staged assets matching the package version.
- Never commit `artifacts/`, `.runtime/`, or `src/Microsandbox/runtimes/`.
- Release tags must be `v<Version>` and matching native assets must already exist in `superradcompany/microsandbox`.
