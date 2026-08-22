# Contributing to ComputeWeave

Thank you for your interest in contributing to ComputeWeave.

ComputeWeave is a fork of [ComputeSharp](https://github.com/Sergio0694/ComputeSharp) that adds declarative compute pipelines, resource lifetime and hazard tracking, Direct3D interoperation, synchronization, and deterministic GPU memory management. Please read the [README](/README.md) first: it states what the library guarantees and, just as importantly, what it does not.

This guide is long because the runtime is unforgiving. A leaked handle, an inverted condition, a duplicated signal or a mistaken vtable slot does not announce itself; it surfaces in one run out of forty, on someone else's adapter, in production. Many of the rules below exist because their absence produced a defect that reached a released version.

You do not need to read all of it. Start with [Getting Started](#getting-started); read [The Runtime Model](#the-runtime-model) and [Engineering Rules](#engineering-rules) when your change touches the runtime.

---

## Table of Contents

1. [Getting Started](#getting-started)
   - [Environment](#environment)
   - [Build and test](#build-and-test)
   - [Your first change](#your-first-change)
2. [How Behavior Is Defined](#how-behavior-is-defined)
3. [Issues](#issues)
4. [Pull Requests](#pull-requests)
   - [What the template asks for](#what-the-template-asks-for)
   - [Automated checks](#automated-checks)
   - [Review, merge and licensing](#review-merge-and-licensing)
   - [Language](#language)
5. [The Runtime Model](#the-runtime-model)
   - [One contract chain](#one-contract-chain)
   - [One source of truth per fact](#one-source-of-truth-per-fact)
   - [Identity, ordinals and exhaustion](#identity-ordinals-and-exhaustion)
   - [State machines and reserved capacity](#state-machines-and-reserved-capacity)
   - [Queues, barriers and copies](#queues-barriers-and-copies)
   - [Interoperation and maintenance](#interoperation-and-maintenance)
   - [Memory](#memory)
   - [Three layers of guarantee](#three-layers-of-guarantee)
   - [Ownership](#ownership)
6. [Engineering Rules](#engineering-rules)
   - [Contracts come first](#contracts-come-first)
   - [Keep separate guarantees separate](#keep-separate-guarantees-separate)
   - [Exclusion and ordering](#exclusion-and-ordering)
   - [Reserve before you act](#reserve-before-you-act)
   - [Failure is classified](#failure-is-classified)
   - [Refuse, do not wait](#refuse-do-not-wait)
   - [Ownership and unwinding](#ownership-and-unwinding)
   - [Public API and diagnostics](#public-api-and-diagnostics)
   - [Allocation contracts](#allocation-contracts)
   - [Deterministic generators](#deterministic-generators)
   - [Native bindings](#native-bindings)
7. [Code Conventions](#code-conventions)
8. [Commit Conventions](#commit-conventions)
9. [Building](#building)
10. [Testing](#testing)
    - [Test suites](#test-suites)
    - [Continuous integration](#continuous-integration)
    - [What a test must prove](#what-a-test-must-prove)
    - [Reading a test run](#reading-a-test-run)
    - [Known baseline](#known-baseline)
    - [Other verification](#other-verification)
11. [Evidence](#evidence)
12. [Performance Changes](#performance-changes)
13. [Upstream Divergence](#upstream-divergence)
14. [Code of Conduct](#code-of-conduct)

---

## Getting Started

### Environment

| Item | Requirement |
|---|---|
| OS | Windows 10 or later, 64-bit |
| SDK | .NET 10 |
| GPU | A Direct3D 12 device at feature level `11_0` and shader model `6_0`; a WARP device is used when none is present |
| Interoperation | Shared-texture work needs an adapter that can create shared handles for both Direct3D 11 and Direct3D 12 |

### Build and test

```console
dotnet build ComputeWeave.sln -c Release -p:Platform=x64
dotnet test tests/ComputeWeave.Tests.SourceGenerators/ComputeWeave.Tests.SourceGenerators.csproj -c Release -p:Platform=x64
dotnet test tests/ComputeWeave.Tests.Internals/ComputeWeave.Tests.Internals.csproj -c Release -p:Platform=x64
dotnet test tests/ComputeWeave.Tests/ComputeWeave.Tests.csproj -c Release -p:Platform=x64
dotnet test tests/ComputeWeave.Tests.DeviceLost/ComputeWeave.Tests.DeviceLost.csproj -c Release -p:Platform=x64
```

`-p:Platform=x64` is not optional. See [Building](#building).

### Your first change

Typo fixes, documentation corrections and trivial bug fixes can go straight to a pull request. Larger bug fixes, new features, behavioral changes, public API changes, compatibility changes and architectural changes need an issue first, so that the intended behavior and scope are agreed before implementation.

Fill in the pull request template. An automated comment will tell you which suites your change needs; it is not a review.

---

## How Behavior Is Defined

The observable behavior of ComputeWeave — its public API surface, state transitions, linearization order, failure results, capacity formulas, data layouts and diagnostic identifiers — is defined by a set of normative specifications maintained by the project. Those specifications are the source of truth; the implementation, the tests and this document follow them.

The specifications are not published in this repository, and most contributions do not need them. You do need them, or a maintainer's confirmation, whenever a change would alter a documented contract. If you are unsure whether your change touches a contract, open an issue and ask before implementing it. That is the intended path, not a fallback.

Five rules govern how those documents are read and kept honest. They apply to this guide as well.

**Precedence is fixed.** When two statements conflict, the more structural one wins, in this order: normative invariants; state transition tables; linearization and trace protocols; internal data contracts; the resource-state and hazard commit protocol; structural capacity and backpressure rules; the public API reference surface; explicit MUST and MUST NOT statements; numbered algorithms; ordinary prose; reference implementations; and last, examples and diagrams.

**Nothing is inferred from an example.** Fallback, retry, waiting, allocation and ownership transfer are never implied by a code sample, a diagram or an existing call site. If a behavior is not required by the normative text, adding it silently is a change of contract.

**Conformance is a checklist, not an impression.** Every MUST satisfied; every MUST NOT avoided; every invariant mapped to a test; no state transition outside the transition tables; no linearization order other than the specified one; the declared public API and data layouts matched exactly; capacity formulas unchanged; generator, runtime and descriptor schema identical; zero errors under the Direct3D 12 debug layer and GPU-based validation.

**The specification never lags the implementation.** A change that alters a contract is incomplete until the specification is amended in the same body of work. A specification that describes something the code no longer does is worse than none.

**Open questions belong to the maintainers.** Items recorded as awaiting a decision are not an invitation to decide them in a pull request. Do not implement them unilaterally, and do not widen scope to cover them.

Where the specification says SHOULD and you have reason to deviate, record the reason, the safety argument and the measurement.

---

## Issues

Open an issue. Templates are available in English and Japanese.

For bug reports, include:

- the expected and actual behavior;
- steps to reproduce, or a minimal reproduction when possible;
- the adapter, the driver version, and whether the adapter is discrete, integrated or WARP;
- any exception, diagnostic or validation output, including the diagnostic identifier when the exception carries one.

Intermittent failures are worth reporting without a reliable reproduction. State how often the failure occurs and out of how many runs, and whether it appears in a warm process or only in a freshly started one. A window that a warm process closes can be wide open in a cold one, so that ratio is often the most useful part of the report.

---

## Pull Requests

Keep each pull request to one logical change. Avoid unrelated refactoring, formatting changes and dependency updates; update a dependency only for a concrete reason, such as a required feature, a bug fix, a security fix or a compatibility requirement.

Follow the implementation pattern already established in the subsystem you are changing. If existing implementations disagree, verify the intended behavior against the current contract and tests instead of copying either one mechanically.

Do not introduce silent fallback behavior or a compatibility shim. Unsupported input is rejected with an analyzer error or an explicit exception, never quietly accommodated, and compatibility is introduced only as a deliberate contract change.

Take particular care when changing:

- public APIs, or anything that alters the code a consumer's compiler generates;
- analyzer diagnostics and runtime diagnostic identifiers;
- the descriptor format, canonical ordering or contract hash;
- resource lifetime, ownership, generations or hazard tracking;
- synchronization, queue submission, interoperation or maintenance;
- memory admission, policy, accounting or trimming;
- native bindings and vtable slot numbers;
- disposal, teardown and failure handling.

### What the template asks for

The template asks for a summary, a linked issue, the kind of change, the observable behavior change and verification. Two of its questions carry more weight than the rest.

**“Behaviour change”.** What an existing caller would observe differently, including exception types and the timing of failures. Write "none" when nothing observable changes; do not leave it blank.

**“Verification”.** The results you actually ran, and how they compare with the same suites before your change. Do not judge a run by its total failure count; see [Reading a test run](#reading-a-test-run).

Throughout, distinguish what you observed, what you derived from reading the code, and what you assume. A defect found by reading code is a real finding — say that it has not been reproduced. Never present analysis as observation. And say what you did not do: if part of the work was skipped, blocked or left unverified, state it plainly rather than letting a passing suite imply completeness.

### Automated checks

Opening a pull request triggers an automated check that posts a single comment and keeps it updated. It reads only pull request metadata through the API and never checks out the branch, so a fork's code is never executed with write permissions.

It reports which test suites the changed paths require, which guarded areas were touched, whether implementation and tests share a commit, whether the template sections are filled in, and whether an issue is linked.

It is not a review, and its findings are input to a decision, not obligations. If a finding does not apply to your change, say so in the thread.

### Review, merge and licensing

A maintainer reviews and merges. The [commit conventions](#commit-conventions) below apply to the commits in your pull request.

Contributions are accepted under the repository's MIT [LICENSE](/LICENSE). By opening a pull request you agree that your contribution is licensed under those terms.

### Language

Issues and pull requests are welcome in English or Japanese; the templates carry both. Code, identifiers and XML documentation follow the conventions of the surrounding code. Commit subjects follow [the repository convention](#commit-conventions).

---

## The Runtime Model

You do not need to know the whole runtime to contribute. You do need to know which of these structures your change is standing on, because most of the rules that follow are consequences of them.

### One contract chain

Everything from an attribute in user code to the release of a GPU resource is a single chain, and each link has exactly one representation.

```text
Attribute contract
  → canonical compile-time contract model
  → canonical binary descriptor          (the only generator/runtime ABI)
  → exact generated resource plan
  → generated typed wrapper
  → recording lease                      (pins the generations being recorded)
  → observed access                      (what the submission actually touched)
  → hazard / interop / lifetime plan
  → execution issued                     (ExecuteCommandLists returned)
  → completion fence
  → commit and publish                   (+ re-read the completed value)
  → retention release                    (resources, allocators, lists, metadata)
```

Consequences that bind every change:

- The descriptor is the **only** intermediate schema between the generator and the runtime. The runtime performs no attribute reflection and resolves no type from a name.
- Resources are constructed only from the exact generated plan. Plan equality is a full field-by-field comparison; a hash may narrow a comparison but never decide it.
- Access declared by contract bounds access observed at runtime. A resource bound to a shader as read-write is observed as read-write, never narrowed by inspecting shader bodies or by self-declaration.
- Schema versions must match exactly. Never infer forward or backward compatibility; introducing it is a separate, deliberate contract change.

### One source of truth per fact

Duplicated state is the defect this codebase is most careful about.

| Fact | Sole owner |
|---|---|
| Generation lifetime, ownership, resident state, fences, reference counts | the generation record embedded in the resource object |
| Active, prepared and retired handles; binding epoch; dispose request | the slot control record |
| Logical plan and physical capacity | the slot plan state |
| Public API shape | the public API reference surface in the specification |
| Generator and runtime ABI | the canonical descriptor |

Do not add a parallel state object, a per-generation managed entry, a redundant flag, or a second place a value can be read from. Validity is derived from existing state — a plan is valid because the handle it belongs to is non-empty — rather than tracked by a new flag. Where several states share one word, every write is a compare-exchange confined to its own bits, so that neighbors cannot lose updates.

### Identity, ordinals and exhaustion

- Ordinals are 0-based and dense. Runtime identities are 1-based, with 0 reserved for "none".
- Identities are never reused: not generation identities, not registration identities, not tokens, not fence values, not epochs, not policy versions.
- A generation identity is immutable for the life of the generation. Replacing the active generation of a slot advances the binding epoch; it does not renumber generations.
- Exhaustion or overflow of a monotonic sequence is terminal for its scope, never a wrap and never a reuse.
- A stale identity is how a late arrival learns it is stale. That is why identities are compared, under the correct exclusion, before any counter is touched.

### State machines and reserved capacity

Lifetime is not managed ad hoc. Slots, generations, submissions, registrations, maintenance records, domains and external ownership each have an explicit state machine, and a transition that does not appear in its table is not permitted — including one that looks harmless.

Capacity is reserved, never grown.

- A host or resource set reserves its entire structural baseline before it is published: recording bundles, pending records, usage sets, command list entries, usage entry storage, prepared and deferred generation records, maintenance records, the persistent lease limit and plan scalar storage. Every figure is derived from the descriptor with checked arithmetic, and a partial reservation is never published.
- Capacity returns only when every completion condition holds, not when `Dispose()` is called. `Dispose()` is idempotent and non-blocking: it rejects new work and requests retirement. Only `WaitForDisposal()` waits.
- Descriptor lengths and table counts are bounded, and the bounds are checked before arrays are allocated and before the contract hash is computed. Rejecting late is rejecting after the damage.

### Queues, barriers and copies

- Generated pipelines submit to the compute queue only. Copies inside a generated pipeline are recorded onto a compute command list.
- A submission is up to three segments: prologue, body, epilogue. The prologue performs cross-queue waits and transitions, the body is exactly what the caller recorded, and the epilogue returns shared textures to `COMMON`. An empty prologue or epilogue produces no command list at all. The runtime never reorders the body or infers shader semantics from it.
- For a manual copy, the target queue is decided only from values that cannot change during the submission: the resident state of every tracked resource, and its dimension. All resources resident in `COMMON` and no 3D texture involved means the copy queue; anything else means the compute queue.
- **A command list submitted to the copy queue records no transition barrier.** Subresources used on a copy queue must be in `COMMON`; promotion to copy source or copy destination is implicit, and decay returns them. Recording a barrier there is reported as an error by the debug layer.
- **3D textures always go to the compute queue**, whatever their resident state. Some copy engines ignore the Z component of the source box and write source slice 0 into every destination slice. There is no exception and no debug-layer error; the pixels are simply wrong. This was measured, not deduced.

### Interoperation and maintenance

- An interoperation round trip acquires one domain operation lease — domain reference, then domain permit, then scheduler reservation — and holds the reservation for the whole transaction, from the provider's signal to the provider's wait. It never waits for GPU completion.
- Hazard commit and ownership commit are separate steps. Ownership returns to the external side only after the provider's wait has actually succeeded.
- External GPU work is enqueued only inside the scope obtained from the borrow or the lease that owns the view.
- A persistent lease fixes both the generation and its physical dimensions inside one critical section, and those dimensions never change afterwards, even when the slot's active generation is replaced.
- The final drain of a retired shared generation is not performed by whoever requested it. The requester transitions the maintenance record and wakes the coordinator; a bounded maintenance coordinator performs the work, scanning records in canonical order, never waiting on the CPU and never polling.
- A temporary shared handle is owned by the runtime alone and closed in a `finally` as soon as the call that consumes it returns.

### Memory

- Segment mapping follows the device architecture. Inactive segments are never queried and never receive a grant request.
- Only committed resources are used. A resource group's allocation size is the checked sum of per-member queries, never a single aggregate query.
- Admission judges the active segment against the DXGI budget, the broker grant when one is configured, and any explicit hard limit, all with checked arithmetic.
- Every resource declares a recovery class: discardable, recreate-from-host, recompute or capacity-only. Trim proceeds in a fixed phase order — retired-ready, managed pool surplus, then idle generations by recovery class — and within a phase by least recent use, then largest reclaimable size, then resource identity. Idle means no recording, pending, external, CPU or native reference and no persistent lease.
- Selection and detachment are not one critical section, so a trim candidate is re-checked under its own gate before it is detached.

### Three layers of guarantee

Lifetime tracking answers whether a native resource may be released. Hazard tracking answers whether accesses to it are correctly ordered across queues. They are independent, and preserving one does not preserve the other.

| Layer | Lifetime | Hazard |
|---|---|---|
| Pipelines, `ComputeContext`, copy paths, interop domains | tracked | tracked |
| Tracked native references | tracked | not tracked |
| Raw native escape hatches | not tracked | not tracked |

The [README](/README.md) publishes this table to users; it is public contract. Do not blur the boundaries for convenience, do not present one layer as a substitute for another, and do not delete an escape hatch that users depend on — document its limits and leave it in place.

Where the runtime cannot guarantee ordering, it still supplies the material a caller needs to establish ordering itself, and says so in the documentation. Supplying material is not the same as making a promise.

### Ownership

- A caller passing a provider into domain registration transfers ownership at method entry. On success the domain owns it; on failure the runtime disposes it exactly once. The caller must not dispose it afterwards.
- A provider never disposes the scheduler and never closes a shared handle it was given.
- Providers, materializers and callbacks never re-enter the runtime, and never observe internal staging.
- A generation owner owns its native resources, managed resources, allocation descriptors, embedded records and accounting as one unit. Either every member is constructed and the owner is published, or every constructed member is released in reverse order and nothing is published.
- Borrowed resources are released by their reference tracker, not by generation reference counts. Code that must keep a borrowed resource alive holds the lease, not just the count.

---

## Engineering Rules

### Contracts come first

- Amend the specification in the same work that changes the contract; never leave one ahead of the other.
- Do not add a public API without a demonstrated need. "A caller might want it" is not one.
- Do not silently coerce invalid input. An out-of-range ordinal fails; it does not resolve to index 0 because the owner happens to hold a single resource. Silent coercion turns a caller's mistake into a write to the wrong resource.
- Keep the access contract in one place, the descriptor. A materializer declares shape and dimensions, never access.

### Keep separate guarantees separate

- Lifetime and hazard are different guarantees; see [Three layers of guarantee](#three-layers-of-guarantee).
- Lifetime and memory policy are different questions. Lifetime decides whether a resource is still in use; policy decides whether an unused but recoverable resource is kept.
- A mechanism that prevents a duplicate *request* is not a mechanism that prevents duplicate *execution*. Name them differently, document them separately, and never cite one as evidence for the other. Conflating the two is what produced a duplicated external signal that shipped.

### Exclusion and ordering

The runtime holds a fixed lock order. Acquire in this order and release in reverse.

```text
invocation permit
  → pending record reservation
  → slot gate (short)
  → domain permit
  → scheduler reservation
  → hazard gate
  → compute queue gate
  → completion registry gate
```

- Never hold a hazard, queue or completion gate across a provider call.
- Never hold a registration, policy or allocation gate across a native allocation, a provider callback or a COM release.
- Every check-then-increment on a reference counter happens under the exclusion that owns that counter: the slot gate, the hazard gate, or the resource's reference-tracker lease, depending on the reference kind. Verifying outside the exclusion and incrementing inside it is the same bug written twice.
- Keep critical sections short and do the expensive work outside them. Snapshot under the gate, then wait, map, allocate or call out.

### Reserve before you act

Work submitted to a GPU queue or an external queue cannot be recalled, and neither can an allocation that has already been published.

- Acquire the right to perform an irreversible effect **before** performing it, inside the same critical section that decides to perform it. Never rely on advancing state afterwards to suppress a duplicate: two passes that both decide, then both act, and both signal.
- Make acquisition and release symmetric. Every path that succeeds acquires, and release happens in a `finally`. An asymmetric path does not merely fail to protect itself; it releases a right another thread is holding.
- Re-validate, inside the deciding critical section, any precondition read outside of it. A decision made on stale evidence is a decision to act on something already released.
- Reserve the pending submission record before recording, not after. Recording that cannot be published is wasted work at best and an inconsistent ledger at worst.
- Size and admit before you create: gather every member's allocation requirement, admit the checked total, then create native resources. Never create first and account afterwards.
- After publishing a pending record, re-read the queue's completed value. A fence that completed during publication must not be lost.
- Retention — resource leases, allocators, command lists, usage sets, interop metadata — is released as one unit after completion, never piecemeal.
- Once execution has been issued there is no clean abort. Design the failure path around that, not against it.

### Failure is classified

Every failure has a scope, and the scope determines the response.

| Class | Scope | Response |
|---|---|---|
| Contract error | one call | reject with a diagnostic identifier; state unchanged |
| Resource unavailable | one call | reject, or report backpressure without an exception |
| Domain poison | one interoperation domain | poison, fault the affected generations, converge teardown |
| Device terminal | one device | reject new work, fault outstanding submissions, retain for teardown |
| Internal invariant | a bug in the runtime | throw with an identifier; never blame the caller |

Fixed mappings you must not reinterpret:

- A Direct3D 12 queue wait or signal failure, and the loss of a completion proof, are device-terminal.
- A provider signal or flush failure before execution is issued poisons the domain and leaves nothing submitted.
- A provider wait failure after the completion signal poisons the domain and faults the generation, but does not terminate the device.
- Timeline or token exhaustion poisons the domain; device-scoped sequence exhaustion terminates the device.
- An allocation-descriptor error is a plan validation failure, not a memory shortage. Do not trim, do not retry, do not create the resource.
- A failed COM call did nothing. Unwind to the state that preceded it.

Releasing a faulted or terminally retained object requires the matching authority: normal completion, domain teardown or device teardown. Recording the authority and checking it are a single atomic step, so that a different authority cannot complete a release it did not start.

Failures must arrive where something reads them. A diagnostic that no code path consumes is equivalent to silence, and some teardown paths cannot even begin until the observable failure state is set — which is how a faulted record once held an external view forever.

### Refuse, do not wait

Backpressure is explicit and non-blocking. The runtime does not hide contention behind a wait.

- Exhausted invocation slots, exhausted pending records, and a busy or re-entered scheduler are rejections carrying a diagnostic identifier.
- A pending retired generation blocks a replacement by returning `false`, not by throwing and not by waiting.
- Unavailable memory is either `false` from a try-style API or an allocation exception, according to the calling path.
- Maintenance never performs a CPU wait and never polls. A pass that cannot take the right to run leaves the record alone and returns; progress resumes when a completion, a release or a fence wakes the coordinator.
- Do not spin, sleep, expand a pool, force a collection, borrow another owner's capacity, or wait on a fence to reclaim one. The single narrow exception in the whole runtime is specified explicitly; do not add a second one.
- Do not make re-entrancy legal by recognizing the owning thread. Nested execution reproduces exactly the duplication the exclusion was introduced to prevent.
- When work is concentrated into a single executor, isolate failure in the same change. One faulting participant must not stop maintenance for everyone else sharing that executor — a lesson learned when a single provider fault disabled a whole device.

### Ownership and unwinding

- Every native allocation and pinned handle has exactly one owner, and that owner releases it on every path, including every failure path. Two owners are not permitted, and an "is allocated" flag on a copied handle is not a defense against double release.
- Unregister a wait before closing the object it waits on, and release the callback context only after unregistration completes. Closing a handle a registered wait still references is undefined behavior; from inside the callback itself, unregistration must use the non-blocking form, or it waits for itself.
- Order every unwind so that resources you own are released first and hand-backs to other components come last. If a hand-back throws, your own resources must already be safe and the original exception must not be replaced.
- Release a partially constructed object in reverse order of construction, directly. An object that was never published has no lifecycle machinery watching it; handing it to promotion conditions leaks it permanently.
- Retry only genuinely transient conditions, and classify the condition before retrying. Retrying a permanent failure turns a fast error into a slow one and hides its cause.
- A COM object written in this repository must implement `QueryInterface`, `AddRef` and `Release` faithfully, release only at zero, and answer only for the interfaces its vtable actually fills. Answering for an interface whose slots are empty is worse than returning `E_NOINTERFACE`.
- A managed exception must never cross an unmanaged frame; it terminates the process. Catch inside the boundary and map onto the failure value the native contract defines.
- Do not operate queues, providers, schedulers, fences, registrations or policies from a finalizer.
- Make the difference between a borrowed pointer and a transferred reference explicit in the name of the API, not only in its documentation. Callers hand pointers to bindings that release whatever they are given, whether or not the documentation asked them not to.

### Public API and diagnostics

- Add the minimum, and follow the established naming: `Compute…` for types the consumer holds, `External…` for values and views describing the external side, `Direct3D11…` and `Direct3D12…` for API-specific common types. Internal bindings keep the SDK spelling; that is not public surface.
- After a change that could affect the surface, enumerate the public and protected members of the built assembly and confirm that nothing was exposed unintentionally.
- Give every enum member you add an explicit numeric value, and reject unknown values during validation. A few public enums inherited from upstream predate this rule; they are not precedent.
- Document, on the API itself, every contract the type system cannot enforce: a borrowed pointer, a scope that must be disposed, a value that goes stale when a generation is replaced, an ordering the runtime does not guarantee, or a cost a caller would not expect.
- Do not mark an API obsolete that the project intends to keep. It tells users a falsehood and breaks builds that treat warnings as errors.
- **Do not add external dependencies.** Interoperation surfaces are exactly where an assembly identity conflict becomes an application the host cannot load. Prefer a small self-contained binding to a convenient package.
- Verify a capability at the point the requirement becomes real, not earlier. Refusing at publication time rejects callers who would never have needed the capability.
- Refuse before exposing anything. When construction fails a precondition, release what was acquired in reverse order and throw without publishing a partially built object.
- Diagnostic identifiers are a stable public contract. Never reuse a retired identifier, change what one means, or alter its severity silently. Every rejection the specification names carries its identifier on the exception, so that a caller can tell one rejection from another without parsing a message.
- An identifier that cannot currently be reached stays in the table with the reason recorded. Deleting it invites the number to be reused later.
- Diagnostics detect contract violations. Deliberate, documented use of a low-level API is not a violation, and a diagnostic for it produces permanent noise instead of safety.

### Allocation contracts

Once warmed up, the no-resize generated pipeline path holds to zero: zero managed allocation per call, zero full registry scans in a normal frame, zero per-submission collections, zero per-resource or per-generation managed tracking objects, zero implicit CPU waits, zero empty prologue or epilogue command lists, zero dynamic pool growth, no unbounded retired generations, and no speculative allocation that has not passed admission.

- Interoperation boundaries can be called every frame. Acquisition and release on those paths allocate nothing and introduce no finalizable type; a short-lived finalizable object is promoted and needs two collections to be reclaimed.
- Hot paths do not use LINQ, per-call collections, reflection, stack-trace capture, full registry scans, wall-clock timestamps or forced collection.
- Pools provide exactly the reserved structural capacity. Exhaustion is backpressure, not growth.
- Descriptors are allocated with the generation that owns them, not per submission.
- If you change a path with a documented allocation or layout budget, either preserve it or present the evidence for changing it. Measure managed layout with `Unsafe.SizeOf<T>()` and update the layout tests when appropriate.

### Deterministic generators

- Implement incremental generators with stateless instances, equatable immutable models and deterministic hint names. No reflection, no arbitrary delegate factories, no hot-path allocation in generated code.
- Order everything canonically by metadata name, never by source path, syntax discovery order or dictionary enumeration order. Two identical canonical signatures are a build error; they are never disambiguated by source position.
- Normalize strings to NFC and compare ordinally. Culture-sensitive and case-insensitive comparisons are forbidden in canonical data.
- Compute the contract hash over the declared field order, keep all 32 bytes, and never truncate.
- Generated output must be byte-identical between a clean build and an incremental one.
- The runtime validates what the generator produced instead of trusting it. Keep the formatter and the validator independent, or a single mistake becomes self-consistent.

### Native bindings

A wrong vtable slot number compiles cleanly and calls a different function. The type system will not catch it, and neither will most tests.

- Confirm every slot number against at least two independent sources — for example the headers of two Windows SDK versions, and the IL of the binding this one derives from — and record where the confirmation came from.
- Read slot numbers from IL, not from attributes. Debug-only annotations are absent in release builds, so a test that reflects over them passes vacuously.
- Write expected values as literals in the test. A test that reads a value from the implementation and compares it against the implementation verifies nothing.
- Bindings hold no COM lifetime; the caller balances `AddRef` and `Release`.
- Keep bindings internal, declare only the members actually used, and add no dependency to obtain them.

---

## Code Conventions

Follow the repository's [`.editorconfig`](/.editorconfig) and the conventions already established in the subsystem you are changing.

The build treats warnings as errors and enforces code style. Do not work around either; fix the underlying issue.

For new internal runtime code, follow the local convention for implementation comments. Do not add comments unless the surrounding code uses them for the same purpose.

Public and protected APIs must carry the XML documentation the repository requires, including the contracts named in [Public API and diagnostics](#public-api-and-diagnostics). Preserve existing documentation comments when modifying existing code, and keep the documentation and comment conventions of the source generators and analyzers when working there.

---

## Commit Conventions

- Keep each commit to the smallest practical logical unit, and make sure every commit builds on its own.
- Keep implementation changes and their verification tests in separate commits.
- Do not mix a local, easily reverted fix with a change that alters observable behavior. They must remain separable afterwards, because one of them may need to be reverted alone.
- Commit subjects in this repository are written in Japanese, are 50 characters or fewer, and have no message body. If you cannot write the subject in Japanese, say so in the pull request.
- Do not rewrite commits already merged into the default branch or referenced by a release tag.

---

## Building

ComputeWeave targets .NET 10. The runtime, the DXC and allocator packages, and every test project all target `net10.0`; the Roslyn components — the two source generators and the code fixers — target `netstandard2.0`, because that is what the compiler loads. No project is multi-targeted, so do not pass a framework with `-f`.

```console
dotnet build ComputeWeave.sln -c Release -p:Platform=x64
```

The platform argument is required. Without it the solution builds the first platform it lists, which is ARM64.

- A solution build and a test run do not necessarily produce and consume the same output directory. Verify a change by building the affected projects individually with `-p:Platform=x64`, and confirm that the assemblies under test are the ones you just built. Output timestamps and reported test counts are the practical indicators. Testing yesterday's binaries and reading the result as "no regression" is a mistake that has already been made here.
- Do not build and test the same working tree concurrently. Doing so can replace or lock binaries mid-run and produce invalid results.
- Do not pass `-p:NoWarn=…` on the command line; it becomes a global property and overrides the repository's own warning configuration.
- Do not finish a verification run with `-p:EnforceCodeStyleInBuild=false` still set; it hides build-breaking style violations.
- Read the build output for errors explicitly. Do not infer success from the absence of an obvious failure message.

---

## Testing

### Test suites

| Project | Role | Device |
|---|---|---|
| `ComputeWeave.Tests.SourceGenerators` | generator and analyzer behavior | none |
| `ComputeWeave.Tests.Internals` | state machines, exclusion, structural and IL-level rules | many tests use a shared real device |
| `ComputeWeave.Tests` | end-to-end behavior, including image comparison | real device |
| `ComputeWeave.Tests.DeviceLost` | device removal; runs in its own process because it destroys a device | real device |
| `ComputeWeave.Tests.DebugLayer` | Direct3D 12 debug layer and GPU-based validation, enabled process-wide before any device exists | real device |

Many tests in the internals suite run against a **shared** device — the harness resolves one per process and hands the same instance to every test — so never inject a fault into it. Poisoning or terminating a shared device takes every later test in the process down with it, and the resulting cascade looks like a regression in code you did not touch.

Device-backed tests are parameterized by adapter. The harness resolves one hardware-accelerated adapter and one WARP adapter; when no hardware-accelerated adapter is present, the variants that target it are reported as inconclusive rather than failed, while the WARP variants still run. A WARP-only run therefore cannot confirm or refute a failure that occurs only on hardware.

The debug-layer suite is where an illegal barrier or an incompatible layout is reported as an error instead of as silently wrong pixels. Its info queue is flushed at the next assertion, so a single process must exercise a single path; otherwise a message from one path is attributed to the next.

### Continuous integration

CI ([`.github/workflows/ci.yml`](/.github/workflows/ci.yml)) builds the solution and runs four suites: source generators, internals, the main suite, and device-lost in a separate job. The debug-layer suite is **not** run by CI. Run it locally whenever you change command ordering, resource states, barriers, queue selection or interoperation; nothing else will catch those defects.

For changes to `src/ComputeWeave`, run all five. For documentation-only or otherwise isolated changes, run the verification appropriate to the affected area.

If you could not run a suite — no adapter of the required kind, no Windows machine, a run that never finished — say so in the pull request and say why. A stated gap is information; silence reads as a claim you never verified.

### What a test must prove

- Every invariant a change adds or relies on is mapped to a test that fails when the invariant is broken.
- **Verify the test by breaking the implementation.** Temporarily revert the guarantee, confirm the test fails, then restore it in the same session. A regression test that survives the mutation it was written to catch is not protection.
- Break the implementation only after committing it. Losing an uncommitted implementation to a checkout is a mistake this project has already made.
- Prefer a failure that is *named*: detected by the specific test written for it, not by a distant assertion elsewhere. A break detected only in aggregate does not tell the next contributor what they broke.
- Some detections legitimately appear as a hang or a crash rather than an assertion failure — a lost exclusion stalls a wait forever, and a driver drops a divergent GPU state immediately. Run with `--blame-hang` so the responsible test is identified, and delete the generated `TestResults` afterwards.
- Never assert on elapsed time, fixed retry counts, or how quickly asynchronous work completes. Wait on observable progress, completion signals or explicit synchronization provided by the implementation. When observing an asynchronous actor, never wait by looping a fixed number of times.
- Where an invariant is structural — "this caller may never reach that operation" — freeze the structure by analyzing the call graph of the built assembly, and assert a known-reachable path in the same test so the check cannot pass vacuously.
- Assert against literals written in the test. Reading the expected value from the implementation verifies nothing.
- Reason about coverage by member, not by type. A type name appearing somewhere in the suite says nothing about which of its members has ever executed.
- Check what the test actually observes. A test meant to prove that a resource outlived its owner, but which observes a reference the test itself holds, proves only that COM works.

### Reading a test run

- **Compare failing test names, not totals.** Counts grow as tests are added, and a total that stays the same can still hide a swap.
- Confirm the total count and the elapsed time, not just the summary line. A run that executes a fraction of the tests can still report success.
- When repeating a run, write the output to a file. Reading only summary lines loses the names that matter; a failure that appeared once and was never identified was lost exactly that way.
- Distinguish failures caused by resource exhaustion from failures caused by logic. Out-of-memory device creation, a `TryEnsure` returning false and a failed mid-size allocation are allocation failures, not logic failures. A run several times slower than usual is the signal: shut down build servers, re-run from a clean state, and do not adopt the results of the slow run.
- Non-deterministic failures need repetition, and repetition alone is not enough: a warm process can close the very window a cold process opens. Repeat the full suite, and separately repeat the focused test in freshly started processes: `dotnet test <project> -c Release -p:Platform=x64 --filter "FullyQualifiedName~<name>"`, invoked once per run so that each repetition starts cold.

### Known baseline

These values were measured on 2026-08-22 with the commands above, on a machine where both a hardware-accelerated adapter and WARP are present. They are a starting point for comparison, not a target: counts change as tests are added, and results depend on the adapters available. Always compare against the same suites run on your own machine before your change.

| Suite | Result |
|---|---|
| `ComputeWeave.Tests` | 3902 total, 3874 passed, 8 failed, 20 skipped |
| `ComputeWeave.Tests.Internals` | 1072, all passed |
| `ComputeWeave.Tests.DeviceLost` | 43, all passed |
| `ComputeWeave.Tests.SourceGenerators` | 207, all passed |
| `ComputeWeave.Tests.DebugLayer` | 14, all passed |

- The eight failures are image comparisons on the hardware-accelerated adapter, in `FractalTiling`, `TwoTiledTruchet`, `ExtrudedTruchetPattern` and `PyramidPattern`; each counts twice because every shader is tested in two variants. The normalized deltas lie between 0.0000005% and 0.0004%. The WARP variants pass, and none of it is a regression.
- Three consecutive runs produced exactly those eight failures, but one of the three reported six fewer tests in total (3896 rather than 3902) while the failing names stayed identical. That is the practical argument for comparing names rather than totals.
- An earlier baseline of 24 failures is obsolete. Sixteen of them were Texture3D copies, resolved by keeping 3D texture copies off the copy queue (`4ed96858`); the copy-queue barrier rule was fixed in `ef5e8549`.
- The skip count was 20 in every run, and the two runs captured in full skipped exactly the same 20 tests, so a change in that number is worth investigating. On a machine with no hardware-accelerated adapter the number differs, because the tests that target such an adapter end as inconclusive.
- Building with the optional D3D12 memory allocator changes the picture substantially and is not the default configuration. Do not compare a run with it enabled against this baseline.
- A rare, never-reproduced failure has been observed in the internals suite. If you hit an unexplained failure, keep the log; it may be that one.

### Other verification

Running the suites is not the whole of verification.

- A change on a path with an established allocation contract is verified by measuring managed allocation with `GC.GetAllocatedBytesForCurrentThread`, not by inspection; the contract itself is stated under [Allocation contracts](#allocation-contracts).
- If you add or modify an analyzer diagnostic that produces build errors, verify the complete solution in addition to the analyzer tests.
- For public API or descriptor changes, run the compatibility, deterministic-generation and golden-data checks of the affected subsystem.

---

## Evidence

A change to runtime behavior arrives with its evidence. Include what applies, and say plainly which items do not.

1. Which invariants the change adds, relies on or modifies, and the tests they map to.
2. The public API difference, or an explicit statement that the surface is unchanged.
3. Descriptor and generated-output results when the generator is involved, including byte-identical clean and incremental output.
4. The result of the mutation used to verify each new test, named test by name.
5. Debug-layer and GPU-based validation results for changes touching barriers, states, queues or interoperation.
6. Allocation measurements for changes on a path with an allocation contract.
7. Repetition results for anything concurrent, including how many runs, and warm or cold.
8. The adapter, driver and adapter class the measurements came from.
9. Known limitations, and anything left unverified.

---

## Performance Changes

Do not claim an improvement from a single benchmark run.

Compare baseline and candidate repeatedly under the same hardware, driver, configuration and power conditions, and report enough measurements to distinguish the change from normal run-to-run variation.

Run correctness validation and performance measurement separately: correctness with the debug layer and GPU-based validation enabled, performance in release with validation disabled.

A measurement is not by itself a reason to change a contract. Queue selection, for example, is decided by the resource state and dimension rules above, not by which queue benchmarks faster.

---

## Upstream Divergence

ComputeWeave is a fork, and it will be merged with upstream changes again.

Record every intentional divergence from upstream, with the reason, in the ledger below. A divergence that is not written down is one the next merge will silently undo. If you fix a defect that originates upstream, add a row; if you change a row's behavior, update it. The ledger covers code inherited from upstream where this repository deliberately behaves differently; subsystems that exist only in this fork are not listed.

| Area | Upstream | Here | Commits |
|---|---|---|---|
| DXC library extraction | Opens with `CreateNew` and swallows `IOException`; no verification, no atomic publish, no retry | Hash verification, atomic publish, and retry classified by condition | `5694027e`, `52e57264`, `c9325120`, `4c1d8898`, `04f9adeb` |
| `GraphicsDevice.WaitForFenceAsync` | Never unregisters the thread-pool wait before closing the event | Registers once and unregisters on both the callback and the failure path | `62f20171`, `be9c04b0` |
| `GraphicsDevice.UnregisterDeviceLostCallback` | Compares the `BOOL` result of `UnregisterWait` against `S_OK`, inverting the condition | The callback is the single owner of the handle release | `6b75afc5`, `22bfe32a`, `7342cf25` |
| `DeviceHelper` DXGI factory backcompat shim | Leaves `QueryInterface` and `AddRef` unimplemented; `Release` ignores the reference count | All three implemented; the type is internal so tests can construct it directly | `75106254` |
| `TextureView2D<T>` and `TextureView3D<T>`, `TryGetSpan` | Compares a byte stride against an element count, so contiguous views of multi-byte elements always report false | Compares against `width * sizeof(T)` | `c226b0b5` |
| `TextureView2D<T>` and `TextureView3D<T>`, `CopyTo(Span<T>)` | 2D demands an exact length; 3D writes part of the data before throwing; both contradict their own documentation | Both reject only a destination that is too short | `c7475273` |
| `ID3D12DeviceExtensions.CreateInfoQueue` | Leaks the queried interface when filter configuration fails | Releases it on the failure path | `a7f7826d` |
| `WICFormatHelper.GetForFilename` | Uses a 4-character buffer, making `.jpeg`, `.jfif`, `.exif` and `.tiff` unreachable and throwing instead | Uses 5 characters | `1965f274` |
| `StructuredBuffer<T>` byte length | Multiplies as `int` before widening, while adjacent code widens first | Widens to `nint` before multiplying | `4001e9a7` |

Record the attempts that failed, too, and why. A rejected approach with its cause documented is worth more to the next contributor than a clean history that invites the same mistake twice.

If a change alters what the library guarantees, update [README.md](/README.md) and [README.ja.md](/README.ja.md) in the same pull request.

---

## Code of Conduct

All contributors are expected to follow the repository's [Code of Conduct](/CODE_OF_CONDUCT.md).
