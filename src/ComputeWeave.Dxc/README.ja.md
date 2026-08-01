# ComputeWeave.Dxc

[English](https://github.com/routersys/ComputeWeave/blob/main/src/ComputeWeave.Dxc/README.md) | 日本語

[ComputeWeave](https://www.nuget.org/packages/ComputeWeave) の拡張パッケージです。DXC コンパイラーを同梱し、シェーダーのリフレクションを可能にします。

生成された HLSL のソースや、Direct3D 12 のリフレクション API が公開する統計値が必要な場合に、`ReflectionServices` がシェーダー型ごとの情報をまとめて取得します。

```csharp
ShaderInfo shaderInfo = ReflectionServices.GetShaderInfo<MyShader>();

string hlslSource = shaderInfo.HlslSource;
uint numberOfResources = shaderInfo.BoundResourceCount;
uint instructionCount = shaderInfo.InstructionCount;
```

本パッケージは `dxcompiler.dll` と `dxil.dll` を同梱するため、x64 と Arm64 以外のプロセスでは動作しません。

このフォークが追加した宣言的な層は [ComputeWeave](https://www.nuget.org/packages/ComputeWeave) パッケージにあり、本パッケージはそこへ何も加えません。

## 詳細

APIの一覧は[リポジトリ](https://github.com/routersys/ComputeWeave)にあります。
