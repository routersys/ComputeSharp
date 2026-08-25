# ComputeWeave.D2D1

[English](https://github.com/routersys/ComputeWeave/blob/main/src/ComputeWeave.D2D1/README.md) | 日本語

[ComputeWeave](https://www.nuget.org/packages/ComputeWeave) と対になるパッケージです。Direct2D のピクセルシェーダーを C# だけで書き、`ID2D1Effect` として登録します。

[ComputeWeave](https://www.nuget.org/packages/ComputeWeave) パッケージの拡張ではなく、参照もしていません。両者が共有するのは [ComputeWeave.Core](https://www.nuget.org/packages/ComputeWeave.Core) の基盤、すなわち基本型と `Hlsl` 組み込みです。ここで書いたシェーダーは Direct3D 12 の計算キューではなく Direct2D が実行するため、本パッケージは `GraphicsDevice` を生成も使用もせず、このフォークが追加した宣言的な層も適用されません。

ピクセルシェーダーは `ID2D1PixelShader` を実装した `partial struct` です。

```csharp
[D2DInputCount(1)]
[D2DInputSimple(0)]
[D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
[D2DGeneratedPixelShaderDescriptor]
public readonly partial struct DifferenceEffect(float amount) : ID2D1PixelShader
{
    public float4 Execute()
    {
        float4 color = D2D.GetInput(0);
        float3 rgb = Hlsl.Saturate(this.amount - color.RGB);

        return new(rgb, 1);
    }
}
```

バイトコードと定数バッファは、デバイスを用意せずに取得できます。

```csharp
ReadOnlyMemory<byte> bytecode = D2D1PixelShader.LoadBytecode<DifferenceEffect>();
ReadOnlyMemory<byte> buffer = D2D1PixelShader.GetConstantBuffer(new DifferenceEffect(1));
```

`D2D1PixelShaderEffect` はシェーダーを Direct2D の効果として登録し、そこから `ID2D1Effect` を生成します。必要なら独自の描画変換も指定できます。`D2D1ReflectionServices` は生成された HLSL のソースとシェーダーの統計値を報告します。

シェーダーは Direct2D が受け付ける DXBC へ、FXC でコンパイルされます。`d3dcompiler_47.dll` は Windows に同梱されているため、本パッケージはコンパイラーを同梱しません。

## 詳細

APIの一覧は[リポジトリ](https://github.com/routersys/ComputeWeave)にあります。
