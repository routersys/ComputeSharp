# ComputeWeave

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](#)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](#)
[![Release](https://img.shields.io/github/v/release/routersys/ComputeWeave.svg)](https://github.com/routersys/ComputeWeave/releases)

English | [日本語](https://github.com/routersys/ComputeWeave/blob/main/README.ja.md)

---

ComputeWeave is a fork of [ComputeSharp](https://github.com/Sergio0694/ComputeSharp), the library that lets DirectX 12 compute shaders be written entirely in C#.
That base is unchanged and is documented upstream; this document covers what the fork adds on top of it.
The addition is a declarative layer: a compute pipeline and its resources are declared with attributes, a source generator turns the declaration into a canonical binary descriptor embedded in the assembly, and the runtime reads that descriptor to bind resources, record command lists and track completion.
The same layer carries shared textures and shared fences across the Direct3D 11 and Direct3D 12 boundary, and adds a GPU memory budget.

---

## Table of Contents

1. [Overview](#overview)
2. [Requirements](#requirements)
3. [Installation](#installation)
4. [Features](#features)
   - [1. Declarative compute pipelines](#1-declarative-compute-pipelines)
   - [2. Owned resource slots](#2-owned-resource-slots)
   - [3. Direct3D 11 interoperation](#3-direct3d-11-interoperation)
   - [4. Shared texture slots](#4-shared-texture-slots)
   - [5. Read-only buffer views](#5-read-only-buffer-views)
   - [6. GPU memory budget](#6-gpu-memory-budget)
   - [7. Compile-time validation](#7-compile-time-validation)
5. [API Reference](#api-reference)
   - [Declaration attributes](#declaration-attributes)
   - [Generated members](#generated-members)
   - [Runtime](#runtime)
   - [Slots and bindings](#slots-and-bindings)
   - [Interoperation](#interoperation)
   - [Shared resources](#shared-resources)
   - [Memory](#memory)
   - [Enumerations](#enumerations)
6. [Limitations](#limitations)
7. [Notes](#notes)
8. [Disclaimer](#disclaimer)
9. [Third-Party Licenses](#third-party-licenses)
10. [License](#license)

---

## Overview

The base library is unchanged. A compute shader is a `partial struct` implementing `IComputeShader`, `GraphicsDevice.GetDefault()` returns the device, and `For` dispatches. Nothing in this document replaces that.

What the fork adds is 61 public types and additional members on `GraphicsDevice` and `InteropServices`. They form one system. A type marked `[ComputePipelineHost]` declares a device field, a set of resource slots and a set of pipeline methods. The source generator reads that declaration, writes a canonical descriptor as a byte array in the generated partial, and emits typed members that forward to the runtime. At construction the runtime parses the descriptor, validates every contract against it, and from then on the descriptor is the single source of truth for ordinals, resource access and structural limits.

Resources are not held directly. They live in slots that publish generations: `TryEnsure` asks a slot to match a requested plan, and a new generation is published only when the plan actually changes. Work in flight keeps the generation it captured alive, so resizing a resource does not invalidate submissions already recorded.

### What is guaranteed

Every path that reaches the GPU through this library is tracked. Lifetime tracking answers whether a native resource may be released. Hazard tracking answers whether accesses to it are ordered across queues. They are separate properties, and the library states which of them it provides.

| Path | Lifetime | Hazard |
|---|---|---|
| Generated pipelines, `ComputeContext`, resource copies, interop domains | Yes | Yes |
| `InteropServices.AcquireNativeResource` and `AcquireNativeDevice` | Yes | No |
| `InteropServices.GetID3D12Resource`, `GetID3D12Device` and the mapped views of transfer resources | No | No |

A native reference holds the resource generation alive while an object outside the library uses it, and reports the completion points of the work already submitted so the holder can order its own work without blocking. It does not order that work for you. Interoperation that needs ordering uses an interop domain instead.

The last row is the escape hatch inherited from the base library. It is kept for compatibility and stays untracked.

---

## Requirements

| Item | Requirement |
|---|---|
| OS | Windows 10 or later (64-bit) |
| Runtime | .NET 10.0 |
| GPU | A Direct3D 12 device at feature level `D3D_FEATURE_LEVEL_11_0` and shader model `D3D_SHADER_MODEL_6_0` |
| Fallback | A WARP device is used when no such GPU is present |
| Interoperation | Shared textures require an adapter able to create shared handles for both Direct3D 11 and Direct3D 12 |

---

## Installation

```bash
dotnet add package ComputeWeave
```

Optional extension packages:

```bash
dotnet add package ComputeWeave.Dxc
dotnet add package ComputeWeave.D3D12MemoryAllocator
```

`ComputeWeave.Core` is a transitive dependency and is not referenced directly.

---

## Features

### 1. Declarative compute pipelines

A host is a `partial` type marked with `[ComputePipelineHost]`. The first argument names the field holding the device, the second is the number of concurrent invocations to reserve. A pipeline is a method marked `[ComputePipeline]` whose first parameter is `in ComputeContext`.

```csharp
using ComputeWeave;

[ComputePipelineHost("device", 1)]
public sealed partial class Host
{
    private readonly GraphicsDevice device;

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
    private readonly ComputeResourceSlot<ReadWriteBuffer<int>> index = new();

    [ComputePipeline]
    private void Run(in ComputeContext context)
    {
    }
}
```

The generator emits into the same partial type a static `Create` factory, `Dispose`, `WaitForDisposal`, and for each pipeline an overload of the same name that takes the declared arguments without the context and returns `ComputeSubmission`. The overload is public unless a parameter type is less accessible.

```csharp
using Host host = Host.Create(GraphicsDevice.GetDefault(), maximumPendingSubmissions: 4);

ComputeSubmission submission = host.Run();

submission.Wait();
```

`ComputeSubmission` carries a `FencePoint`, a `ComputeSubmissionStatus` and `IsCompleted`. Waiting is explicit; a submission is not awaited implicitly at disposal.

### 2. Owned resource slots

A resource owned by a host is declared as a field of `ComputeResourceSlot<TResource>` or `ComputeResourceGroupSlot<TGroup>`, annotated with `[ComputePipelineResource]` and initialised with `new()`. The `TGroup` of a group slot is a `sealed partial class` marked with `[ComputeResourceGroup]`, whose members are get-only properties annotated with `[ComputePipelineResource]`. The generator emits `TryEnsure<Slot>(in <Plan> plan, out bool changed)` and, for single-resource slots, `Get<Slot>ComputeBinding()` returning a `ComputeResourceBinding<TResource>`.

`TryEnsure` reports whether the owned resources match the requested plan, and `changed` reports whether a new generation was published. `ComputeResourceRecovery` selects what happens to the contents when a generation is replaced: `Discardable`, `RecreateFromHost`, `Recompute` or `CapacityOnly`.

A pipeline reaches the owned resources through a parameter marked with `[ComputeOwnedResource]`, naming the slot field. A `ComputeResourceSlot<TResource>` provides its `TResource`, a `ComputeResourceGroupSlot<TGroup>` provides its `TGroup` with every member assigned. Such a parameter is removed from the generated overload, as the caller does not supply it, and it refers to the generation pinned for that invocation rather than to whichever generation is active while the body runs.

```csharp
[ComputePipeline]
private void Run(
    in ComputeContext context,
    [ComputeOwnedResource(nameof(index))] ReadWriteBuffer<int> index,
    [ComputeOwnedResource(nameof(grid))] GridResources grid)
{
    context.For(index.Length, new Shader(index, grid.Cells));
}
```

### 3. Direct3D 11 interoperation

An external API is connected by implementing `IComputeExternalInteropProvider<TView>` and registering it. The provider is asked to initialise a shared timeline, to enqueue signals and waits on its own queue, and to open a shared texture as its own view type.

```csharp
using ComputeInteropDomain domain = device.RegisterExternalDomain(provider);
```

`ComputeInteropDomain` exposes `Device`, `Id`, `Capabilities` and the disposal pair `Dispose` / `WaitForDisposal`. `ExternalInteropCapabilities` reports `SharedFence`, `SharedTexture2D`, `SingleImmediateContextOrdering` and `PersistentExternalViewOrdering`. Providers whose queue must be entered and left around each operation derive a `ComputeExternalQueueScheduler`.

A provider that throws leaves its external queue in a state the runtime cannot reason about, so the domain is poisoned. Every subsequent operation on that domain, and every borrow or lease taken from it, reports the failure the provider raised. Other domains on the same device are unaffected.

### 4. Shared texture slots

A resource set is a `partial` type marked `[ComputeInteropResourceSet]` holding `SharedTextureSlot<T, TPixel, TView>` fields annotated with `[ComputeSharedTexture]`. The attribute fixes the resize policy, the access on each side, the external usage, the alpha mode, the initial owner and the recovery.

```csharp
using System;
using ComputeWeave;

[ComputeInteropResourceSet]
public sealed partial class ResourceSet
{
    [ComputeSharedTexture(
        ComputeResourceResizePolicy.Exact,
        ComputeResourceAccess.ReadWrite,
        ExternalResourceAccess.Write,
        ExternalTextureUsage.RenderTarget,
        ComputeAlphaMode.Premultiplied,
        ComputeSharedTextureInitialOwner.External,
        ComputeResourceRecovery.RecreateFromHost)]
    private readonly SharedTextureSlot<Bgra32, Float4, ExternalView> source;
}
```

The generator emits `Create(GraphicsDevice device, ComputeInteropDomain domain)` and, per slot, `TryEnsure<Slot>(int width, int height, out bool changed)`. `TryGet<Slot>AllocatedSize` delegates to `SharedTextureSlot.TryGetAllocatedSize` and reports the allocated width and height of the published texture, which can remain larger than the logical dimensions under `GrowOnly`. The result is an unpinned snapshot and does not describe a binding, borrow or lease acquired separately when generation replacement can run concurrently. The `Width` and `Height` of an `ExternalTextureLease<TView>` describe the allocated dimensions of the generation held by that lease. Ownership is handed over through the shared fence: `BeginExternalOperation` borrows the view for the external API, `AcquireExternalViewLease` takes a lease that outlives a single operation, and `GetComputeBinding` returns the compute-side binding.

Retiring a shared texture generation, whether by resizing it or by disposing its slot, drains the external queue before the external view is released. That drain runs on the device rather than on the calling thread, so the retired generation is still held when `TryEnsure` or `Dispose` returns. A foreground operation waits when that internal maintenance operation temporarily holds the domain, while another foreground operation remains a conflicting use and is rejected. A provider that throws poisons its domain, and every later operation on that domain reports the failure. `WaitForDisposal` waits for retirement and disposal to complete.

### 5. Read-only buffer views

`ReadWriteBuffer<T>.AsReadOnly()` returns an `IReadOnlyBuffer<T>`. The view binds the same resource through its SRV, so a shader taking it cannot write to it.

```csharp
using ReadWriteBuffer<int> source = device.AllocateReadWriteBuffer<int>(length);

device.For(length, new ProduceShader(source));
device.For(length, new ConsumeShader(source.AsReadOnly(), destination));
```

A buffer produced on the GPU can be handed to later shaders as read-only without copying it into a read-only buffer. Copying between two `Buffer<T>` instances blocks the CPU until the GPU completes, so this keeps that wait out of a per-frame path.

Unlike the texture counterparts, no state transition is involved: a buffer resides in `COMMON` and needs no transition to be read through an SRV. The returned view stays valid for the whole lifetime of the buffer and can be cached and reused. `ReadOnlyBuffer<T>` also implements `IReadOnlyBuffer<T>`, so either one can be passed to the same parameter.

### 6. GPU memory budget

`GraphicsDevice` gains three members. `SetMemoryPolicy` installs hard limits per memory segment and, optionally, an `IGraphicsMemoryBudgetBroker` that arbitrates between clients. `GetMemoryStatistics` returns a `GraphicsMemoryStatistics` snapshot carrying an epoch, per-segment statistics and generation counts. `TrimMemory` releases what is retired and idle. A generation is idle only once the work and the external queue that held it are done with it, so trimming right after the call that retired it reclaims nothing.

Allocation failures caused by the budget surface as `GraphicsMemoryAllocationException`, which derives from `InvalidOperationException`.

The budget covers the resources the device creates itself. A device using an allocator configured through `AllocationServices.ConfigureAllocatorFactory`, such as the one in the `ComputeWeave.D3D12MemoryAllocator` package, creates its resources through that allocator instead. Those resources are not admitted against the policy, are not counted in the statistics and are not reclaimed by `TrimMemory`. Budget them in the allocator.

**A configured allocator and the declarative layer are mutually exclusive.** Generations, trimming and the budget all rest on the device owning its allocations, so a device that allocates through an external allocator cannot host them: `ComputeHostRuntime.Create`, `ComputeInteropResourceSetRuntime.Create` and the generated `Create` factories throw `NotSupportedException` on it. The base library, `ComputeContext`, resource copies and `InteropServices` are unaffected. Pick one of the two.

### 7. Compile-time validation

The declarations above are checked by analyzers that report 95 diagnostics with the `CMPW` prefix, covering attribute placement, host and pipeline method shape, slot declaration, resource contracts and generated overload conflicts. Some carry a code fix.

---

## API Reference

### Declaration attributes

| Member | Description |
|---|---|
| `[ComputePipelineHost(string deviceFieldName, int maximumConcurrentInvocations)]` | Marks a partial type as a pipeline host. |
| `[ComputePipeline]` | Marks a method as a pipeline. Its first parameter must be `in ComputeContext`. |
| `[ComputePipelineResource(ComputeResourceAccess access)]` | Declares a resource borrowed by a host, or a member of a resource group. |
| `[ComputePipelineResource(ComputeResourceAccess access, ComputeResourceRecovery recovery)]` | Declares an owned resource slot with its recovery class. |
| `[ComputeResource(ComputeResourceAccess access)]` | Declares the access contract of a graphics resource parameter of a pipeline method. `Sharing` and `Aliasing` are settable. |
| `[ComputeOwnedResource(string slotFieldName)]` | Binds a pipeline parameter to the resources of an owned slot. |
| `[ComputeResourceGroup]` | Marks a sealed partial class as a resource group. |
| `[ComputeInterop]` | Marks a pipeline method as an external interop round-trip. |
| `[ComputeInteropResourceSet]` | Marks a partial type as an interop resource set. |
| `[ComputeSharedTexture(resizePolicy, computeAccess, externalAccess, externalUsage, alphaMode, initialOwner, recovery)]` | Declares a shared texture slot. |

### Generated members

| Member | Description |
|---|---|
| `static THost Create(GraphicsDevice device, int maximumPendingSubmissions)` | Registers the host on a device. |
| `ComputeSubmission <Pipeline>(...)` | Records and submits one invocation of the pipeline. Resource parameters declaring `Sharing.External` are replaced by `ComputeResourceBinding<T>`. |
| `bool TryEnsure<Slot>(in TPlan plan, out bool changed)` | Matches the owned resources to a plan. |
| `ComputeResourceBinding<T> Get<Slot>ComputeBinding()` | Returns the binding of the owned resource. |
| `static TSet Create(GraphicsDevice device, ComputeInteropDomain domain)` | Registers an interop resource set. |
| `bool TryEnsure<Slot>(int width, int height, out bool changed)` | Matches a shared texture to a size. |
| `bool TryGet<Slot>AllocatedSize(out int width, out int height)` | Reports the allocated size of the published shared texture as an unpinned snapshot. |
| `void Dispose()` / `void WaitForDisposal()` | Releases the registration and waits for it to complete. |

### Runtime

| Member | Description |
|---|---|
| `ComputeHostRuntime.Create(device, canonicalDescriptor, maximumPendingSubmissions, ownedSlots)` | Creates the host runtime. Called by generated code. |
| `ComputeHostRuntime.Submit<TInvocation>(in TInvocation invocation)` | Records and submits one invocation. |
| `ComputeHostRuntime.TryEnsureResource<TMaterializer>(...)` | Matches an owned slot to a plan. |
| `ComputeHostRuntime.GetBinding<TResource>(int slotOrdinal, int resourceIndex)` | Returns a resource binding. |
| `ComputeHostRuntime.Device` / `IsDisposeRequested` | Reports the device and the disposal state. |
| `ComputeInteropResourceSetRuntime.Create(device, domain, canonicalDescriptor, slots)` | Creates the resource set runtime. |
| `ComputeInteropResourceSetRuntime.Device` / `Domain` / `IsDisposeRequested` | Reports the device, the domain and the disposal state. |
| `ComputeSubmission.Completion` / `Status` / `IsCompleted` / `Wait()` | Tracks the completion of submitted work. |
| `IComputePipelineInvocation.Bind(ref ComputePipelineBinder)` / `Record(in ComputeContext)` | Implemented by generated invocation types. |

### Slots and bindings

| Member | Description |
|---|---|
| `ComputeResourceSlot<TResource>` | Owns a single resource and publishes generations of it. |
| `ComputeResourceGroupSlot<TGroup>` | Owns a group of resources published as one generation. |
| `SharedTextureSlot<T, TPixel, TView>` | Owns a texture shared with an external API. |
| `SharedTextureSlot.TryEnsure(int width, int height, out bool changed)` | Matches the texture to a size. |
| `SharedTextureSlot.TryGetAllocatedSize(out int width, out int height)` | Reports the allocated size of the published generation as an unpinned snapshot. |
| `SharedTextureSlot.GetComputeBinding()` | Returns the compute-side binding. |
| `SharedTextureSlot.BeginExternalOperation()` | Borrows the external view for one operation. |
| `SharedTextureSlot.AcquireExternalViewLease()` | Takes a lease on the external view. |
| `SharedTextureSlot.Width` / `Height` / `IsAllocated` | Reports the current logical dimensions and whether a generation is published. |
| `ComputeResourceBinding<TResource>` | A binding to a published resource generation. It carries the slot it was produced from. |
| `ComputePipelineBinder.TryPin(IGraphicsResource resource)` | Pins the generation of a borrowed resource. |
| `ComputePipelineBinder.TryPin<TResource>(in ComputeResourceBinding<TResource> binding, out TResource resource)` | Pins a resource shared with an external queue, revalidated under the slot the binding carries. |
| `ComputePipelineBinder.TryPin<TResource>(int slotOrdinal, in ComputeResourceBinding<TResource> binding)` | Pins a resource owned by a slot of the host. |
| `IComputeGenerationMaterializer.Materialize(ref ComputeGenerationContext)` | Implemented by generated materializers. |
| `IReadOnlyBuffer<T>` | A structured buffer a shader takes as read-only. |
| `ReadWriteBuffer<T>.AsReadOnly()` | Returns a read-only view binding the same resource through its SRV. |

### Interoperation

| Member | Description |
|---|---|
| `GraphicsDevice.RegisterExternalDomain<TView>(IComputeExternalInteropProvider<TView> provider)` | Registers an external API and returns its domain. |
| `ComputeInteropDomain.Device` / `Id` / `Capabilities` | Reports the device, the domain identifier and the negotiated capabilities. |
| `IComputeExternalInteropProvider.Initialize(in ExternalTimelineInitialization)` | Initialises the shared timeline. |
| `IComputeExternalInteropProvider.EnqueueSignal(ulong)` / `EnqueueWait(ulong)` / `FlushAfterSignal()` | Drives the shared fence on the external queue. |
| `IComputeExternalInteropProvider.OpenSharedTexture(BorrowedSharedHandle, in ExternalTextureDescriptor)` | Opens a shared texture as the external view type. |
| `IComputeExternalInteropProvider.OnDeviceTerminal(Exception)` | Reports that the device entered a terminal state. |
| `ComputeExternalQueueScheduler` | Base class for providers needing a scope around each queue operation. |
| `ExternalTextureLease<TView>.Width` / `Height` | Reports the allocated dimensions of the generation held by the lease. |
| `ExternalTextureLease<TView>.DangerousGetView()` / `BeginExternalQueueOperation()` | Uses the leased external view. |
| `ExternalTextureDescriptor` | `Width`, `Height`, `Format`, `ExternalUsage`, `AlphaMode`. |
| `ExternalAdapterIdentity(long adapterLuid)` / `ExternalDomainId` | Identifies the adapter and the domain. |
| `InteropServices.AcquireNativeResource(resource, out NativeResourceSynchronization, NativeResourceAcquisition)` | Holds the resource generation of a buffer, a texture or a transfer resource while an external object uses it. |
| `NativeResourceReference.QueryInterface(Guid*, void**)` / `TryQueryInterface(Guid*, void**)` / `IsValid` / `Dispose()` | Uses and releases a native reference. Must be disposed. |
| `NativeResourceSynchronization.LastWrite` / `LastComputeRead` / `LastCopyRead` | Reports the completion points of the work already submitted for the generation. |
| `InteropServices.GetID3D12Fence(GraphicsDevice, ComputeQueueKind, Guid*, void**)` | Gets the fence of a queue, so that external work can wait on those completion points. |
| `InteropServices.AcquireNativeDevice(GraphicsDevice)` | Holds the device while an external object uses its native object. |
| `NativeDeviceReference.QueryInterface(Guid*, void**)` / `TryQueryInterface(Guid*, void**)` / `IsValid` / `Dispose()` | Uses and releases a device reference. Must be disposed. |

### Shared resources

| Member | Description |
|---|---|
| `InteropServices.AllocateSharedReadWriteTexture2D<T>(device, width, height)` | Allocates a shareable read-write texture. |
| `InteropServices.AllocateSharedReadWriteTexture2D<T, TPixel>(device, width, height)` | Allocates a shareable normalized read-write texture. |
| `InteropServices.AllocateSharedReadOnlyTexture2D<T>(device, width, height)` | Allocates a shareable read-only texture. |
| `InteropServices.OpenSharedReadWriteTexture2D<T>(device, handle)` | Opens a shared texture from a handle. |
| `InteropServices.OpenSharedReadWriteTexture2D<T, TPixel>(device, handle)` | Opens a shared normalized texture from a handle. |
| `InteropServices.OpenSharedReadOnlyTexture2D<T>(device, handle)` | Opens a shared read-only texture from a handle. |
| `InteropServices.CreateSharedHandle<T>(Texture2D<T> texture)` | Creates a shared handle for a texture. |
| `InteropServices.CreateSharedFence(device, riid, ppvFence, sharedHandle)` | Creates a shared fence and its handle. |
| `InteropServices.OpenSharedFence(device, handle, riid, ppvFence)` | Opens a shared fence from a handle. |
| `InteropServices.SignalSharedFence(device, d3D12Fence, value)` | Signals a shared fence on the compute queue. |
| `InteropServices.WaitForSharedFence(device, d3D12Fence, value)` | Waits on a shared fence on the compute queue. |

### Memory

| Member | Description |
|---|---|
| `GraphicsDevice.SetMemoryPolicy(in GraphicsMemoryPolicy policy)` | Installs the budget policy. |
| `GraphicsDevice.GetMemoryStatistics()` | Returns a snapshot of the memory state. |
| `GraphicsDevice.TrimMemory()` | Releases retired and idle memory. |
| `GraphicsMemoryPolicy` | `BudgetBroker`, `LocalOwnedHardLimitBytes`, `NonLocalOwnedHardLimitBytes`. |
| `GraphicsMemoryStatistics` | `Epoch`, `Local`, `NonLocal`, `ActiveGenerationCount`, `RetiredGenerationCount`, `ManagedPoolSurplusCount`, `NativeReferencedGenerationCount`. |
| `IGraphicsMemoryBudgetBroker.RegisterClient(in GraphicsMemoryClientDescriptor)` | Registers a budget client. |
| `IGraphicsMemoryBudgetClient.TryGetGrant(GraphicsMemorySegment, out GraphicsMemoryGrant)` | Requests a grant for a segment. |
| `GraphicsMemoryAllocationException` | Thrown when the budget refuses an allocation. |

### Enumerations

| Type | Members |
|---|---|
| `ComputeResourceAccess` | `Read`, `Write`, `ReadWrite` |
| `ComputeResourceResizePolicy` | `Exact`, `GrowOnly` |
| `ComputeResourceRecovery` | `Discardable`, `RecreateFromHost`, `Recompute`, `CapacityOnly` |
| `ComputeResourceSharing` / `ComputeResourceAliasing` | Options of `[ComputeResource]` |
| `ComputeSharedTextureInitialOwner` | `Compute`, `External` |
| `ExternalResourceAccess` | `Read`, `Write`, `ReadWrite` |
| `ExternalTextureUsage` | `Sampled`, `RenderTarget` |
| `ComputeAlphaMode` | `Ignore`, `Premultiplied`, `Straight` |
| `ComputeQueueKind` | `None`, `Compute`, `Copy` |
| `ComputeSubmissionStatus` | `Succeeded`, `Pending`, `Faulted` |
| `ExternalTextureFormat` | `Bgra8Unorm` |
| `ExternalInteropCapabilities` | `None`, `SharedFence`, `SharedTexture2D`, `SingleImmediateContextOrdering`, `PersistentExternalViewOrdering` |
| `GraphicsMemorySegment` | `Local`, `NonLocal` |
| `MemoryBudgetStatus` | `Unknown`, `Valid`, `Unsupported`, `DeviceLost` |

---

## Limitations

- Windows only. The library uses Direct3D 12 and does not run on other operating systems.
- `ExternalTextureFormat` currently declares a single member, `Bgra8Unorm`. Shared textures are limited to that format.
- `ExternalTextureUsage` declares `Sampled` and `RenderTarget` only.
- The body of a compute shader is limited to the C# constructs the generator can translate to HLSL. Anything outside that range is reported as a diagnostic at compile time.
- `ComputeWeave.Dxc` bundles `dxcompiler.dll` and `dxil.dll` and therefore runs only in x64 and Arm64 processes.

---

## Notes

- The canonical descriptor is the contract between the generator and the runtime. Both sides ship in the same version and a descriptor written by one version is not intended to be read by another.
- A submission is not awaited implicitly. Call `ComputeSubmission.Wait()` when the result is needed.
- `Dispose` requests the release of a registration; `WaitForDisposal` blocks until it has completed. Work still in flight keeps the generation it captured alive.
- `GraphicsDevice.GetDefault()` caches the device for the process and returns the same instance until it is disposed.
- The `DeviceLost` event on `GraphicsDevice` is raised at most once per instance. After the device is lost, the public APIs throw `InvalidOperationException`.
- The `AppContext` switches are named `COMPUTEWEAVE_ENABLE_DEBUG_OUTPUT`, `COMPUTEWEAVE_ENABLE_DEVICE_REMOVED_EXTENDED_DATA` and `COMPUTEWEAVE_ENABLE_GPU_TIMEOUT` since 1.2.0. The `COMPUTESHARP_` names they had before are still honoured when the new ones are not set, so an application that set them keeps its behaviour. Setting them through the `ComputeWeaveEnableDebugOutput`, `ComputeWeaveEnableDeviceRemovedExtendedData` and `ComputeWeaveEnableGpuTimeout` MSBuild properties is unaffected.

---

## Disclaimer

This library is published under the MIT license.

The software is provided "as is", without warranty of any kind, express or implied, including but not limited to the warranties of merchantability, fitness for a particular purpose and noninfringement.

The authors accept no liability for any damage arising from the use of or the inability to use this library.

---

## Third-Party Licenses

The full text of each license is included under [`.github/LICENSE`](.github/LICENSE) in the repository and under `THIRD-PARTY-NOTICES` in the NuGet packages.

| Software | Use | License | Copyright |
|---|---|---|---|
| [ComputeSharp](https://github.com/Sergio0694/ComputeSharp) | The project this library is derived from | [MIT License](.github/LICENSE/ComputeSharp.txt) | Copyright (c) 2024 Sergio Pedri |
| [DirectX Shader Compiler](https://github.com/microsoft/DirectXShaderCompiler) | HLSL compilation. `dxcompiler.dll` and `dxil.dll` are bundled | [University of Illinois/NCSA Open Source License](.github/LICENSE/DirectXShaderCompiler.txt) ([third-party notices](.github/LICENSE/DirectXShaderCompiler.ThirdPartyNotices.txt)) | Copyright (c) 2003-2015 University of Illinois at Urbana-Champaign |

This repository is an independently maintained derivative of [Sergio0694/ComputeSharp](https://github.com/Sergio0694/ComputeSharp) and is not affiliated with the original author. ComputeSharp itself was originally based in part on code from [DX12GameEngine](https://github.com/Aminator/DirectX12GameEngine).

---

## License

[MIT License](LICENSE)
