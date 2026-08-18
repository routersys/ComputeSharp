# ComputeWeave

English | [日本語](https://github.com/routersys/ComputeWeave/blob/main/src/ComputeWeave/README.ja.md)

ComputeWeave is a fork of [ComputeSharp](https://github.com/Sergio0694/ComputeSharp), the library that lets DirectX 12 compute shaders be written entirely in C#. That base is unchanged: a shader is a `partial struct` implementing `IComputeShader`, `GraphicsDevice.GetDefault()` returns the device, and `For` dispatches.

This package also contains everything the fork adds, which is a declarative layer. A compute pipeline and its resources are declared with attributes, a source generator turns the declaration into a canonical binary descriptor embedded in the assembly, and the runtime reads that descriptor to bind resources, record command lists and track completion. The same layer carries shared textures and shared fences across the Direct3D 11 and Direct3D 12 boundary, and adds a GPU memory budget.

## Declarative compute pipelines

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

The generator emits into the same partial type a static `Create` factory, `Dispose`, `WaitForDisposal`, and for each pipeline an overload of the same name that takes the declared arguments without the context and returns `ComputeSubmission`.

```csharp
using Host host = Host.Create(GraphicsDevice.GetDefault(), maximumPendingSubmissions: 4);

ComputeSubmission submission = host.Run();

submission.Wait();
```

Waiting is explicit; a submission is not awaited implicitly at disposal.

## Owned resource slots

A resource owned by a host is declared as a field of `ComputeResourceSlot<TResource>` or `ComputeResourceGroupSlot<TGroup>`. The generator emits `TryEnsure<Slot>(in <Plan> plan, out bool changed)` and, for single-resource slots, `Get<Slot>ComputeBinding()`.

Resources are not held directly; they live in slots that publish generations. A new generation is published only when the requested plan actually changes, and work in flight keeps the generation it captured alive, so resizing a resource does not invalidate submissions already recorded. `ComputeResourceRecovery` selects what happens to the contents when a generation is replaced: `Discardable`, `RecreateFromHost`, `Recompute` or `CapacityOnly`.

## Direct3D 11 interoperation

You do not have to write a provider to drive a Direct3D 11 immediate context. `ComputeExternalDirect3D11Provider` opens the shared fence, enqueues the signals, the waits and the flush, opens the shared textures and creates the external views. It takes the device, the immediate context and the render target as raw COM pointers, so it stays independent of the Direct3D 11 bindings you use.

```csharp
using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();
ComputeExternalDirect3D11Provider provider = new(device, immediateContext, renderTarget, scheduler);
using ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(provider);
```

`ComputeExternalQueueScheduler.Create()` returns a scheduler serializing the reservations of one immediate context. Providers enqueueing onto the same context share one instance, and you keep that mapping.

On the `ExternalDirect3D11TextureView` it creates, `Texture` and `Bitmap` are borrowed and must not be released. Use `AddRefTexture()` and `AddRefBitmap()` to hand a pointer to your own bindings: they return a reference you own, so the binding can take ownership of it.

A host whose external side is a Direct3D 12 command queue of its own device uses `ComputeExternalDirect3D12Provider` the same way: it opens the shared fence and the shared textures on its device, signals and waits on the queue, and its views expose the opened resource through `Resource` and `AddRefResource()`. `GraphicsDevice.TryGetDevice` resolves the graphics device running on the adapter of either provider from its `ExternalAdapterIdentity`.

Any other API is connected by implementing `IComputeExternalInteropProvider<TView>` and registering it with `GraphicsDevice.RegisterExternalDomain`. The provider is asked to initialise a shared timeline, to enqueue signals and waits on its own queue, and to open a shared texture as its own view type.

Shared textures are declared in a `partial` type marked `[ComputeInteropResourceSet]`, as `SharedTextureSlot<T, TPixel, TView>` fields annotated with `[ComputeSharedTexture]`.

```csharp
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

`TryGet<Slot>AllocatedSize` delegates to `SharedTextureSlot.TryGetAllocatedSize` and reports the allocated width and height of the published texture, which can remain larger than the logical dimensions under `GrowOnly`. The result is an unpinned snapshot and does not describe a binding, borrow or lease acquired separately when generation replacement can run concurrently. The `Width` and `Height` of an `ExternalTextureLease<TView>` describe the allocated dimensions of the generation held by that lease. Ownership is handed over through the shared fence: `BeginExternalOperation` borrows the view for the external API, `AcquireExternalViewLease` takes a lease that outlives a single operation, and `GetComputeBinding` returns the compute-side binding.

Retiring a shared texture generation drains the external queue before the external view is released, and that drain runs on the device rather than on the calling thread, so the retired generation is still held when `TryEnsure` or `Dispose` returns. A foreground operation waits when that internal maintenance operation temporarily holds the domain, while another foreground operation remains a conflicting use and is rejected. A provider that throws poisons its domain, and every later operation on that domain reports the failure. Rejections carry an identifier: `ComputeDiagnosticException` derives from `InvalidOperationException` and reports a stable `DiagnosticId` such as `CMPW3004`. Whether to retry, rebuild the resource or tear the domain down differs per identifier, so do not tell rejections apart by their message.

`InteropServices` additionally exposes the shared texture and shared fence primitives directly, for callers that manage the handles themselves.

## GPU memory budget

`GraphicsDevice.SetMemoryPolicy` installs hard limits per memory segment and, optionally, an `IGraphicsMemoryBudgetBroker` that arbitrates between clients. `GraphicsDevice.GetMemoryStatistics` returns a snapshot and `GraphicsDevice.TrimMemory` releases what is retired and idle. A generation is idle only once the work and the external queue that held it are done with it, so trimming right after the call that retired it reclaims nothing. Allocation failures caused by the budget surface as `GraphicsMemoryAllocationException`. The budget covers the resources the device creates itself; a device using an allocator configured through `AllocationServices.ConfigureAllocatorFactory` creates its resources through that allocator, and those are neither admitted against the policy nor counted in the statistics. A configured allocator and the declarative layer are mutually exclusive: generations, trimming and the budget all rest on the device owning its allocations, so the `Create` factories throw `NotSupportedException` on such a device. The base library and `InteropServices` are unaffected.

## Compile-time validation

The declarations above are checked by analyzers that report 95 diagnostics, `CMPW0001` through `CMPW0111`, covering attribute placement, host and pipeline method shape, slot declaration, resource contracts and generated overload conflicts. Some carry a code fix. Runtime rejections use the same `CMPW` prefix, told apart by their number band.

## More

The complete API reference is in the [repository](https://github.com/routersys/ComputeWeave).
