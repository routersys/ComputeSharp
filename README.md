# ComputeWeave

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](#)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](#)
[![Release](https://img.shields.io/github/v/release/routersys/ComputeWeave.svg)](https://github.com/routersys/ComputeWeave/releases)

---

DirectX 12の計算シェーダーをC#だけで記述し、GPU上で実行するための.NETライブラリです。
シェーダー本体をC#のメソッドとして書くと、ソースジェネレーターがHLSLへ変換してコンパイル済みのバイトコードを埋め込みます。HLSLのファイルを別に用意する必要はありません。
GPUデバイスの取得、バッファとテクスチャの確保、メインメモリとの転送、コマンドの投入までを型付きのAPIで扱えます。
Direct3D 11で動く既存のアプリケーションとは、共有テクスチャと共有フェンスを介して接続できます。

---

## 目次

1. [概要](#概要)
2. [動作要件](#動作要件)
3. [インストール方法](#インストール方法)
4. [主な機能](#主な機能)
   - [1. C#による計算シェーダーの記述](#1-cによる計算シェーダーの記述)
   - [2. GPUリソースの確保と転送](#2-gpuリソースの確保と転送)
   - [3. コマンドの一括投入](#3-コマンドの一括投入)
   - [4. Direct3D 11・12相互運用](#4-direct3d-1112相互運用)
   - [5. シェーダーのリフレクション](#5-シェーダーのリフレクション)
   - [6. アナライザーによる静的検証](#6-アナライザーによる静的検証)
5. [パッケージ一覧](#パッケージ一覧)
6. [制限事項](#制限事項)
7. [注意事項](#注意事項)
8. [免責事項](#免責事項)
9. [サードパーティライセンス](#サードパーティライセンス)
10. [ライセンス](#ライセンス)

---

## 概要

公開APIの入口は`GraphicsDevice`型です。`GraphicsDevice.GetDefault()`が現在の環境の既定のデバイスを返します。このメソッドは、機能レベル`D3D_FEATURE_LEVEL_11_0`とシェーダーモデル`D3D_SHADER_MODEL_6_0`を満たすアダプターを探し、見つからない場合はWARPデバイスへ切り替えます。どちらも取得できない場合だけ`NotSupportedException`を送出します。対応GPUが無い環境のためのフォールバック経路を自分で書く必要はありません。

シェーダーは`IComputeShader`を実装する`partial struct`として宣言します。ソースジェネレーターが型のフィールドから定数バッファとリソースの束縛を組み立て、`Execute`メソッドの本体をHLSLへ変換し、DXCでコンパイルしたバイトコードを生成コードへ埋め込みます。HLSLへ変換できない構文はアナライザーがコンパイル時に検出します。

計算シェーダーの実行に加えて、Direct3D 11で動く既存のアプリケーションと接続するための相互運用の仕組みを備えます。共有テクスチャと共有フェンスをC#の属性から宣言し、両APIの間で所有権を受け渡します。

---

## 動作要件

| 項目 | 要件 |
|---|---|
| OS | Windows 10 以降（64bit） |
| ランタイム | .NET 10.0 |
| GPU | 機能レベル `D3D_FEATURE_LEVEL_11_0` とシェーダーモデル `D3D_SHADER_MODEL_6_0` に対応したDirect3D 12デバイス |
| 代替 | 上記を満たすGPUが無い場合はWARPデバイスで動作します |

---

## インストール方法

NuGetパッケージとして参照します。

```bash
dotnet add package ComputeWeave
```

シェーダーのリフレクションやD3D12MAを使う場合は、対応する拡張パッケージを追加します。

```bash
dotnet add package ComputeWeave.Dxc
dotnet add package ComputeWeave.D3D12MemoryAllocator
```

`ComputeWeave.Core`は他のパッケージの推移的な依存先です。直接参照する必要はありません。

---

## 主な機能

### 1. C#による計算シェーダーの記述

シェーダーは`IComputeShader`を実装する`partial struct`として宣言します。`partial`はコード生成に必要なので省略できません。`[ThreadGroupSize]`でスレッドグループの構成を、`[GeneratedComputeShaderDescriptor]`でコード生成の対象であることを指定します。フィールドはそのままGPUへ渡す値になります。

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

`ThreadIds`はシェーダー本体からディスパッチ情報を参照するための特別な型で、ここでは`for`文の添字に相当する値を返します。実行は`GraphicsDevice`の`For`メソッドで行います。

```csharp
GraphicsDevice.GetDefault().For(buffer.Length, new MultiplyByTwo(buffer));
```

`For`は1次元から3次元までのオーバーロードがあります。

その他に、GPUのグループ共有メモリを使う`[GroupShared]`、書き込みの可視性を全体へ広げる`[GloballyCoherent]`、倍精度の対応を要求する`[RequiresDoublePrecisionSupport]`、コンパイルオプションを指定する`[CompileOptions]`を利用できます。

### 2. GPUリソースの確保と転送

バッファとテクスチャを型付きで確保します。用途に応じて次の型を使い分けます。

| 分類 | 型 |
|---|---|
| バッファ | `ReadOnlyBuffer<T>` / `ReadWriteBuffer<T>` / `ConstantBuffer<T>` |
| テクスチャ | `ReadOnlyTexture1D` / `2D` / `3D`、`ReadWriteTexture1D` / `2D` / `3D` |
| 転送用バッファ | `UploadBuffer<T>` / `ReadBackBuffer<T>` |
| 転送用テクスチャ | `UploadTexture1D` / `2D` / `3D`、`ReadBackTexture1D` / `2D` / `3D` |

確保と転送は`GraphicsDevice`の拡張メソッドで行います。

```csharp
int[] array = [.. Enumerable.Range(1, 100)];

using ReadWriteBuffer<int> buffer = GraphicsDevice.GetDefault().AllocateReadWriteBuffer(array);

buffer.CopyTo(array);
```

`AllocateReadWriteBuffer`は入力配列と同じ長さのバッファを確保して内容を転送します。要素型や長さの異なるオーバーロードも用意しています。

### 3. コマンドの一括投入

`ComputeContext`は、複数のディスパッチと転送を1つのコマンドリストへまとめて投入するための型です。`IDisposable`と`IAsyncDisposable`の両方を実装しており、破棄の時点でコマンドを投入します。個別に投入する場合と比べて、コマンドリストの生成と同期の回数を減らせます。

### 4. Direct3D 11・12相互運用

Direct3D 11で動く既存のアプリケーションとGPUリソースを共有するための仕組みです。`[ComputePipelineHost]`を付けた型にパイプラインの束をまとめ、個々の処理を`[ComputePipeline]`を付けたメソッドとして宣言します。共有するテクスチャは`[ComputeInteropResourceSet]`を付けた型の中で、`[ComputeSharedTexture]`を付けた`SharedTextureSlot<...>`のフィールドとして宣言します。

宣言の内容はソースジェネレーターが記述子へ変換してアセンブリへ埋め込み、実行時に`ComputeHostRuntime`と`ComputeInteropResourceSetRuntime`が読み取ります。共有テクスチャの所有権はフェンスで受け渡すため、通常の処理ではCPUへの読み戻しが発生しません。

### 5. シェーダーのリフレクション

`ComputeWeave.Dxc`パッケージを追加すると、生成されたHLSLのソースやDirect3D 12のリフレクションAPIが公開する統計値を参照できます。

```csharp
ShaderInfo shaderInfo = ReflectionServices.GetShaderInfo<MyShader>();

string hlslSource = shaderInfo.HlslSource;
uint numberOfResources = shaderInfo.BoundResourceCount;
uint instructionCount = shaderInfo.InstructionCount;
```

### 6. アナライザーによる静的検証

HLSLへ変換できない構文、属性の指定漏れ、リソースの誤った扱いを、コンパイル時に`CMPW`で始まる診断として報告します。診断は92種類あり、一部にはコード修正の提案が付きます。GPU上でしか現れない不具合の多くを、ビルドの時点で検出できます。

---

## パッケージ一覧

| 名前 | 説明 |
|---|---|
| ComputeWeave | 本体。計算シェーダーの記述と実行、相互運用を提供します。 |
| ComputeWeave.Core | 各パッケージが共有する基本型と内部基盤です。直接参照する必要はありません。 |
| ComputeWeave.Dxc | DXCコンパイラーを同梱し、シェーダーのリフレクションを可能にします。 |
| ComputeWeave.D3D12MemoryAllocator | グラフィックスリソースの確保にD3D12MAを用いるようにします。 |

---

## 制限事項

- Windows専用です。Direct3D 12を利用するため、他のOSでは動作しません。
- シェーダーの本体に書けるC#の構文は、HLSLへ変換できる範囲に限られます。範囲外の構文はコンパイル時に診断として報告します。
- `ComputeWeave.Dxc`は`dxcompiler.dll`と`dxil.dll`を同梱するため、x64とARM64以外のプロセスアーキテクチャでは動作しません。
- 相互運用の共有テクスチャは、Direct3D 11とDirect3D 12の両方で共有ハンドルを作成できるGPUを必要とします。

---

## 注意事項

- 既定のデバイスはプロセス内でキャッシュされます。`GraphicsDevice.GetDefault()`は同じインスタンスを返します。
- GPUリソースは`IDisposable`です。`using`で確実に破棄してください。破棄しない場合、GPUメモリが解放されません。
- WARPデバイスはCPU上のエミュレーションで動作するため、実GPUと比べて処理速度が大きく低下します。
- 実GPUとWARPでは、超越関数の実装差により計算結果の下位桁が一致しない場合があります。画像の一致を検証する場合は許容誤差を設けてください。
- 本ライブラリは`ComputeSharp`から派生していますが、名前空間、アセンブリ名、診断IDのすべてが異なります。両者を同一プロジェクトへ同時に参照した場合の動作は検証していません。

---

## 免責事項

本ライブラリはMITライセンスのもとで公開されています。

本ソフトウェアは「現状のまま」提供されており、明示または黙示を問わず、商品性、特定目的への適合性、および権利非侵害に関する保証を含む、いかなる種類の保証も行いません。

作者は、本ライブラリの使用または使用不能に起因するいかなる損害についても、一切の責任を負いません。ご利用は自己責任でお願いします。

---

## サードパーティライセンス

本ライブラリは以下のサードパーティソフトウェアを派生元とし、また同梱しています。ライセンスの全文はリポジトリの[`.github/LICENSE`](.github/LICENSE)に収録しています。

| ソフトウェア | 用途 | ライセンス | 著作権表示 |
|---|---|---|---|
| [ComputeSharp](https://github.com/Sergio0694/ComputeSharp) | 本ライブラリの派生元 | [MIT License](.github/LICENSE/ComputeSharp.txt) | Copyright (c) 2024 Sergio Pedri |
| [DirectX Shader Compiler](https://github.com/microsoft/DirectXShaderCompiler) | HLSLのコンパイル。`dxcompiler.dll`と`dxil.dll`を同梱 | [University of Illinois/NCSA Open Source License](https://github.com/microsoft/DirectXShaderCompiler/blob/main/LICENSE.TXT) | 同ライセンスファイルを参照 |

本リポジトリは[Sergio0694/ComputeSharp](https://github.com/Sergio0694/ComputeSharp)から派生した独立の実装であり、原作者との関係はありません。原作のComputeSharpは[DX12GameEngine](https://github.com/Aminator/DirectX12GameEngine)のコードを一部の基礎としています。

---

## ライセンス

[MIT License](LICENSE)
