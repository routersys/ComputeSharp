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
   - [Labels](#labels)
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

**“Behavior change”.** What an existing caller would observe differently, including exception types and the timing of failures. Write "none" when nothing observable changes; do not leave it blank.

**“Verification”.** The results you actually ran, and how they compare with the same suites before your change. Do not judge a run by its total failure count; see [Reading a test run](#reading-a-test-run).

Throughout, distinguish what you observed, what you derived from reading the code, and what you assume. A defect found by reading code is a real finding — say that it has not been reproduced. Never present analysis as observation. And say what you did not do: if part of the work was skipped, blocked or left unverified, state it plainly rather than letting a passing suite imply completeness.

### Labels

Apply the label that matches each kind you kept in the template. The kinds and the labels correspond one for one: `bug`, `public api`, `behavior change`, `performance`, `analyzer or generator`, `documentation` and `build and ci`. Nothing applies them for you, and the automated check described below does not set them. Labels are how the merged history is filtered by kind, so a pull request that declares a kind and carries no label is invisible to that filter.

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

CI ([`.github/workflows/ci.yml`](/.github/workflows/ci.yml)) builds the solution and runs the source generator, internals, main and Direct2D suites, and the top-level statement program, with device-lost in a separate job. Two suites are left out, each for a reason recorded where it is skipped: the debug-layer suite needs a real device, and the native library resolver suite packs and runs repeatedly. Run the debug-layer suite locally whenever you change command ordering, resource states, barriers, queue selection or interoperation; nothing else will catch those defects.

The release workflow ([`.github/workflows/release.yml`](/.github/workflows/release.yml)) runs everything CI runs and adds the native library resolver suite. The quick release workflow ([`.github/workflows/quick-release.yml`](/.github/workflows/quick-release.yml)) runs no tests of its own and refuses to publish unless CI concluded successfully on the same commit, so nothing reaches a feed without having been tested.

**Choose what to run by what the change can break, not by a count.** Suites are added over time, and a number written here goes stale without anything failing. For a change to `src/ComputeWeave`, run what CI runs and add the debug-layer suite when the change touches the areas named above. For a documentation-only or otherwise isolated change, run the verification appropriate to the affected area.

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

These values were measured on 2026-08-22 with the commands above, on a machine where both a hardware-accelerated adapter and WARP are present, the `ComputeWeave.Tests` row again on 2026-09-03. They are a starting point for comparison, not a target: counts change as tests are added, and results depend on the adapters available. Always compare against the same suites run on your own machine before your change.

| Suite | Result |
|---|---|
| `ComputeWeave.Tests` | 4150 total, 4098 passed, 0 failed, 52 skipped |
| `ComputeWeave.Tests.Internals` | 1072, all passed |
| `ComputeWeave.Tests.DeviceLost` | 43, all passed |
| `ComputeWeave.Tests.SourceGenerators` | 207, all passed |
| `ComputeWeave.Tests.DebugLayer` | 14, all passed |

- Those eight failures were image comparisons on the hardware-accelerated adapter, in `FractalTiling`, `TwoTiledTruchet`, `ExtrudedTruchetPattern` and `PyramidPattern`, each counting twice because every shader is tested in two variants. They are inconclusive there now rather than failed. Nine of the thirteen shaders fold `sin` or `cos` into a hash, scaling it past its low bits and taking the fraction, which ties the image to the implementation of that function on the device. The WARP variants are the ones that pass against the reference images, so only they are held to them. Those nine still run on the adapter and still compare on WARP, and the four that carry no such hash still compare on both.
- Three consecutive runs produced exactly those eight failures, but one of the three reported six fewer tests in total (3896 rather than 3902) while the failing names stayed identical. That is the practical argument for comparing names rather than totals.
- An earlier baseline of 24 failures is obsolete. Sixteen of them were Texture3D copies, resolved by keeping 3D texture copies off the copy queue (`4ed96858`); the copy-queue barrier rule was fixed in `ef5e8549`.
- The skip count was 20 in every run, and the two runs captured in full skipped exactly the same 20 tests, so a change in that number is worth investigating. On a machine with no hardware-accelerated adapter the number differs, because the tests that target such an adapter end as inconclusive. On a machine that has one, the eighteen hashed shader comparisons above end the same way.
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

The Direct2D authoring projects were removed before the first release and restored in August 2026. Restoring them widened what the audit inspects by 212 inherited source files, across `ComputeWeave.D2D1`, `ComputeWeave.D2D1.SourceGenerators`, `ComputeWeave.D2D1.CodeFixers` and `ComputeWeave.Win32.D2D1`. The remaining Direct2D projects at the fork point, the 55 files of the UI integration family, were deliberately not restored and are not inherited code here. The audit counts modifications and not additions, so the restoration itself put nothing in the queue; changes made to those files from now on do.

| Area | Upstream | Here | Commits |
|---|---|---|---|
| DXC library extraction | Opens with `CreateNew` and swallows `IOException`; no verification, no atomic publish, no retry | Hash verification, atomic publish, and retry classified by condition | `5694027e`, `52e57264`, `c9325120`, `4c1d8898`, `04f9adeb`, `ca17c3a3` |
| `GraphicsDevice.WaitForFenceAsync` | Never unregisters the thread-pool wait before closing the event | Registers once and unregisters on both the callback and the failure path | `62f20171`, `be9c04b0` |
| `GraphicsDevice.UnregisterDeviceLostCallback` | Compares the `BOOL` result of `UnregisterWait` against `S_OK`, inverting the condition | The callback is the single owner of the handle release | `6b75afc5`, `22bfe32a`, `7342cf25` |
| `DeviceHelper` DXGI factory backcompat shim | Leaves `QueryInterface` and `AddRef` unimplemented; `Release` ignores the reference count | All three implemented; the type is internal so tests can construct it directly | `75106254` |
| `TextureView2D<T>` and `TextureView3D<T>`, `TryGetSpan` | Compares a byte stride against an element count, so contiguous views of multi-byte elements always report false | Compares against `width * sizeof(T)` | `c226b0b5` |
| `TextureView2D<T>` and `TextureView3D<T>`, `CopyTo(Span<T>)` | 2D demands an exact length; 3D writes part of the data before throwing; both contradict their own documentation | Both reject only a destination that is too short | `c7475273` |
| `ID3D12DeviceExtensions.CreateInfoQueue` | Leaks the queried interface when filter configuration fails | Releases it on the failure path | `a7f7826d` |
| `WICFormatHelper.GetForFilename` | Uses a 4-character buffer, making `.jpeg`, `.jfif`, `.exif` and `.tiff` unreachable and throwing instead | Uses 5 characters | `1965f274` |
| `StructuredBuffer<T>` byte length | Multiplies as `int` before widening, while adjacent code widens first | Widens to `nint` before multiplying | `4001e9a7` |
| `Hlsl.Lit` | Declares a `float` return, though the HLSL `lit` intrinsic returns four components | Declares `Float4` | `f8acc01f` |
| `Hlsl.Transpose` | Declares only the 36 non-square overloads of `Bool`, `Float` and `Int`, though the HLSL `transpose` intrinsic accepts any matrix | Declares all 80, one for each public matrix type | `1383d273` |
| `Hlsl.AsDouble` | Declares only the unsigned scalar and two component overloads, though the HLSL `asdouble` intrinsic also accepts signed halves, three and four component vectors, and every matrix shape | Declares all 40, matching what `AsFloat`, `AsInt` and `AsUInt` already cover on both the signed and the unsigned side | `4553359c`, `7a95ec18` |
| Coarse and fine derivatives | `ddx_fine`, `ddx_coarse`, `ddy_fine` and `ddy_coarse` declare the scalar and three vectors only, though HLSL accepts a matrix there and the plain `ddx`, `ddy` and `fwidth` already declare all sixteen | All four declare the matrix shapes, so the family accepts the same shapes whether or not the coarseness is named | `7390000d` |
| `Hlsl.Clamp`, `Hlsl.Max` and `Hlsl.Min` | `Clamp` declares Float and Int only, and `Max` and `Min` add Double but not UInt, though HLSL accepts every one of those and `Mad` already declares UInt | All three declare Float, Int, Double and UInt, so a clamp reaches as far as the min and max it is built from | `93a8073c` |
| `Hlsl.All`, `Hlsl.Any` and `Hlsl.Mul` | `All` and `Any` reduce Bool, Float and Int but not Double or UInt, and `Mul` declares Float and Int only, though HLSL accepts those kinds. A call passing two unsigned scalars binds to the float overload, so the product is float | `All` and `Any` reduce Double and UInt as well, and `Mul` declares UInt. Mul is left without Double on purpose: its three vector by vector rows fail DXIL validation the way `Dot` has no double overload, and a single `Double2x2` product loses the device at run time, so the kind is refused rather than half declared. A call passing two unsigned scalars now binds to the unsigned overload, so the product is unsigned and an expression that divides it divides as integers | `40fb5a27` |
| `Hlsl.Dot` | Declares Float and Int only, so a pair of unsigned vectors converts to the float shape and the product comes back as float, though the vector by vector `Mul` that lowers to the same `dot` returns unsigned here, which leaves one HLSL operation with two result types depending on the name it is written under | Declares the unsigned shapes as well, so both names give the same unsigned product and the conversion to float is gone, which also stops the rounding a float significand imposes past its twenty four bits. A call that already passed unsigned vectors now yields unsigned where it used to yield float | `8c730f0a` |
| `UInt` vector and matrix products | Generates the `mul` operators, and the scalar promotion operators that keep a product by a scalar unambiguous, for Float and Int only, so an unsigned product of two shapes that differ has to be written as `Hlsl.Mul` even where the intrinsic declares it. The list the operator templates read is a verbatim second copy of the one the intrinsic list holds, kept equal by hand | Generates all three kinds, so `x * y` reaches the same intrinsic whichever kind it is, and the unsigned side declares the same 141 multiplication operators Float and Int do. The intrinsic list reads the shared copy instead of repeating it, so the two cannot drift apart again | `2c9c859b` |
| `WICFormatHelper`, saving `R16` | Encodes through an 8-bit grayscale intermediate, discarding the low byte of every pixel | Keeps the 16-bit format | `21c54a22` |
| `Rg32` and `Rgba64` documentation | Describe their 16-bit components as ranging from 0 to 255 | 0 to 65535; a documentation correction only, with no behavioral change | `bb3bfe75` |
| `D2D1PixelShaderEffect.SetResourceTextureManagerForD2D1Effect` documentation | Writes the accepted index range as [0, 16], which includes an index the argument check refuses | [0, 16); a documentation correction only, with no behavioral change | `27280a7f` |
| `WICHelper`, size mismatch | Reports the failure with `nameof(texture.Width)`, naming a property instead of the parameter | Names the parameter and states which dimension differs | `4bb55543` |
| `WICHelper`, encoder results | Discards the `HRESULT` of stream initialization and of both `Commit` calls | Asserts each of them | `817addc4` |
| `ComputeContext`, dispatch group counts | Reports the Y and Z checks with `nameof(groupsX)` | Reports each argument under its own name | `35cd67ea` |
| Generated HLSL, `bool` constants | Casts the constant to `IFormattable`, which `bool` does not implement, so the generator throws and every shader in the compilation unit loses its descriptor | Emits `true` and `false` | `b242e784` |
| Generated HLSL, discovered types | Casts every tracked type to `INamedTypeSymbol`, so an array or a pointer throws and every shader in the compilation unit loses its descriptor | Adds only named types to the discovered set | `cfe0e650` |
| Generated HLSL, a matrix given to an intrinsic with an out parameter | Writes the call out and hands it to DXC, which terminates with an access violation and takes the compiler process with it, so the build fails with a native fatal error naming no source line | Refuses the call while rewriting, which skips the compilation step for that shader and reports `CMPW0123` at the author's own line. The condition is the shape of the signature rather than a name: one out parameter terminates on an integer matrix, measured on `modf`, `frexp` and `sincos` alike, and two of them terminate on a matrix of any element type, measured on `sincos`, the only intrinsic declaring two. Both rewriters carry the refusal, an initializer reaching the compiler with the same call whenever the out argument is an identifier, so the judgment sits on the type they share. Direct2D is left alone, being compiled through FXC, which does not have the defect | `3ae0fa13`, `35b7edc7`, `3843a284`, `75c1a8b8` |
| Generated HLSL, lambda parameters | Reads the type syntax of every parameter, which a simple lambda does not have, so the semantic query throws | Tracks the type only when the parameter declares one | `814c32ca` |
| Generated HLSL, `scoped` locals | Resolves the type of a local declaration without looking through `scoped`, so the semantic model binds nothing, the null reference ends the generator, and every shader in the compilation unit loses its descriptor | Looks through the modifier, so a scoped local is diagnosed exactly as the one without it | `695a5f25` |
| Generated constant buffer accessors | Applies the `Bool` type name to every part of a field path, so the accessor for a struct that holds a `Bool` returns `ref Bool` and the marshaller does not compile | Applies it to the leaf part only | `add58a53` |
| Thread pool wait callbacks | Lets a managed exception leave the `[UnmanagedCallersOnly]` boundary, which ends the process and abandons the wait registration, the event and the callback context | Contains it inside the boundary; the fence wait releases its native state on every path and delivers the failure to the awaiter, and the device lost callback drops it once the reason has been recorded | `f1ccc0a5`, `82845a15`, `193507b4` |
| `Configuration`, `AppContext` switch names | Reads the `COMPUTESHARP_…` switches | Reads the `COMPUTEWEAVE_…` switches; the former names were honored as a fallback for a time and are no longer read | `7c3ebec1`, `391c75ea` |
| Generated HLSL, mapped member replacements | Splices the replacement for a mapped member into the tree unparenthesized, so `GroupSize.Count`, `DispatchSize.Count` and `ThreadIds.Normalized` lose their grouping when they stand as a divisor, and `Float4.Zero` cannot be the target of a member access | Parenthesizes a replacement that is not already a primary expression | `538b1b4d` |
| Generated HLSL, numeric constants | Formats a `const float`, a `const double` or a `const decimal` with `IFormattable.ToString`. A whole value loses its decimal point and becomes an integer literal, so dividing by such a constant performs integer division, while the same value written as a literal keeps one. A non-finite value becomes `NaN` or `Infinity`, which HLSL does not declare | Appends the decimal point, and the `L` suffix for `double`, exactly as the literal path does; a `decimal` becomes a plain floating point literal, HLSL having no decimal type. A non-finite value is written as the reinterpretation of its bit pattern | `9e20da42`, `9b88c149`, `b6d88a6f` |
| Generated HLSL, reserved identifiers | Reserves a partial set of the Shader Model 5 resource type names, so a captured field named after any other reserved identifier reaches the compiler unrenamed and fails | Reserves every name measured to collide, found by declaring 240540 candidate identifiers as constant buffer fields and compiling them. The candidates are every identifier-like string in the compiler binaries plus every identifier of at most three characters. The set is version dependent, so the union over DXC 1.6.2112.16, 1.7.2308.7 and 1.8.2502.11 is reserved | `46e199b3`, `1bcce681`, `7bd16677`, `c2c2bb1a` |
| Generated constant buffer, member names | Names a member of the generated `ConstantBuffer` type after the captured field path, so a field named `ConstantBuffer` becomes a member of its own type and the generated code does not compile, with no diagnostic | Renames that one member the same way the HLSL side renames a reserved identifier | `c74a262d` |
| Group shared fields, element type | Accepts any unmanaged array element type, so a pointer element reaches an unguarded cast to `INamedTypeSymbol`; the generator throws and every shader in the compilation unit loses its descriptor, with no diagnostic | Requires a named element type in both the generator and the analyzer, so the field is reported with `CMPW0004` instead | `56a4a1d0` |
| Captured field names in both languages | Carries the field name into the generated C# and the generated HLSL verbatim. A name matching an artificial constant buffer field collides on both sides, and a verbatim identifier keeps its `@` in HLSL while the generated C# fails to parse; neither case is diagnosed | Reserves the artificial names on both sides and escapes a C# keyword where the generated C# needs it, so the two languages get names that cannot collide | `00eaddfb` |
| Constant buffer size, analyzer path | Counts fields that the generator skips, so a shader holding a field of an inaccessible type is reported as too large even when it fits | Skips the same fields in both paths, so the two compute the same size | `4722d9a7` |
| `WICHelper`, saving through an intermediate format | Creates and initializes a format converter that is never passed to `WriteSource`, which converts on its own; the unused converter only adds a way to fail | Removed; all thirty format and pixel type combinations produce byte identical output without it | `7e832ea1` |
| Generated HLSL, the dispatch values | Declares the whole dispatch surface as `int` in C#, then writes the entry point as `uint3 ThreadIds : SV_DispatchThreadID`, the group thread and group identifiers the same way, the group index as `uint`, and the three dispatch bounds as `uint __x`. An expression that reaches an operation without a local to hold it is therefore evaluated unsigned. On the first thread `ThreadIds.X - 1 < 0` is false, `(ThreadIds.X - 1) >> 1` is 2147483647 and `(ThreadIds.X - 1) / 2` is 2147483647, where the C# the author wrote gives true, -1 and 0; storing into an `int` local first restores the sign, which is why it goes unseen. Of the 198 members those five types publish, 122 are read unsigned. The generated marshaller declares the same buffer field as `int __x`, so the two halves of one buffer disagree, and the same mapping already wraps the two and three component forms of the dispatch size in `int2` and `int3` | Declares the four system values and the three bounds as signed, so the body text is unchanged and every expression carries the sign its declaration promises. `int3` on `SV_DispatchThreadID`, `SV_GroupThreadID` and `SV_GroupID`, and `int` on `SV_GroupIndex`, are all accepted by the compiler, and every comparison in the result lowers to a signed one, which was measured on the compiler directly. Of the 680 generated files compared before and after, 305 differ, and every differing line is one of three kinds: the entry signature, a constant buffer declaration, or the recompiled bytecode | `5ebe1198` |
| Generated HLSL, matrix constructor arguments | Casts every argument of a matrix constructor to the element type without parentheses, so an argument that binds more loosely than a cast is regrouped; on integers `new Float2x2(a / b, …)` then divides in floating point instead of truncating, with no diagnostic | Parenthesizes an argument that does not already bind at least as tightly as a cast | `5d99d17f`, `6bf1821b`, `8b490f7e` |
| Generated HLSL, unsigned right shift operands | Rewrites `>>>` into `>>` and reuses both operands unparenthesized, so the value of a `>>>=` is regrouped against the shift, and a signed left operand is cast to the unsigned type before its own operator applies | Parenthesizes an operand that does not already bind at least as tightly as the surrounding cast or shift | `5ebf9d1d`, `6bf1821b`, `8b490f7e` |
| Generated HLSL, local function names | Builds the name of a hoisted local function from the syntax token at the declaration and from the symbol at the call site. A verbatim identifier keeps its `@` on one side and loses it on the other, so the two names never meet and the `@` is not valid HLSL | Builds both from the symbol, so the two sides agree for every identifier | `504980eb`, `483c5cf2` |
| Generated HLSL, local functions in imported methods | Gives every nested rewriter its own collection of hoisted local functions and reads only the one belonging to the shader type. A local function declared in an external static method, in an instance method of a custom struct, or in a constructor is dropped while its call site is still rewritten, and no diagnostic is reported | Merges the collections and qualifies the name of a local function outside the shader type with the name of its containing method, so it cannot collide with one from another type | `483c5cf2`, `be1d10a1` |
| Generated HLSL, character values | Leaves a character literal untouched, which HLSL only accepts for the ASCII range, and formats a `const char` with `IFormattable.ToString`, which writes the character itself into the `#define` and never compiles | Writes the UTF-16 code unit as a numeric literal on both paths, which is the value every HLSL expression using it would observe | `cb309cdb` |
| Generated HLSL, non static local functions | Lifts a local function to a top level HLSL function and renames it, but rewrites the call site only for a static one. A non static local function keeps its original name at the call site, so when the shader type declares a static method of that name the call silently binds to the method instead, with no diagnostic and no compiler error | Reports `CMPW0113`. C# guarantees that a static local function captures nothing, so lifting one preserves its meaning without any capture analysis of our own, and the rule costs a shader nothing because C# already forbids a local function in a struct from reading `this` | `010e9ca9` |
| Generated HLSL, logical intrinsic operands on the Direct2D path | Rewrites `Hlsl.And`, `Hlsl.Or` and `Hlsl.Select` into `&&`, `\|\|` and `?:` for Direct2D and reuses every operand unparenthesized, so an operand that binds more loosely than the operator now surrounding it is regrouped; `Hlsl.And(a ? b : c, d)` is printed as `(a ? b : c && d)`, which reads back as a different expression, with no diagnostic | Parenthesizes an operand that does not already bind at least as tightly as the surrounding operator, the same rule the matrix constructor and shift paths follow. The branch is not compiled by any project here, so the fix leaves every generated file byte identical | `3895015d` |
| Generated HLSL, D2D input sampling coordinates | Lowers `D2D.SampleInput`, `D2D.SampleInputAtOffset` and `D2D.SampleInputAtPosition` to the function-like macros of `d2d1effecthelpers.hlsli` and pastes the coordinate argument in unparenthesized, so a compound argument is regrouped by the preprocessor. `D2D.SampleInputAtOffset(0, a - b)` expands to `uv.xy + a - b * uv.zw`, which scales only `b` by the texel size, with no diagnostic and no compiler error | Parenthesizes the coordinate argument of all three when lowering them, so the emitted HLSL does not depend on macro hygiene in any particular version of the header. The embedded copy of `d2d1effecthelpers.hlsli` is left byte for byte identical to the Windows SDK header, so it stays safe to refresh. Taken from upstream pull request 930, which fixes upstream issue 929 and is still open; retire this row once it is merged | `e396a470` |
| Generated HLSL, float values on the Direct2D path | Writes a `float` literal as the reinterpretation of its bit pattern through `asfloat`, to work around a compiler defect that can give a decimal literal the wrong value (upstream issue 780), while writing a `const float` holding the same value as decimal text. For the two feature level 9 profiles the compiler also emits a second translation of the shader, and it is that one such a device runs; in it `asfloat` of a literal is folded as a conversion of the number rather than a reinterpretation of its bits, so `1.5` arrives as `1069547520` | Writes both as decimal text. No spelling of the bit pattern survives the feature level 9 translation, and the HLSL a shader carries can be recompiled at run time for any profile, so one text has to be right for all of them. Decimal text was measured right in 266 combinations of value, profile and options, in both translations, and to agree with the bit pattern over 1500000 values on the shader model 4 path, across the three `d3dcompiler_47.dll` versions installed on the measuring machine; issue 780 did not reproduce on any of them. Should a compiler that does exhibit it turn up, this trade has to be measured again | `569ff52d`, `b529d72d` |
| Generated HLSL, reserved identifiers on the Direct2D path | Renames a captured field whose name collides with a reserved identifier, but measures that set against DXC alone. The Direct2D path compiles with FXC, which still parses the effect framework and so keeps names DXC has dropped, and which matches four of them without regard to case. A field named `SamplerState`, `PixelShader`, `texture2D` or `Pass` reaches FXC unrenamed and the shader fails to compile, naming a line of generated code the author never wrote | Reserves the 31 further names FXC rejects, and every casing of the four it matches case insensitively, from the Direct2D side of the mapping so that the shared set stays the one DXC measured. The names were found by declaring 222351 candidate identifiers as shader globals and compiling them, over the three `d3dcompiler_47.dll` versions installed on the measuring machine, which reject the same set; the four case insensitive ones were isolated by sweeping all 20804 casings of every rejected name of at most ten characters. `Execute` is left unreserved on purpose, being a collision with the generated entry point rather than a reserved word | `da12b093`, `41d3c1bb` |
| Generated HLSL, matrix constructor arguments that do not bind | Reads the parameter each argument of a matrix constructor binds to, in order to know which element type to cast it to, and dereferences that parameter unconditionally. When overload resolution has failed there is no parameter, so the generator faults with a `NullReferenceException`, its output is discarded for the whole compilation unit, and the one error the author needs to see is buried under the errors that causes | Leaves an argument alone when it does not bind. The C# compiler already reports the call, so the only thing left for the generator to do is finish. Every argument that does bind is cast as before, so no shader that compiles produces different output | `ce80ccad` |
| `D2D1ResourceTextureManagerImpl.Factory` and `D2D1DrawTransformMapperImpl.Factory` | Allocates the instance with `NativeMemory.Alloc`, then calls `GCHandle.Alloc` with neither call guarded. If `GCHandle.Alloc` throws, the native block is never freed, since its address was never handed back to the caller | Wraps the sequence in a `try`/`catch` that frees the native block before rethrowing, matching the pattern `PixelShaderEffect.Factory` already used for the identical two-allocation shape | `f807d6b0` |
| `D2D1ResourceTextureManagerImpl`, resource texture staging buffer initialization | Overwrites the staging pointers without releasing what they already hold, and abandons an allocation that a later failure in the same call makes unreachable. `Initialize` refuses to run again only when a resource texture or `data` is present, and a call that completes with no data leaves `data` null, so a second `Initialize` is accepted and leaks the extents, extend modes and strides of the first; measured over one million calls this grows the private working set by 97.8 MB, with no allocation failure involved. The same gap makes a partial failure unrecoverable: if the extend modes allocation fails, the extents buffer it had just allocated is a local that no field ever receives, and if the strides allocation fails, `data` is left set while `strides` stays null, which the staging path of `Update` then dereferences for a multi-dimensional texture | Releases every staging pointer and clears it before allocating a new set, so the sequence always begins from a fully null state; frees the local extents buffer immediately when the extend modes allocation after it fails; and rolls `data` back when the strides allocation after it fails, so `Update` can never observe one without the other. Over the same one million calls the working set reaches a plateau at 9.6 MB | `95055663`, `13cbf624` |
| `ComputeWeave.Dxc` packaging | Declares the packing items only in the `Otherwise` branch of a `Choose`, so a pack invoked with `Platform` or `CI_RUNNER_DOTNET_TEST_PLATFORM` set produces a package that carries no native libraries at all. Nothing reports it: the package still restores, and a consumer that has another copy of DXC on the machine still runs | Declares the packing items unconditionally and leaves the `Choose` to decide only where the libraries are copied for local use, so the package carries both architectures however pack was invoked. The audit script inspects only `.cs` files, so this row is the sole record of the divergence | `9912c9d1` |
| `ReflectionServices` DXC library resolver | Registers the resolver on the assembly that declares `ReflectionServices`, which declares no P/Invoke of its own, so it never runs. `dxcompiler` is found by the default probing either way, but `dxil.dll` is imported by no managed code at all: `dxcompiler.dll` loads it itself through `LoadLibrary`, which searches neither the application directory nor `runtimes/<rid>/native`. A package reference without a runtime identifier, which is how the library is normally consumed, therefore never uses the `dxil.dll` the package deployed, however the application is launched; it binds whatever copy is on `PATH`, or none at all. With a runtime identifier the libraries sit beside the application and only a launch through the shared host is affected | Registers it on the assembly that declares the `dxcompiler` import, so the pre-load happens and every deployment layout and launch mode binds the `dxil.dll` the package deployed. An assembly that already carries a resolver is treated as a lost race rather than a failed type initializer, that failure being cached and otherwise poisoning every later call. The consumer sample asserts that the only `dxil.dll` mapped into the process is the one under the application's own directory, so the deployment shapes the resolver suite publishes fail if the pre-load stops happening. The audit script does not read `tests/`, so this row is the only record of that half | `5a28402b`, `6653c6e5` |
| `NativeLibrariesResolverTestsBase`, package staging | Packs the projects the sample consumes and then restores, without touching the folder NuGet extracts into. The version does not change between runs and a restore prefers an already extracted copy over any source, so the sample links against whatever build of that version was extracted first. Measured both ways on the same machine: with a package built from earlier sources extracted, a correct working tree reported 14 failures, and with a package built from correct sources extracted, a working tree whose resolver had been reverted reported all 53 passing | Removes each package it has just packed from that folder, so the restore has to take the fresh one; the same starting state that produced the false pass then reports on the tree. The folder is read from `dotnet nuget locals` rather than assumed, because a configuration file or an environment variable can move it and guessing wrong would make the removal a silent no-op. The audit script does not read `tests/`, so this row is the only record of the divergence | `2e2aa9b2` |
| `ShadersTests`, comparing against a reference image | Compares what all thirteen shaders draw against a stored reference image on every device. Nine of them fold `sin` or `cos` into a hash, scaling it past its low bits and taking the fraction, so the image follows the implementation of that function on the device rather than the shader; the WARP variants are the ones that pass against the references, and on a hardware-accelerated adapter four of the nine exceed their threshold on every run. The Direct2D suite meets the same difference and answers it by ignoring four of its own, a comment there recording the cause as unidentified | Reports those nine as inconclusive on any device other than WARP, after the shader has run and its image has been written, so the dispatch and the readback still happen there and only the comparison is left undecided. The four that carry no such hash still compare on every device, so a difference that is not the transcendental has somewhere to show. The audit script does not read `tests/`, so this row is the only record of the divergence | `a1e71426` |
| Shader descriptor generators, semantic model reuse | Builds a fresh `SemanticModel` for the shader type out of the `Compilation`, discarding the one the incremental generator already handed to the pipeline, so a shader whose type is declared in a single file pays for a semantic model it already had | Seeds `SemanticModelProvider` with the semantic model from the generator context and returns it for any node in that same syntax tree, building additional models only for the other trees a shader actually reaches into. Taken from upstream pull request 927, which is still open; retire this row once it is merged | `69f5281b` |
| Shader descriptor generators, parallel shader compilation | Compiles each shader's HLSL bytecode inline, inside the incremental generator's per-shader transform callback. The driver invokes transform callbacks sequentially, so a project with many shaders serializes every one of their compilations, even though each is independent and both the shared cache and the native compilers support concurrent use | Defers the compilation to a dedicated node that compiles every shader discovered in the compilation in parallel with `Parallel.ForEach`, then joins each shader with its now cached bytecode and synthesizes its diagnostics from info captured by value during the transform, a symbol not being usable past that point. Taken from upstream pull request 932, which closes upstream issue 931 and is still open; retire this row once it is merged. Generated output was measured byte identical, against the same build without the change, for every shader in `ComputeWeave.Tests` and `ComputeWeave.D2D1.Tests`, which are the projects the comparison built; the shaders the other projects hold were outside it | `fb206b19` |
| `DiagnosticInfo`, locations outside a syntax tree | Keeps only the syntax tree and the span of the location it is handed, so a location that names a file but belongs to no tree is discarded and the diagnostic reaches the author with no position at all. Nothing handed it such a location until the shader compilation moved out of the transform node, where a location can no longer be tree bound, and the four compile diagnostics of each descriptor generator lost their file and line | Captures such a location by value and rebuilds it when the diagnostic is created, so the file and line survive. A location inside a tree takes the same path as before, and a location that names no file, a metadata one, still yields none. The rebuilt location is bound to the tree the compilation holds for that file, which is what a suppression written in source for a single line and the analyzer configuration entries for a file are applied through: measured on a project built against both versions, where the directive the author wrote around the shader silences the warning only once the location is bound, and a diagnostic of the same generator that was tree bound all along is silenced either way. The tree is looked up on the node that reports the diagnostic and not carried in the model, a model holding a tree comparing unequal across unrelated edits and keeping a stale compilation alive. That node therefore takes the compilation and runs on every edit, having nothing to run for while every shader is free of diagnostics, the sequence reaching it having been filtered. A file the compilation does not hold, an empty path, and a span the tree does not cover all still yield the location as captured. Retire this row once upstream carries the same repair | `10622a78`, `21ebf964` |
| Test host | Runs every suite through VSTest | Runs them through Microsoft.Testing.Platform, with the MSTest runner and its `dotnet test` support enabled for the whole `tests` directory. Taken from upstream pull request 898, which is still open; retire this row once it is merged. The rest of that pull request adapts a WinUI test project around the generated entry point, which does not apply here, this fork not carrying that project. The projects here that declare their own entry point do not reference MSTest, so enabling the runner leaves them alone | `5f9cf75c` |
| `D2D1CompileOptions.DeclareMinimumPrecisionSupport` | Has no way to ask for a compiled shader to declare minimum precision support, so Direct2D generally declines to link effects even when `EnableLinking` is set, costing a rendering pass and an intermediate surface per effect that would otherwise have been linked | Adds the option, which appends a shader feature info blob declaring `D3D_SHADER_FEATURE_MINIMUM_PRECISION` and recomputes the container checksum FXC validates. Taken from upstream pull request 928, which is still open; retire this row once it is merged. The experimental diagnostic is `CMPWEXP0001`, this fork numbering its diagnostics `CMPW`, and its help link points at this repository. Two review comments left unresolved upstream are also addressed: the container validation helper in the tests returned malformed input as an exception rather than as `false`, and it computed a blob end offset in 32 bit arithmetic that could wrap past the check; and a variable carried a name from an unrelated reflection retention test. A further test asserts what the option documents, that every blob the compiler produced, including the one holding the instructions, survives the patching byte for byte | `c3adb173`, `0ca3f31d`, `9c1ce61d`, `c5d04199`, `eee01274`, `dc727ca4` |
| Generated HLSL, property reads | Reports a property declared on the shader type itself, and reports nothing for a property read from any other type. A custom type is written to HLSL field by field, so a property it declares is left out of the generated struct while the read is written out as it stands, and the shader fails in the HLSL compiler with a message that names the generated struct rather than the source the author wrote. Every shape that reaches this path answers alike: a static property, an extension member, a partial property, a property whose accessor uses the `field` keyword, and an instance property on a custom struct | Reports the read as `CMPW0114`, or `CMPWD2D0088` on the Direct2D path. The check sits after every mapping has declined the member, in the base rewriter that `ShaderSourceRewriter` and `StaticFieldRewriter` both derive from, so the shader body and a static field initializer answer the same way; placing it before the mappings would reject the swizzles, the vector components and the resource lengths that share the same fall through. Only a property is reported, a field being written out as it stands on purpose because HLSL structs carry fields. The declaration is not reported, because a custom type that declares a property the shader never reads loses nothing and still compiles, so widening the check that already covers the shader type would reject source that works today | `276d4a48` |
| Generated HLSL, user defined operators | Resolves the operator declared on a custom type and then writes the operation out as it stands, so the body the author wrote never runs. Six of the seven forms then fail in the HLSL compiler, naming generated code the author never wrote. The seventh does not: HLSL converts between a struct and a scalar on its own, taking the first member or filling every member, so an explicit conversion compiles and the shader computes a different value than the same code in C#, with no diagnostic and no compiler error. Measured both directions: a struct holding 1 and 7 whose conversion returns the second arrives as 1, and a scalar converted into a struct whose conversion fills 1 and 10 arrives as 1 and 1 | Reports `CMPW0115`, or `CMPWD2D0089` on the Direct2D path, for every operator a rewritten declaration resolves whose containing type is not one of the HLSL primitive types. The walk is over the resolved operations rather than over the syntax, because an implicit conversion has no node of its own and a report from a visit method would reach every other form and miss that one. An operator on a primitive type is either mapped to an intrinsic or left as it stands, both of which are correct, and a built-in operation resolves no operator method at all, so neither is claimed; a test pins that the built-in, vector and matrix operators keep compiling. The change adds diagnostics and rewrites nothing, which was measured: every generated file in the repository is byte identical, apart from two Direct2D shaders compiled with debug information whose bytecode differs between any two builds | `a5a92cea` |
| Generated HLSL, parameter default values | Writes the parameter list of a method into both the forward declaration and the implementation, so a default value is written twice. HLSL takes default values from the first prototype alone and every compiler here rejects the second: DXC reports `redefinition of default argument` and FXC reports `X3114`, naming a line of generated code the author never wrote, and the generator reports nothing of its own | Writes the default value on the forward declaration alone and strips it from the implementation. The two sides are not interchangeable, which was measured on both compilers before choosing one: with the default on the implementation alone, a call that appears before that implementation does not resolve. Every path that writes a method into HLSL strips it, an external static method, a method on the shader type, a local function, and an instance method and a constructor on a discovered type; the entry point is left alone, having no forward declaration to carry a default. No shader in this repository declares one, so the change leaves the generated files as they were. The two Direct2D shaders compiled with debug information differ between any two builds and were compared as HLSL text instead | `a63268dc` |
| Generated HLSL, element accesses | Rewrites the element accesses it recognizes, the resource indexers that take separate coordinates and the swizzled matrix indexer, and writes every other one out as it stands. An indexer declared on a custom type is never imported, so the accessor the author wrote does not run and the access reaches the HLSL compiler; an inline array has no indexer of its own at all, the access resolving through a `Span<T>` the author never wrote. Neither is diagnosed | Reports `CMPW0116`, or `CMPWD2D0090` on the Direct2D path, for an element access over a type that HLSL gives no indexer. The judgment is on the type being indexed and not on where the rewriter gives up: a probe that recorded every element access in the repository found 29 reaching that point, and every one of them has to keep working, 22 being indexers on the resource types, six element accesses on the vector types and one on an array, so a report placed there would reject them all. The types HLSL can index are its own vector and matrix types, the resource types and an array, and which types are resources is known to each generator rather than to the rewriter they share, so the rewriter asks through a partial method. The question asked is where the indexer is declared, so that an extension indexer over a type HLSL can index is reported rather than resolving to the built-in element access, which compiles and computes a different value; extension indexers are a preview feature and no test in the generator suites can express one, so that half is measured only by building a shader with the preview language version. Naming the indexed type rather than the indexer is what lets one diagnostic cover the inline array, which has no indexer to name. A swizzled matrix indexer with a non constant argument already has a diagnostic of its own and reaches the same point afterwards; the matrix type is one HLSL can index, so no second report is added | `ae7468d1`, `6adc64d1` |
| Generated HLSL, generic method calls | Imports the declaration of a called method by rewriting it, without asking whether the method is generic. HLSL has no type parameters, so the type parameter list is carried into the generated source and the shader fails to compile against a generated function name the author never wrote. A call in a static field initializer is not imported at all, being written out as it stands, and neither path is diagnosed | Reports `CMPW0117`, or `CMPWD2D0091` on the Direct2D path, for a generic method that no mapping claims. The check is in both rewriters, so the shader body and a static field initializer answer alike, and it covers the static, instance and local function paths at once by sitting before the branch between them. A mapping is asked for first because an intrinsic is written out under its HLSL name, which drops the type arguments and stays correct. No intrinsic is generic today, `Hlsl` declaring 2258 public static methods and `D2D` six with none of them generic, so that branch is there for the day one is added; the mutation suite records it as the one change no test catches, for that reason | `3254fbd8` |
| Generated HLSL, constant buffer size accessors | Ignores a size accessor read on a `ConstantBuffer<T>` on purpose, a comment saying the type should be reworked one day, and reports nothing. The read is written out as it stands and the HLSL compiler rejects it. The check also sits inside a cache keyed on the accessor, and `Length` is declared on the buffer base type that a structured buffer and a constant buffer share, so a structured buffer read earlier in the same shader fills that cache and the constant buffer read is written out instead as a call to a generated helper that does not take it, naming generated code the author never wrote. Which of the two failures the author sees therefore depends on the order of the reads | Reports `CMPW0118` and still returns the node unchanged. The check moves above the cache, so the order of the reads no longer decides the outcome. It is compiled into the Direct3D 12 generator alone, the Direct2D path declaring no constant buffer type, so that the other path is untouched by the build rather than by a test | `b3fc0997` |
| Generated HLSL, syntax outside the accepted set | Walks the shader body with a visit method for the constructs it knows and writes every other one out as it stands, so a syntax kind the generator has no verdict for reaches the HLSL compiler unannounced. Some of it compiles and computes the value the same code computes in C#, and some of it fails there, naming generated code the author never wrote; nothing tells the two apart, and nothing says which kinds have been considered at all | Reports `CMPW0121`, or `CMPWD2D0094` on the Direct2D path, for a syntax kind outside the set a shader body may use. The set is measured rather than designed: it is the union of the kinds the rewriter walks while this repository is built and the kinds that were built one at a time and shown to compute the same value on a device. The check sits in the one method every node the rewriting reaches passes through, and not in the visit methods, because a kind with no visit method is exactly the kind being looked for. The declaration a rewriting starts from is the one node that does not reach it, the typed overloads taking a method or a constructor to the base method and forwarding a variable declarator's initializer, and those three kinds are in the set. Nodes are what reaches it, so a modifier or any other token keeps the diagnostic it already has. It lists the kinds that are accepted rather than the kinds that are not: the generators reference Roslyn 4.9.2, whose `SyntaxKind` has 561 members against the 576 of the version the tests parse with, and four of the missing fifteen are kinds a shader can reach, so the other listing does not compile. The severity is `Error`, so such a kind is refused against the source the author wrote instead of reaching the shader compiler, which answers by naming generated code; the descriptor is still written, what the refusal stops being the shader compilation. It was raised once the solution built with it and no shader reported anything, and once every construct the sweep measured to work was shown to be inside the set. A construct with a refusal of its own is answered by that refusal alone: a refused `foreach` holds an array creation and a refused declaration sits beside one, so the records are dropped from the set produced for a shader whenever it holds an error of another kind, rather than answering one cause in several places. What is read is that whole set and not the subtree a record sits in, so the outcome does not depend on the order the rewriting walks in; syntax with no verdict elsewhere in the same shader is dropped with the rest and is reported once the refusal is gone. Of 35 inputs built for the measurement, 31 drew a refusal and a record together before this and none do now, and the one carrying no refusal still draws its record. An attribute list is dropped rather than written out, and is the only such place a kind outside the set can appear: an attribute of the author's own on an imported method carries a named argument, which is a kind the set has no verdict for anywhere else, and refusing it would refuse a construct that cannot change the generated HLSL. The rewriting drops a walked subtree in five places, the four attribute lists and the entry point's explicit interface specifier, and only type name syntax can appear under the latter, every kind of which is in the set. Everything else it drops is a modifier, which is a token and never reaches the report. The description the diagnostic carries says how the set grows, an author whose construct HLSL can express having no way to read that from the refusal itself. The ancestors are read only for a kind outside the set, and before the kind is recorded as seen, so the same kind written elsewhere in the same method is still refused. A kind is reported once per rewriter, so a construct used many times in one method gives one report. Rebuilding the solution with the severity raised reports nothing, and putting one `switch` expression into a shader on each path reports `SwitchExpression`, `SwitchExpressionArm`, `ConstantPattern` and `DiscardPattern` on both, so that nothing is a measurement and not a silence. The change that added the diagnostic rewrote nothing, which was measured: of the 857 files the generators write when the solution is built, 855 are byte identical to the ones the commit before it writes, and the two that are not are the Direct2D shaders compiled with debug information, whose blob carries the path of the source and a mark that changes between two builds of the same unchanged tree | `bd2c86cc`, `c3ec4dac`, `7977e1b5`, `2a994f5d`, `91d5e692`, `95fffcc3` |
| Generated HLSL, syntax written inside an attribute list | Walks an attribute list and drops it afterwards, each declaration clearing its own lists once the base visit has returned, so everything written inside one is refused the way the same construct is refused in a body. A string argument stops the build over an attribute the generated HLSL never holds, and an argument that is a string concatenation, a cast to object, a null, an array creation or a checked expression does the same under three other identifiers; the author cannot correct any of them in the source, the list being dropped either way. Only the record of syntax the accepted set has no verdict for reads the ancestors to hold itself back there, which is a rule per refusal rather than a rule about the place | Returns an attribute list without walking it, so nothing inside one reaches a refusal at all and a refusal added later needs no rule of its own. The five places that dropped a list after walking it are gone, four of them clearing the lists of a parameter, a method or a local function, and the record of unknown syntax no longer reads the ancestors, that condition being one this makes always true. The generated HLSL is unchanged: a type named in an attribute argument was measured not to be recorded among the discovered types either way, the argument of an attribute reaching no site that records one | `07a9871b` |
| `CMPWD2D0041`, the invalid discovered type message | Repeats the clause "and custom types containing these types can be used" twice, in the message and in the description alike. The compute counterpart `CMPW0050` writes it once, so the pair disagrees, and a reader of the Direct2D message meets the same sentence twice in one parenthesis | Writes it once, which makes the two sides read alike. The rule that found it was written down before the count: split every message, description and title the two generators declare on commas, and look for a clause of at least twenty characters that appears twice in the same string. Over the 597 strings declared today it matched this pair and nothing else, and nothing at all after the change. No permanent test enforces the rule: the Direct2D generator does not open its internals to its test project, and opening them to hold one sentence in place is a wider change than the sentence is worth | `15feb506` |
| Generated HLSL, extension method calls | Imports the declaration of an extension method as it stands, with the receiver as its first parameter, and rewrites the call site by name alone. C# leaves the receiver out of the argument list, so the declaration takes one argument more than the call supplies and the shader fails to compile against a generated function name the author never wrote. Every receiver is affected alike: a value, a custom type, an `in` receiver, a `ref` receiver that writes to itself, an HLSL primitive, a chained call, and a call with further arguments | Moves the receiver into the first position of the argument list. Which calls have a receiver to move is asked of the semantic model: the target method of the operation is the unreduced symbol whether or not the call was written with a receiver, so only the symbol the call itself binds to carries a reduced form. Comparing the argument count of the syntax against that of the operation was measured and rejected, a call that omits an optional argument giving the same one-short shape as a call that omits its receiver. The receiver modifiers already came out right, a value staying a value, `in` staying `in` and `ref` becoming `inout`, so nothing but the call site changed. Measured on the device: eleven shapes, the writing `ref` receiver among them, compute the same value as the same source run in C#. A generic extension method is still refused by `CMPW0117`, which runs first | `43b7095c` |
| Generated HLSL, extension member calls | Reaches a called method through the static path or, for an instance method, only when the declaring type is a struct. A member declared in a C# extension block is an instance member of neither, its declaring type being one the author cannot name, so the declaration is never imported and the call is written out as it stands. The body the author wrote never runs and the HLSL compiler rejects a member it never saw, naming generated code. An extension property is already reported, so the two halves of the same language feature answer differently | Reports `CMPW0119`, or `CMPWD2D0092` on the Direct2D path, for a call whose declaring type cannot be referenced by name. The check sits beside the generic method check in the shared rewriter, so the shader body and a static field initializer answer alike. It asks about nameability rather than the type kind because the kind for an extension declaration was added to Roslyn after the version the generators compile against. An extension method declared with a `this` parameter is unaffected, and so is a static method written inside an extension block, both of which belong to the enclosing class and keep their existing import path | `038e3ed7`, `bfd61c5c`, `d50d9083` |
| Generated HLSL, static field initializers | Rewrites a static field initializer with a rewriter that only maps intrinsics. A call to a static method declared outside the shader is written out as it stands, naming a type the generated HLSL never declares, and the shader compiler rejects it while naming generated code the author never wrote. The same call one line away in the shader body is imported, so where it is written decides whether it works | Imports the declaration the same way the body does, and renames the call to it. The forward declarations are written ahead of the static fields, so a call to an imported function is in scope where the initializer needs it, which was measured on both the DXC and the FXC path before the import was written. The local functions lifted out of an imported method are carried out to the caller and written like any other. The static methods are gathered after the static fields, an initializer being able to import one of its own | `70550094` |
| Generated HLSL, a constructor in a static field initializer | Hands a user defined constructor call to an extension point whose default writes a default value, and overrides that default only in the rewriter for the shader body. An initializer holds zero where the same call one line away in the body constructs the value, and nothing is reported on either path: the generated HLSL still compiles, so the only sign is the number the shader computes | Imports the constructor the way the body does, the initializer rewriter handing the call to the one that rewrites bodies, a constructor declaration being a body. The arguments keep the rewriting the initializer gave them, the import reading them from the node it is handed rather than visiting them, and the local functions it lifts out are carried over the way an imported method's already are. The stubs are written among the type declarations, which both generators write ahead of the static fields, so the call is in scope where the initializer needs it, measured on the DXC path and on the FXC path alike; the value the field holds is read back from a device rather than inferred from the two paths writing the same source. The parameterless constructor a struct always has stays a default value, which is what it computes in C# as well. The extension point carries no default any more, one answering with a default value having computed something other than what the author wrote without saying so, so a rewriter that will not import a constructor states what it does instead | `b3bd8a3a`, `40fe41df`, `03433ef5`, `3220e6f4` |
| Generated HLSL, a static field initializer reaching itself | Rewrites the initializer of an external static field and adds the entry to the collection of static field definitions after the rewriting finishes, where the two other collections a rewriting can return to claim their entry before it. An initializer that reaches the field it initializes therefore finds no entry, rewrites the field a second time and adds it, and the outer rewriting adds the same key again when it returns. The generator faults with an argument exception, which discards the descriptors for every shader in the compilation unit and leaves the author with errors that name none of this. The two routes that close the cycle are a method and a constructor the initializer imports | Claims the entry before rewriting, the way the other two collections do, and treats a return to a claimed entry as a cycle, reporting `CMPW0124` on the compute path and `CMPWD2D0096` on the Direct2D one. Claiming alone was measured to be insufficient: the fault stops, no diagnostic is reported, and the shader compiler accepts generated HLSL that reads a global static through a function reading it back, so the fault would become a silently different value, which is the worse of the two in the order this tree keeps. The mark is an empty type declaration, which widens the type alias for a static field; the two places that read it both read an array built after every rewriting finishes, and building both generators over the widened alias reports no warning. One signature wrote the collection as a raw tuple rather than as the alias, which the widening turns into a nullability error, so it is aligned with the other three. The report is located on the read that closes the cycle and not on the field declaration: a cycle closed from two imported declarations is two places the author has to change, and one report on the declaration names neither of them. A static field of the shader itself reaches neither the report nor the fault, every call site excluding fields of the shader type and such a field being collected into an array rather than into a dictionary. An initializer reading a static field directly reaches the report as well, that read being imported the way the shader body imports one. | `da160eb1`, `7ac9a03d`, `a1a12303`, `fabf4202` |
| Generated HLSL, static field reads in an initializer | Rewrites a static field initializer with a rewriter that answers for a constant and for nothing else a field can be. A read of a static field declared outside the shader is written out under the name the author wrote, naming a type the generated HLSL never declares, and a read of the shader's own static field through the type name keeps the type name on it. The shader body imports the first and drops the type name from the second, one line away. The initializer of an imported field is rewritten by the same rewriter, so the body meets this as well as soon as an imported field's own initializer reads another one by name alone. Nothing is reported on any of these paths, the shader compiler answering instead by naming generated code the author never wrote. The fields the body does import are written after the ones the shader type declares, and among themselves in whatever order the collection holding them enumerates, so a read that resolved to an import would still name a declaration written after it | Reads a static field the way the body does, handing the read to the rewriter that imports every other declaration an initializer reaches, the way the import of a constructor is already handed to it, and carrying back the local functions that import lifted out. A field of the shader type is written under the name it has in the shader rather than imported, which is what the body does with it. A read written by name alone is intercepted before the member access path, an identifier being where a read of a field of the enclosing type arrives, and a read the import declines falls back to what the base rewriter would have done, which keeps a constant and an enum member on the path they already had. Every static field, imported or declared on the shader type, is written as one sequence ordered by how many had finished when its own rewriting did, which is after every field its initializer reached, so each is written after the ones it names; the count is carried on the model for a static field rather than left to the enumeration order of the collection. Writing the imported ones as a block ahead of the declared ones was measured to be insufficient, a field declared on the shader type and read from an imported initializer landing after the read. The import claims its entry before rewriting, so an initializer reaching the field it initializes through one of these reads is answered by `CMPW0124`, or `CMPWD2D0096` on the Direct2D path, rather than by a fault | `3bc1bcca`, `19fe5d51` |
| Generated HLSL, a variable a static field initializer would have to declare | Writes an out argument into a call in a static field initializer as the author wrote it, declaration and all, so the generated HLSL declares a variable inside an argument list and the shader compiler rejects it while naming generated code. A discarded out argument answers the same way, the discard reaching the generated HLSL as an identifier nothing declares. The shader body declares the variable at the start of the body and passes it to the call, so where the author writes the call decides whether it works | Reports `CMPW0126`, or `CMPWD2D0098` on the Direct2D path, at the declaration and at the discard alike. The two reporting sites are the two places the rewriter for a body introduces such a variable, so what is refused is what that rewriter hoists rather than the shapes that happened to be measured. Giving the variable a global static of its own instead was rejected: one storage would be shared by every invocation of the shader, and HLSL leaves the order of global static initializers undefined, which is the hazard `CMPW0124` already refuses. The report names the source the author wrote, where the failure it replaces named the shader type and quoted a line of generated HLSL | `121de6e8` |
| Generated HLSL, the requirements a shader raises | Keeps what a rewriting gathers on the rewriter that gathered it, and carries it out by hand wherever one rewriter creates another. Which of those places carry anything is left to a hook the specialized rewriter may implement, and a hook nothing implements is silent. On the Direct2D path the scene position requirement is raised inside the rewriter for a called method, an instance method, a constructor, the initializer of an external static field, an initializer of the shader itself, and a declaration imported by one, and reaches the generator from none of them, so `CMPWD2D0045` answers only for a call written in the shader body. The generated HLSL then reads the position without the define its header needs, and the author is left with the failure FXC raises against generated code, which names a line of generated source and not the attribute to add. On the compute path the three initializer routes lose the requirement that a dispatch cover whole thread groups, so a range that cuts a group in half is accepted for a shader that waits for one. Two of the hooks are declared and implemented with no call site anywhere in the tree | Holds the requirements in one instance per shader, handed to every rewriter created for it, so a requirement is raised where it is found and read where the shader is written, with no path in between having to carry it. Which requirements exist still differs between the two generators, so they are declared in a partial declaration each one carries, the way the rewriters themselves already are, and a rewriter that is not handed them does not compile. The two hooks with no call site are removed along with the carrying they existed for, and so is the suppression that hid them: measured over both generators, that rule had nothing else to report, so a hook declared and never called now stops the build. The other rule in that suppression is kept, one field being assigned and never read where the shared file compiles into the Direct2D generator, and what it hides is written beside it. The hook that raises a requirement from a mapped call moves to the type both rewriters derive from: what a call requires does not depend on whether it was written in a body or in an initializer, and declaring it twice was the shape that left one of the two uncalled. The sampler requirement is left where it is, a texture being refused as a static field and as a parameter alike, so only a rewriting of the shader's own members can raise it, which was measured rather than read. Measured on both paths over the six places a rewriter creates another one and the one each generator creates for a static field of the shader itself | `44f39c86`, `6b8f4af7`, `f937b857`, `33756710` |
| Generated HLSL, primary constructors | Refuses the construction of a type declaring a primary constructor, which is deliberate, the captures of such a constructor reaching the members of the type in ways the rewriting cannot follow. It reports the diagnostic for a constructor with no source to analyze, because the check that reaches it is whether a constructor declaration can be found, and a primary constructor has none of its own. The author reads a reason that is visibly not the one, the source being right there, and the same refusal covers a constructor from another assembly, for which the reason is correct | Reports `CMPW0120`, or `CMPWD2D0093` on the Direct2D path, when the constructor is declared in source, and keeps the older diagnostic for one that is not. The two are told apart by whether the constructor has any declaring syntax reference at all, a primary constructor having one on the type declaration. The refusal itself is unchanged, and so is the shader type's own primary constructor, whose captures become the shader fields | `00f95213` |
| Generated HLSL, generic local functions | Lifts a local function to a top level HLSL function whether or not it is called, carrying its type parameter list with it, and asks whether a method is generic only where a call is rewritten. A generic local function that is never called reaches the HLSL compiler with `<T>` on it, and one that is called is reported against the call rather than against the declaration the author has to change | The declaration is refused, so the report names it, and the lifted function, which still carries its type parameter list, never reaches the shader compiler. The call site leaves a local function alone, because the declaration has already answered and two reports would name two places for one cause | `e364c7e4` |
| Diagnostic descriptions | Writes the description of `CMPW0041` and of `CMPWD2D0032` with a `{0}` in it. Only the message format is given the arguments, so the placeholder is read as it stands wherever the description is shown, including the error log written with the ErrorLog switch | Both descriptions state the limit without naming the type, which is how the descriptions around them are written. A test refuses a placeholder in any string the arguments never reach | `3ed210de` |
| `ComputeWeave.Dxc` native library copies | Chooses which native libraries are copied to the output with `'$(Platform)' == 'x64' OR '$(CI_RUNNER_DOTNET_TEST_PLATFORM)' == 'x64'`, so the environment variable alone decides x64 however the build was invoked: a build that names ARM64 on a machine where the variable is set places x64 libraries. The ARM64 branch repeats the same x64 term, and being reached only when the first branch did not match, that term can never hold; the branch is entered when `Platform` names ARM64 and the variable is unset, and at no other time | Resolves the platform once, ahead of the `Choose`: an explicit `Platform` decides, and the variable is the default only when none was given. The ARM64 branch reads the variable for its own name, so a variable naming ARM64 selects ARM64, where before it fell to the default that copies both architectures under `runtimes/<RID>/native`. `build/verify-dxc-native-copy.ps1` evaluates the project over seven combinations of the two and compares what is copied against a list held in the script, and CI runs it. What the package carries is unaffected, the packing items sitting outside the `Choose`. The audit script inspects only `.cs` files, so this row is the sole record of the divergence | `bee12c36` |
| Generated HLSL, integer literals | Writes a float or a double literal from the token value, so the spelling the author used never leaves the generator, but leaves an integer literal as it was written. The generated HLSL then carries C# literal syntax: a digit separator reaches the shader compiler, which rejects it while naming a line of generated code the author never wrote, and a binary literal is accepted by DXC and rejected by FXC, so the same shader body compiles or not depending on which path received it | Writes an integer literal from the token value as well, keeping the `u` suffix for an unsigned literal so a value past the signed range does not change meaning. The generated HLSL no longer carries a spelling, so it does not depend on which literal forms the compiler in front of it accepts. The three separated spellings and the binary one were measured on both compilers before and after. No shader in this repository writes a hexadecimal, binary or separated literal, so the generated files are unchanged, which was compared in full | `5b4dd602` |
| Generated HLSL, native integer types | Refuses a discovered type whose fully qualified name begins with `System.`, but reads that name from how the type displays rather than from what it carries in metadata, a few lines after the lookup for a mapped type reads the metadata name. A native integer type displays as `nint` or `nuint`, so it is taken for a struct the author declared; of the ten integer and character types measured here it is the only kind that displays under a name other than the one it carries in metadata: the generated HLSL declares `struct System_IntPtr` and writes `nint` at the use site, and the shader compiler rejects the identifier while naming generated code the author never wrote | Reads the metadata name in both places, so the two agree. A native integer type is refused like every other .NET integer that carries no mapping, with `CMPW0050` on the compute path and `CMPWD2D0041` on the Direct2D path. The eight .NET integer and character types were measured on both paths before and after, and a struct the author declares is still collected. The generated files are unchanged, which was compared in full | `a0ac42fc` |
| Generated HLSL, converted intrinsic arguments | Writes an argument as it stands, so the shader compiler resolves the call again over the type the argument had before the conversion C# applied. An intrinsic given a signed integer beside an unsigned one binds to the floating point overload in C# and to the unsigned one there, and the shader computes another value with no diagnostic, no compiler error and no runtime failure: a maximum reads a negative value as a large positive one, and a dot product wraps at 32 bits | Writes the conversion out, as a cast to the type of the parameter the argument binds to, so the generated call states the signature C# chose rather than resting on the shader compiler promoting the way C# does. The judgment is the type and not the pair of kinds, which is what closes the family instead of the one combination measured to diverge. It sits before the named intrinsics are lowered, `Select` being one of them and taking arguments whose kinds can be mixed this way, and both rewriters carry it on both generators, FXC resolving such a call the way DXC does. Of the 590 files the generators write for the compute test project, 26 change and 134 lines with them, every one of which is identical to the line before it once the casts and the parentheses are removed. All 202 casts added are to a floating point type, so no shader in this repository changes the value it computes | `e57fe1cf` |
| Generated HLSL, operands widened past the type set | Writes an operation out with its operands as they stand, so the shader compiler resolves it again over the types they had before the conversions C# applied. A signed integer beside an unsigned one is brought to a 64 bit integer there, which the HLSL type set has no name for, so unlike an argument conversion this one cannot be written out at all: the operation is resolved as unsigned instead, and a comparison answers the other way while an arithmetic result wraps at 32 bits, with no diagnostic, no compiler error and no runtime failure. The same widening is reached by a binary operator and by negating an unsigned value, while a conditional with one arm of each kind has no natural type at all and is target typed instead | Reports `CMPW0125`, or `CMPWD2D0097` on the Direct2D path, for an operation whose operands C# brings to a type outside the HLSL type set. The judgment is that type and not the pair of kinds that reached it, which is what closes the family rather than the one combination measured to diverge. It sits in the walk over resolved operations the shared rewriter already makes for an operator it cannot express, so the shader body and a static field initializer answer alike, and it is read from an operand rather than from the result because a comparison answers with a `bool`. Only the innermost such operation is reported, one holding another widening a result that is already outside the set. Five operations in the shaders of this repository were brought to one kind to satisfy it, each of them a division whose operands are non negative, so what the generated code computes is unchanged | `f5c04228` |
| Language version | `preview`, so what the sources compile as changes with the SDK in use | `14.0`, the version the sources actually require; a simple lambda parameter modifier makes `13.0` insufficient | `5d5f476f` |
| Shader compilation after a refusal | Builds the HLSL for a shader and hands it to the shader compiler whether or not the rewriter has already refused the input, so a refused construct is answered twice: once against the source the author wrote, and once by `CMPW0046` or `CMPWD2D0034` naming a line of generated code, whose text asks the author to open an issue for a shader the generator itself declined to translate. `HlslBytecodeInfoKey.IsCompilationEnabled` is documented as covering errors earlier in the pipeline, and the Direct2D generator carries a comment saying that compilation is done last so it can be skipped when errors happened before, but neither generator ever reads the diagnostics it has collected | Both read them, and disable compilation when a diagnostic of error severity is already present. The severity read is the one the descriptor declares, so a report that refuses nothing leaves compilation enabled: syntax outside the accepted set still reaches the compiler, and the failure it raises still reaches the author. An input that is not refused is unaffected, the forwarding it may produce included. Reverting the change makes 31 refusal assertions in the compute suite and 42 in the Direct2D suite carry the forwarded failure again | `26590ffd` |
| Diagnostic titles | Six descriptors carry the title of another diagnostic. `CMPW0018` and `CMPW0019` are named after the foreach statement they follow, as are `CMPWD2D0010` and `CMPWD2D0011`; `CMPWD2D0057` is named after the missing compile options it follows, and `CMPWD2D0058` after the missing resource texture index attribute. The title is the name tooling shows for a rule, so a `lock` statement in a shader is reported under a name that says foreach. The description of `CMPWD2D0057` also writes `using` twice | Each names its own diagnostic, and the description writes the word once. The set was measured over all 199 declared descriptors by two rules: two descriptors in one assembly carrying the same title, and an identifier and a title sharing no word. Their union is the six above; the second rule alone misses `CMPWD2D0057`, whose identifier and title do share words. The first rule is held by a test on both sides, so a title copied from another descriptor cannot come back unnoticed; a title that is wrong without being a copy is caught by neither rule | `79190cd2` |
| A dispatch under a shader that waits for its whole thread group | Rounds the dispatch up to whole thread groups and has the entry point run the body only for the threads inside the requested range. A shader that reaches one of the three barriers that synchronize the group is run that way as well, so a range that is not a multiple of the thread group size leaves the last group partly out: the threads left out never reach the barrier, and never write the group shared state the ones inside it read. Measured over a range of 100 with a group of 64, 28 of the 100 values disagreed, on both devices and on every run. The undefined dispatch also damages the device for the work that follows it: two later tests of the same run fail, and pass when run on their own | The generator marks a shader that reaches such a barrier, the descriptor carries the mark, and the dispatch refuses a range that is not a multiple of the thread group size on every axis. A range that is a multiple leaves the entry point unchanged, the check inside it being uniform across the group. The mark is written only for a shader that carries it, so the generated code of every other shader is unchanged, which was compared in full | `c2d6b452` |
| The message a shader compiler failure carries | Tells the author to open an issue `an include` a working repro. The two exception types that build the message also document their argument as a `compilatin` error message, and the one for FXC attributes the message to DXC | Reads `and include`. The two documentation slips are corrected with it; those two change no behavior | `1207b5a6` |
| Root signature size analysis | Counts the captured resources only, so a pixel shader like type is measured one DWORD short of the signature the runtime builds: the implicit output texture is bound as a descriptor table and costs a DWORD that the analysis omits. A type whose analyzed size lands on the 64 DWORD limit therefore builds without a diagnostic and fails at dispatch, where `D3D12SerializeVersionedRootSignature` returns `E_INVALIDARG` and the author sees only an invalid parameter | Counts the implicit output texture as well, so the analyzed size is the size of the signature the runtime builds. The types this newly refuses are exactly those the runtime already refused, measured by dispatching one of them before the change | `feb631da` |
| Thread group total size | Bounds each axis on its own and never the group as a whole, in the analyzer and in the generator alike, so a size whose axes are all in range but whose threads exceed what one group may hold reaches the shader compiler. The refusal arrives as `CMPW0046`, which points at a line of generated HLSL and invites the author to open an issue, for an attribute value the hardware does not allow | Both bound the total as well, so the size is refused as `CMPW0044` at the attribute and the generator stops before the compiler, the way it already does when an axis is out of range. `CMPW0046` stays reachable through limits neither side models, and the test that covers it now uses group shared storage past its own maximum | `5b3cdeb7` |
| Trivial sampling over the full set of inputs | Bails out when the input count is the maximum the shader format allows, so a shader carrying every input it may declare never reaches the check, and can declare trivial sampling over an input that is not simple without a word. The bound is written as one below the maximum, while the analyzer for input descriptions writes the same bound inclusively | Accepts the maximum as well. The check reads a fixed buffer of that size and slices it to the count, so accepting it reads nothing beyond the buffer. The neighboring bound in the same method is left alone: it reads an input index, whose last valid value is one below the count | `12b192a1` |

The ledger is kept current by derivation, not by memory: `build/audit-upstream-divergence.ps1` lists every commit after the marker below that modifies an inherited `.cs` file under `src/` and is not cited by a row. That is the whole of what it reads. Inherited project files, documents and test sources are outside it, so a divergence in one of those reaches the ledger only because someone wrote the row by hand. Run it before a release. When the queue is not empty, either add a row or decide that the commit is not a divergence, and then move the marker. The ledger is audited through commit `3843a284`.

Record the attempts that failed, too, and why. A rejected approach with its cause documented is worth more to the next contributor than a clean history that invites the same mistake twice.

If a change alters what the library guarantees, update [README.md](/README.md) and [README.ja.md](/README.ja.md) in the same pull request.

---

## Code of Conduct

All contributors are expected to follow the repository's [Code of Conduct](/CODE_OF_CONDUCT.md).
