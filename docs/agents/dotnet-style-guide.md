# .NET SDK Style Guide

This guide records conventions already established by the SDK. Repository
configuration enforces formatting, analyzers, and warnings-as-errors; the
remaining rules describe the expected design of new code.

## Source Layout

- Use file-scoped namespaces.
- Keep `System.*` imports first and use one import group.
- Use four-space indentation in C# and two-space indentation in MSBuild files.
- Put opening braces on the following line.
- Always use braces for conditional and loop bodies.
- Use the pinned CSharpier tool for deterministic formatting; retain
  `dotnet format` for analyzer and code-style fixes that CSharpier does not
  apply.
- Prefer concise expression-bodied members for simple delegation or return logic.
- Keep implementation details internal and expose the smallest public surface.
- Name private instance fields with an underscore and camel case, such as
  `_native` or `_state`.
- Keep private properties, methods, static fields, and constants in PascalCase;
  do not use `this.` to qualify instance field access.
- Use explicit accessibility modifiers on non-interface members.
- Prefer C# keywords such as `string` and `int` over framework type aliases.
- Mark fields `readonly` whenever they are not reassigned after construction.
- Use file-scoped namespaces and place `using` directives outside namespaces.
- Use PascalCase for types and non-field members; prefix interfaces with `I`.

## Public APIs

- Use `Async` suffixes for asynchronous methods.
- Put `CancellationToken` last and default it to `default` on public async APIs.
- Use `ConfigureAwait(false)` in library implementation code.
- Validate public string inputs with framework guard APIs and reject null,
  empty, or whitespace values where the protocol requires a value.
- Use `required` for values that must be supplied by callers or protocol
  payloads.
- Prefer immutable option and result models with `init` properties or records.
- Make types `sealed` by default; leave them extensible only when inheritance
  is part of the intended API.

## JSON And Native Contracts

- Treat native JSON as an external ABI. Never rely on managed property naming
  conventions to define native names.
- Put serialization behind internal `ToJson` methods and private payload
  types where the public model does not exactly match the wire contract.
- Use explicit `JsonPropertyName` attributes for native fields.
- Preserve omission semantics: default-valued optional fields are omitted, and
  protocol fields that require explicit zero or false values opt into writing
  those defaults.
- Serialize protocol enums using the established lowercase snake-case format.
- Convert time values explicitly at the boundary, using the protocol unit and
  rounding behavior rather than implicit conversions.
- Keep native byte encoding and decoding at the interop boundary; expose byte
  arrays to managed callers.

## Native Handles And Disposal

- Represent native ownership with `IAsyncDisposable` when cleanup crosses the
  native or asynchronous boundary.
- Make handle ownership explicit. Consume handles atomically and treat zero as
  consumed or unavailable.
- Throw `ObjectDisposedException` when an operation needs a consumed handle.
- Serialize concurrent close or completion operations.
- Mark consuming ownership as gone before invoking native cleanup; retryable
  completion should remain open until native cleanup succeeds.
- Once ownership has been consumed, perform final native cleanup with
  `CancellationToken.None`.
- Use `await using` for SDK resources in examples and application-facing code.

## Tests

- Use TUnit `[Test]` methods with behavior-oriented names.
- Test wire contracts directly: exact property names, omitted fields, explicit
  zero and false values, enum strings, nullable fields, and byte encoding.
- Add tests for cancellation, concurrency, repeated disposal, handle ownership,
  and retry behavior when changing stateful native wrappers.
- Gate native integration tests on an explicitly configured native library;
  managed contract tests must not silently require native assets.
- Prefer focused assertions that explain the contract being protected.

## Examples

- File-based examples start with a `#:project` reference to the local SDK.
- Use bounded cancellation, unique sandbox names, `try/finally`, and
  best-effort cleanup.
- Keep examples runnable and focused on one SDK capability.

## Native Assets

- Download and checksum native assets through the repository scripts.
- Match native asset versions to the managed package version.
- Do not commit generated native runtime directories or package artifacts.

## Deliberate Non-Rules

- Do not require XML documentation on every member yet; coverage is strong in
  core public types but not universal.
- Do not require every public type to be sealed; some models intentionally allow
  inheritance.
