# ComputeWeave.D3D12MemoryAllocator

English | [日本語](https://github.com/routersys/ComputeWeave/blob/main/src/ComputeWeave.D3D12MemoryAllocator/README.ja.md)

An extension package for [ComputeWeave](https://www.nuget.org/packages/ComputeWeave). It makes graphics resources be allocated through [D3D12MA](https://gpuopen.com/d3d12-memory-allocator/).

Configure the allocator at startup with `AllocationServices`.

```csharp
AllocationServices.ConfigureAllocatorFactory(new D3D12MemoryAllocatorFactory());
```

Every subsequent allocation then goes through D3D12MA.

`GraphicsDevice.SetMemoryPolicy` and `GraphicsDevice.GetMemoryStatistics` are in the [ComputeWeave](https://www.nuget.org/packages/ComputeWeave) package and apply whether or not this package is referenced.

Configuring the allocator changes that. Once an external allocator owns the allocations, the device no longer reserves or accounts for them: the limits of `SetMemoryPolicy` are not enforced, the owned bytes are not counted, and `TrimMemory` reclaims nothing. The declarative layer the fork adds cannot be used at all on such a device, and `Create` throws a `NotSupportedException`. The base library and `InteropServices` are unaffected.

## More

The complete API reference is in the [repository](https://github.com/routersys/ComputeWeave).
