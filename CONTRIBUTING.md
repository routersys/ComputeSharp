# Contributing to ComputeWeave

Thank you for your interest in contributing to ComputeWeave!

ComputeWeave is a fork of [ComputeSharp](https://github.com/Sergio0694/ComputeSharp) that extends it with declarative compute pipelines, resource lifetime and hazard tracking, Direct3D interoperation, synchronization, and GPU memory management.

Please read the [README](/README.md) before contributing. It describes the supported environment, public API, and guarantees provided by ComputeWeave.

## Questions, bug reports, and feature requests

If you have a question, want to report a bug, or would like to request a feature, please open an issue.

For bug reports, include:

* the expected and actual behavior;
* steps to reproduce the problem, or a minimal reproduction when possible;
* relevant environment information;
* any relevant exceptions, diagnostics, or validation output.

Small, self-contained changes such as typo fixes, documentation corrections, and trivial bug fixes may be submitted directly.

For larger bug fixes, new features, behavioral changes, public API changes, compatibility changes, or architectural changes, open a linked issue first so that the intended behavior and scope can be discussed before implementation.

## Pull requests

Keep each pull request focused on one logical change. Avoid unrelated refactoring, formatting changes, and dependency updates.

Describe what changed, why the change is necessary, and how you verified it.

Follow the established implementation pattern in the subsystem you are changing. If existing implementations use conflicting patterns, verify the intended behavior against the current contract and tests instead of copying either implementation mechanically.

Take particular care when changing:

* public APIs;
* analyzer diagnostics;
* generated descriptor formats;
* resource lifetime or hazard tracking;
* synchronization and Direct3D interoperation;
* disposal and failure handling.

Do not introduce silent fallback behavior or compatibility changes.

Lifetime tracking and hazard tracking are separate guarantees. Preserving one does not necessarily preserve the other.

Only update dependencies for a concrete reason, such as a required feature, bug fix, security fix, or compatibility requirement. Do not include unrelated dependency upgrades simply because a newer version is available.

## Code conventions

Follow the repository's [`.editorconfig`](/.editorconfig) and the conventions already established in the subsystem you are changing.

For new internal runtime code, follow the local convention for implementation comments. Do not add comments unless the surrounding code uses them for the same purpose.

Public and protected APIs must include the XML documentation required by the repository.

When modifying source generators or analyzers, preserve the documentation and comment conventions used in that area.

Preserve existing documentation comments when modifying existing code.

## Commit conventions

Keep each commit to the smallest practical logical unit, and make sure every commit builds independently.

Keep implementation changes and their verification tests in separate commits.

Do not rewrite commits that have already been merged into the default branch or referenced by a release tag.

## Building

ComputeWeave targets .NET 10.

Build the x64 solution with:

```console
dotnet build ComputeWeave.sln -c Release -p:Platform=x64
```

Use the target framework configured by each test project; do not pass a different framework with `-f`.

Do not build and test the same working tree concurrently. Doing so can replace or lock binaries while tests are running and produce invalid results.

## Testing

Changes to `src/ComputeWeave` should be verified with all four test suites:

```console
dotnet test tests/ComputeWeave.Tests.SourceGenerators/ComputeWeave.Tests.SourceGenerators.csproj -c Release -p:Platform=x64
dotnet test tests/ComputeWeave.Tests.Internals/ComputeWeave.Tests.Internals.csproj -c Release -p:Platform=x64
dotnet test tests/ComputeWeave.Tests/ComputeWeave.Tests.csproj -c Release -p:Platform=x64
dotnet test tests/ComputeWeave.Tests.DeviceLost/ComputeWeave.Tests.DeviceLost.csproj -c Release -p:Platform=x64
```

For documentation-only or otherwise isolated changes, run the verification appropriate to the affected area.

Do not evaluate a test run only by its total number of failures. Compare the failing test names and failure modes to determine whether the change introduced a regression.

Tests for resource lifetime, concurrency, synchronization, hazard tracking, disposal, and state-machine behavior must exercise the behavior they are intended to protect. When practical, temporarily break the guarded behavior to confirm that the regression test detects the failure.

Avoid tests that depend on arbitrary delays, fixed retry counts, or assumptions about how quickly asynchronous work completes. Prefer observable progress, completion signals, or explicit synchronization provided by the implementation.

If you change GPU command ordering, resource states, barriers, queue synchronization, or Direct3D interoperation, use the appropriate Direct3D debug and GPU validation facilities.

If you change a path with an established allocation contract, preserve that contract or provide evidence and justification for changing it.

If you change a runtime structure whose size is performance-sensitive, measure its managed layout with `Unsafe.SizeOf<T>()` and update the corresponding layout tests when appropriate.

If you add or modify analyzer diagnostics that produce build errors, verify the complete solution in addition to the analyzer tests.

For public API or generated descriptor changes, run the compatibility, deterministic-generation, or golden-data checks used by the affected subsystem.

## Performance changes

Do not claim a performance improvement based on a single benchmark run.

Compare the baseline and candidate repeatedly under the same hardware, driver, configuration, and power conditions. Report enough measurements to distinguish the change from normal run-to-run variation.

Run correctness validation and performance measurements separately.

## Code of Conduct

All contributors are expected to follow the repository's [Code of Conduct](/CODE_OF_CONDUCT.md).
