# ComputeWeave.D3D12MemoryAllocator

ComputeWeave の拡張パッケージです。グラフィックスリソースの確保に [D3D12MA](https://gpuopen.com/d3d12-memory-allocator/) を用いるようにします。

## 使い方

起動時に `AllocationServices` 型でアロケーターを設定します。

```csharp
AllocationServices.ConfigureAllocatorFactory(new D3D12MemoryAllocatorFactory());
```

これ以降のリソース確保が D3D12MA を経由するようになります。

## 詳細

その他の機能は [GitHub リポジトリ](https://github.com/routersys/ComputeWeave)を参照してください。
