# ComputeWeave.Dxc

ComputeWeave の拡張パッケージです。DXC コンパイラーを同梱し、シェーダーのリフレクションを可能にします。

生成された HLSL のソースや、DirectX 12 のリフレクション API が公開する統計値を参照したい場合に、`ReflectionServices` 型からシェーダー型ごとの情報をまとめて取得できます。

## 使い方

`MyShader` 型が定義済みであるとして、次のように調べます。

```csharp
ShaderInfo shaderInfo = ReflectionServices.GetShaderInfo<MyShader>();

string hlslSource = shaderInfo.HlslSource;
uint numberOfResources = shaderInfo.BoundResourceCount;
uint instructionCount = shaderInfo.InstructionCount;
```

## 詳細

その他の機能は [GitHub リポジトリ](https://github.com/routersys/ComputeWeave)を参照してください。
