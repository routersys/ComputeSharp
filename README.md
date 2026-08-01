# ComputeWeave

ComputeWeave は、DirectX 12 の計算シェーダーを C# だけで記述して GPU 上で実行するための .NET ライブラリです。GPU デバイスの取得、バッファとテクスチャの確保、メインメモリとの転送、シェーダー本体の記述までを C# で完結でき、HLSL はソースジェネレーターが生成します。

対応環境は Windows と .NET 10 です。DirectX 12 に対応した GPU が無い環境では [WARP デバイス](https://learn.microsoft.com/windows/win32/direct3darticles/directx-warp)へ自動的に切り替わるため、フォールバック経路を自分で書く必要はありません。

## パッケージ

| 名前 | 説明 |
| --- | --- |
| ComputeWeave | 本体。計算シェーダーの記述と実行を提供する |
| ComputeWeave.Core | 依存パッケージが共有する基本型と内部基盤。直接参照する必要は無い |
| ComputeWeave.Dxc | DXC コンパイラーを同梱し、シェーダーのリフレクションを可能にする拡張 |
| ComputeWeave.D3D12MemoryAllocator | グラフィックスリソースの確保に D3D12MA を用いる拡張 |

## 使い方

バッファの全要素を 2 倍にするシェーダーを例にします。まず GPU バッファを確保し、データを転送します。

```csharp
int[] array = [.. Enumerable.Range(1, 100)];

using ReadWriteBuffer<int> buffer = GraphicsDevice.GetDefault().AllocateReadWriteBuffer(array);
```

次にシェーダーを定義します。`IComputeShader` を実装する `partial struct` として宣言し、`[ThreadGroupSize]` でディスパッチ構成を、`[GeneratedComputeShaderDescriptor]` でコード生成の対象であることを指定します。`partial` はコード生成に必要なので省略できません。フィールドはそのまま GPU へ渡す値になります。

```csharp
[ThreadGroupSize(DefaultThreadGroupSizes.X)]
[GeneratedComputeShaderDescriptor]
public readonly partial struct MultiplyByTwo(ReadWriteBuffer<int> buffer) : IComputeShader
{
    public void Execute()
    {
        buffer[ThreadIds.X] *= 2;
    }
}
```

`ThreadIds` はシェーダー本体からディスパッチ情報を参照するための特別な型で、ここでは `for` 文の添字に相当する値を返します。

最後にシェーダーを実行し、結果を読み戻します。

```csharp
GraphicsDevice.GetDefault().For(buffer.Length, new MultiplyByTwo(buffer));

buffer.CopyTo(array);
```

## ビルド

```bash
dotnet build ComputeWeave.sln -c Release -p:Platform=x64
```

テストは次のとおりです。`ComputeWeave.Tests` は GPU を使うため実行に数分かかります。

```bash
dotnet test tests/ComputeWeave.Tests.SourceGenerators/ComputeWeave.Tests.SourceGenerators.csproj -c Release -p:Platform=x64
```

## ライセンスと由来

MIT ライセンスで配布します。詳細は [LICENSE](LICENSE) を参照してください。同梱する第三者コンポーネントの表記は [ThirdPartyNotices.txt](ThirdPartyNotices.txt) にあります。

本リポジトリは [Sergio0694/ComputeSharp](https://github.com/Sergio0694/ComputeSharp) から派生した独立の実装であり、原作者との関係はありません。原作の ComputeSharp は [DX12GameEngine](https://github.com/Aminator/DirectX12GameEngine) のコードを一部の基礎としています。
