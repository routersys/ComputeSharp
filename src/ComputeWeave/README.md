# ComputeWeave

DirectX 12 の計算シェーダーを C# だけで記述して GPU 上で実行するための .NET ライブラリです。GPU デバイスの取得、バッファとテクスチャの確保、メインメモリとの転送、シェーダー本体の記述までを C# で完結でき、HLSL はソースジェネレーターが生成します。

公開 API の入口は `GraphicsDevice` 型です。`GraphicsDevice.GetDefault()` が現在の環境の主 GPU デバイスを返します。DirectX 12 に対応した GPU が無い環境では [WARP デバイス](https://learn.microsoft.com/windows/win32/direct3darticles/directx-warp)へ自動的に切り替わり、シェーダーは CPU 上のエミュレーションで動作します。フォールバック経路を自分で書く必要はありません。

## 使い方

バッファの全要素を 2 倍にするシェーダーを例にします。まず GPU バッファを確保し、データを転送します。

```csharp
int[] array = [.. Enumerable.Range(1, 100)];

using ReadWriteBuffer<int> buffer = GraphicsDevice.GetDefault().AllocateReadWriteBuffer(array);
```

`AllocateReadWriteBuffer` は入力配列と同じ長さの `ReadWriteBuffer<T>` を確保し、内容を転送します。要素型や長さの異なるオーバーロードも用意しています。

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

この例ではプライマリコンストラクターを使っていますが、フィールドを明示して通常のコンストラクターで設定しても構いません。`ThreadIds` はシェーダー本体からディスパッチ情報を参照するための特別な型で、ここでは `for` 文の添字に相当する値を返します。

最後にシェーダーを実行し、結果を読み戻します。

```csharp
GraphicsDevice.GetDefault().For(buffer.Length, new MultiplyByTwo(buffer));

buffer.CopyTo(array);
```

## 詳細

その他の機能は [GitHub リポジトリ](https://github.com/routersys/ComputeWeave)を参照してください。
