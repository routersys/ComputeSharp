# ComputeWeave.D3D12MemoryAllocator

[English](https://github.com/routersys/ComputeWeave/blob/main/src/ComputeWeave.D3D12MemoryAllocator/README.md) | 日本語

[ComputeWeave](https://www.nuget.org/packages/ComputeWeave) の拡張パッケージです。グラフィックスリソースの確保を [D3D12MA](https://gpuopen.com/d3d12-memory-allocator/) 経由にします。

起動時に `AllocationServices` でアロケーターを設定します。

```csharp
AllocationServices.ConfigureAllocatorFactory(new D3D12MemoryAllocatorFactory());
```

これ以降の確保はすべて D3D12MA を経由します。

これはフォークが追加したGPUメモリの予算管理とは独立しています。`GraphicsDevice.SetMemoryPolicy` と `GraphicsDevice.GetMemoryStatistics` は [ComputeWeave](https://www.nuget.org/packages/ComputeWeave) パッケージにあり、本パッケージを参照するかどうかに関わらず機能します。

## 詳細

APIの一覧は[リポジトリ](https://github.com/routersys/ComputeWeave)にあります。
