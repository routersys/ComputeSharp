# ComputeWeave.D3D12MemoryAllocator

English | [日本語](https://github.com/routersys/ComputeWeave/blob/main/src/ComputeWeave.D3D12MemoryAllocator/README.ja.md)

An extension package for [ComputeWeave](https://www.nuget.org/packages/ComputeWeave). It makes graphics resources be allocated through [D3D12MA](https://gpuopen.com/d3d12-memory-allocator/).

Configure the allocator at startup with `AllocationServices`.

```csharp
AllocationServices.ConfigureAllocatorFactory(new D3D12MemoryAllocatorFactory());
```

Every subsequent allocation then goes through D3D12MA.

This is independent of the GPU memory budget the fork adds. `GraphicsDevice.SetMemoryPolicy` and `GraphicsDevice.GetMemoryStatistics` are in the [ComputeWeave](https://www.nuget.org/packages/ComputeWeave) package and apply whether or not this package is referenced.

## More

The complete API reference is in the [repository](https://github.com/routersys/ComputeWeave).
