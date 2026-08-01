# ComputeWeave

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](#)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](#)
[![Release](https://img.shields.io/github/v/release/routersys/ComputeWeave.svg)](https://github.com/routersys/ComputeWeave/releases)

[English](https://github.com/routersys/ComputeWeave/blob/main/README.md) | 日本語

---

ComputeWeave は、DirectX 12 の計算シェーダーを C# だけで記述できる [ComputeSharp](https://github.com/Sergio0694/ComputeSharp) から派生したライブラリです。
その基盤部分は変更しておらず、説明は上流にあります。本書は、このフォークが基盤の上へ追加した部分を扱います。
追加したのは宣言的な層です。計算パイプラインとその資源を属性で宣言すると、ソースジェネレーターが宣言を正準記述子という一つのバイト列へ変換してアセンブリへ埋め込み、実行時はその記述子を読んで資源の束縛、コマンドリストの記録、完了の追跡を行います。
同じ層が Direct3D 11 と Direct3D 12 の境界を越えて共有テクスチャと共有フェンスを受け渡し、GPUメモリの予算管理を加えます。

---

## 目次

1. [概要](#概要)
2. [動作要件](#動作要件)
3. [インストール方法](#インストール方法)
4. [追加した機能](#追加した機能)
   - [1. 宣言による計算パイプライン](#1-宣言による計算パイプライン)
   - [2. 所有資源スロット](#2-所有資源スロット)
   - [3. Direct3D 11との相互運用](#3-direct3d-11との相互運用)
   - [4. 共有テクスチャスロット](#4-共有テクスチャスロット)
   - [5. GPUメモリの予算管理](#5-gpuメモリの予算管理)
   - [6. コンパイル時の検証](#6-コンパイル時の検証)
5. [APIリファレンス](#apiリファレンス)
   - [宣言用の属性](#宣言用の属性)
   - [生成されるメンバー](#生成されるメンバー)
   - [実行時](#実行時)
   - [スロットと束縛](#スロットと束縛)
   - [相互運用](#相互運用)
   - [共有資源](#共有資源)
   - [メモリ](#メモリ)
   - [列挙型](#列挙型)
6. [制限事項](#制限事項)
7. [注意事項](#注意事項)
8. [免責事項](#免責事項)
9. [サードパーティライセンス](#サードパーティライセンス)
10. [ライセンス](#ライセンス)

---

## 概要

基盤部分は変更していません。計算シェーダーは `IComputeShader` を実装する `partial struct` で、`GraphicsDevice.GetDefault()` がデバイスを返し、`For` がディスパッチします。本書はそれを置き換えるものではありません。

このフォークが追加したのは、公開型56件と、`GraphicsDevice` および `InteropServices` への追加メンバーです。これらは一つの体系を成します。`[ComputePipelineHost]` を付けた型が、デバイスを保持するフィールドと、資源スロットの集合と、パイプラインメソッドの集合を宣言します。ソースジェネレーターはその宣言を読み、正準記述子をバイト配列として生成側の partial へ書き出し、実行時へ委譲する型付きのメンバーを出力します。実行時は構築の時点で記述子を解析し、あらゆる契約をそれと照合します。以降、序数、資源の参照権、構造上の上限は記述子だけが決めます。

資源は直接保持しません。世代を発行するスロットに入ります。`TryEnsure` はスロットへ要求した計画への一致を求め、計画が実際に変わったときだけ新しい世代を発行します。実行中の処理は捕捉した世代を生かし続けるため、資源の再確保が記録済みの投入を無効にすることはありません。

---

## 動作要件

| 項目 | 要件 |
|---|---|
| OS | Windows 10 以降（64bit） |
| ランタイム | .NET 10.0 |
| GPU | 機能レベル `D3D_FEATURE_LEVEL_11_0` とシェーダーモデル `D3D_SHADER_MODEL_6_0` に対応した Direct3D 12 デバイス |
| 代替 | 上記を満たすGPUが無い場合は WARP デバイスを使用します |
| 相互運用 | 共有テクスチャには、Direct3D 11 と Direct3D 12 の双方で共有ハンドルを作成できるアダプターが必要です |

---

## インストール方法

```bash
dotnet add package ComputeWeave
```

任意の拡張パッケージです。

```bash
dotnet add package ComputeWeave.Dxc
dotnet add package ComputeWeave.D3D12MemoryAllocator
```

`ComputeWeave.Core` は推移的な依存先なので、直接参照する必要はありません。

---

## 追加した機能

### 1. 宣言による計算パイプライン

ホストは `[ComputePipelineHost]` を付けた `partial` な型です。第1引数はデバイスを保持するフィールド名、第2引数は確保する同時実行数です。パイプラインは `[ComputePipeline]` を付けたメソッドで、第1引数は `in ComputeContext` でなければなりません。

```csharp
using ComputeWeave;

[ComputePipelineHost("device", 1)]
public sealed partial class Host
{
    private readonly GraphicsDevice device = null!;

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
    private readonly ComputeResourceSlot<ReadWriteBuffer<int>> index = new();

    [ComputePipeline]
    private void Run(in ComputeContext context)
    {
    }
}
```

ジェネレーターは同じ partial へ、静的な `Create`、`Dispose`、`WaitForDisposal` と、各パイプラインについて同名のオーバーロードを出力します。オーバーロードは文脈を除いた宣言どおりの引数を取り、`ComputeSubmission` を返します。引数の型の参照権がより狭い場合を除き、オーバーロードは public です。

```csharp
using Host host = Host.Create(GraphicsDevice.GetDefault(), maximumPendingSubmissions: 4);

ComputeSubmission submission = host.Run();

submission.Wait();
```

`ComputeSubmission` は `FencePoint` と `ComputeSubmissionStatus` と `IsCompleted` を持ちます。待機は明示的に行うもので、破棄の時点で暗黙に待つことはありません。

### 2. 所有資源スロット

ホストが所有する資源は、`ComputeResourceSlot<TResource>` または `ComputeResourceGroupSlot<TGroup>` のフィールドとして宣言し、`[ComputePipelineResource]` または `[ComputeResourceGroup]` を付けます。ジェネレーターは `TryEnsure<スロット名>(in <計画> plan, out bool changed)` を出力し、資源が単一のスロットについては `ComputeResourceBinding<TResource>` を返す `Get<スロット名>ComputeBinding()` も出力します。

`TryEnsure` は所有資源が要求した計画に一致するかを返し、`changed` は新しい世代を発行したかを返します。世代を差し替えたときの内容の扱いは `ComputeResourceRecovery` が決め、`Discardable`、`RecreateFromHost`、`Recompute`、`CapacityOnly` から選びます。

### 3. Direct3D 11との相互運用

外部のAPIは `IComputeExternalInteropProvider<TView>` を実装して登録します。実装側は、共有タイムラインの初期化、自身のキューへの信号と待機の投入、共有テクスチャを自身のビュー型として開く処理を求められます。

```csharp
using ComputeInteropDomain domain = device.RegisterExternalDomain(provider);
```

`ComputeInteropDomain` は `Device`、`Id`、`Capabilities` と、`Dispose` および `WaitForDisposal` の対を公開します。`ExternalInteropCapabilities` は `SharedFence`、`SharedTexture2D`、`SingleImmediateContextOrdering`、`PersistentExternalViewOrdering` を報告します。操作のたびにキューへの出入りが必要な実装は `ComputeExternalQueueScheduler` を継承します。

### 4. 共有テクスチャスロット

資源集合は `[ComputeInteropResourceSet]` を付けた `partial` な型で、`[ComputeSharedTexture]` を付けた `SharedTextureSlot<T, TPixel, TView>` のフィールドを持ちます。属性が、再確保の方針、両側それぞれの参照権、外部側の用途、アルファの扱い、最初の所有者、復帰の方法を固定します。

```csharp
using System;
using ComputeWeave;

[ComputeInteropResourceSet]
public sealed partial class ResourceSet
{
    [ComputeSharedTexture(
        ComputeResourceResizePolicy.Exact,
        ComputeResourceAccess.ReadWrite,
        ExternalResourceAccess.Write,
        ExternalTextureUsage.RenderTarget,
        ComputeAlphaMode.Premultiplied,
        ComputeSharedTextureInitialOwner.External,
        ComputeResourceRecovery.RecreateFromHost)]
    private readonly SharedTextureSlot<Bgra32, Float4, ExternalView> source;
}
```

ジェネレーターは `Create(GraphicsDevice device, ComputeInteropDomain domain)` と、スロットごとの `TryEnsure<スロット名>(int width, int height, out bool changed)` を出力します。所有権は共有フェンスを介して受け渡します。`BeginExternalOperation` が外部API向けにビューを一時的に貸し出し、`AcquireExternalViewLease` が単一の操作を越えて保持する貸与を取り、`GetComputeBinding` が計算側の束縛を返します。

### 5. GPUメモリの予算管理

`GraphicsDevice` へ3つのメンバーが加わります。`SetMemoryPolicy` はメモリ区分ごとの上限と、必要であれば利用者間を調停する `IGraphicsMemoryBudgetBroker` を設定します。`GetMemoryStatistics` は、世代番号、区分ごとの統計、世代数を持つ `GraphicsMemoryStatistics` の断面を返します。`TrimMemory` は退役して待機中の資源を解放します。

予算による確保の失敗は `GraphicsMemoryAllocationException` として現れます。これは `InvalidOperationException` を継承します。

### 6. コンパイル時の検証

以上の宣言はアナライザーが検査し、接頭辞 `CMPW` の診断92種類として報告します。対象は属性の位置、ホストとパイプラインメソッドの形、スロットの宣言、資源の契約、生成されるオーバーロードの衝突です。一部にはコード修正が付きます。

---

## APIリファレンス

### 宣言用の属性

| メンバー | 説明 |
|---|---|
| `[ComputePipelineHost(string deviceFieldName, int maximumConcurrentInvocations)]` | partial な型をパイプラインホストとして印付けます。 |
| `[ComputePipeline]` | メソッドをパイプラインとして印付けます。第1引数は `in ComputeContext` でなければなりません。 |
| `[ComputePipelineResource(ComputeResourceAccess access)]` | 所有資源スロットを宣言します。 |
| `[ComputePipelineResource(ComputeResourceAccess access, ComputeResourceRecovery recovery)]` | 復帰の方法を明示して所有資源スロットを宣言します。 |
| `[ComputeResource(ComputeResourceAccess access)]` | グループ内の資源を宣言します。`Sharing` と `Aliasing` を設定できます。 |
| `[ComputeResourceGroup]` | 資源グループのスロットを宣言します。 |
| `[ComputeInterop]` | パイプラインメソッドを外部との往復として印付けます。 |
| `[ComputeInteropResourceSet]` | partial な型を相互運用の資源集合として印付けます。 |
| `[ComputeSharedTexture(resizePolicy, computeAccess, externalAccess, externalUsage, alphaMode, initialOwner, recovery)]` | 共有テクスチャのスロットを宣言します。 |

### 生成されるメンバー

| メンバー | 説明 |
|---|---|
| `static THost Create(GraphicsDevice device, int maximumPendingSubmissions)` | ホストをデバイスへ登録します。 |
| `ComputeSubmission <パイプライン名>(...)` | パイプラインを1回記録して投入します。 |
| `bool TryEnsure<スロット名>(in TPlan plan, out bool changed)` | 所有資源を計画へ一致させます。 |
| `ComputeResourceBinding<T> Get<スロット名>ComputeBinding()` | 所有資源の束縛を返します。 |
| `static TSet Create(GraphicsDevice device, ComputeInteropDomain domain)` | 相互運用の資源集合を登録します。 |
| `bool TryEnsure<スロット名>(int width, int height, out bool changed)` | 共有テクスチャを寸法へ一致させます。 |
| `void Dispose()` / `void WaitForDisposal()` | 登録の解除を要求し、完了まで待ちます。 |

### 実行時

| メンバー | 説明 |
|---|---|
| `ComputeHostRuntime.Create(device, canonicalDescriptor, maximumPendingSubmissions, ownedSlots)` | ホストの実行時を作ります。生成コードが呼びます。 |
| `ComputeHostRuntime.Submit<TInvocation>(in TInvocation invocation)` | 1回の呼び出しを記録して投入します。 |
| `ComputeHostRuntime.TryEnsureResource<TMaterializer>(...)` | 所有スロットを計画へ一致させます。 |
| `ComputeHostRuntime.GetBinding<TResource>(int slotOrdinal, int resourceIndex)` | 資源の束縛を返します。 |
| `ComputeHostRuntime.Device` / `IsDisposeRequested` | デバイスと破棄状態を報告します。 |
| `ComputeInteropResourceSetRuntime.Create(device, domain, canonicalDescriptor, slots)` | 資源集合の実行時を作ります。 |
| `ComputeInteropResourceSetRuntime.Device` / `Domain` / `IsDisposeRequested` | デバイス、ドメイン、破棄状態を報告します。 |
| `ComputeSubmission.Completion` / `Status` / `IsCompleted` / `Wait()` | 投入した処理の完了を追跡します。 |
| `IComputePipelineInvocation.Bind(ref ComputePipelineBinder)` / `Record(in ComputeContext)` | 生成される呼び出し型が実装します。 |

### スロットと束縛

| メンバー | 説明 |
|---|---|
| `ComputeResourceSlot<TResource>` | 単一の資源を所有し、その世代を発行します。 |
| `ComputeResourceGroupSlot<TGroup>` | 一つの世代として発行される資源の組を所有します。 |
| `SharedTextureSlot<T, TPixel, TView>` | 外部APIと共有するテクスチャを所有します。 |
| `SharedTextureSlot.TryEnsure(int width, int height, out bool changed)` | テクスチャを寸法へ一致させます。 |
| `SharedTextureSlot.GetComputeBinding()` | 計算側の束縛を返します。 |
| `SharedTextureSlot.BeginExternalOperation()` | 1回の操作のために外部ビューを借ります。 |
| `SharedTextureSlot.AcquireExternalViewLease()` | 外部ビューの貸与を取ります。 |
| `SharedTextureSlot.Width` / `Height` / `IsAllocated` | 発行済みの寸法と、その有無を報告します。 |
| `ComputeResourceBinding<TResource>` | 発行済みの資源世代への束縛です。 |
| `IComputeGenerationMaterializer.Materialize(ref ComputeGenerationContext)` | 生成される実体化器が実装します。 |

### 相互運用

| メンバー | 説明 |
|---|---|
| `GraphicsDevice.RegisterExternalDomain<TView>(IComputeExternalInteropProvider<TView> provider)` | 外部APIを登録してドメインを返します。 |
| `ComputeInteropDomain.Device` / `Id` / `Capabilities` | デバイス、ドメイン識別子、合意した能力を報告します。 |
| `IComputeExternalInteropProvider.Initialize(in ExternalTimelineInitialization)` | 共有タイムラインを初期化します。 |
| `IComputeExternalInteropProvider.EnqueueSignal(ulong)` / `EnqueueWait(ulong)` / `FlushAfterSignal()` | 外部キュー上で共有フェンスを駆動します。 |
| `IComputeExternalInteropProvider.OpenSharedTexture(BorrowedSharedHandle, in ExternalTextureDescriptor)` | 共有テクスチャを外部のビュー型として開きます。 |
| `IComputeExternalInteropProvider.OnDeviceTerminal(Exception)` | デバイスが終了状態へ入ったことを通知します。 |
| `ComputeExternalQueueScheduler` | 操作ごとにキューの出入りが必要な実装の基底クラスです。 |
| `ExternalTextureLease<TView>.DangerousGetView()` / `BeginExternalQueueOperation()` | 貸与した外部ビューを使います。 |
| `ExternalTextureDescriptor` | `Width`、`Height`、`Format`、`ExternalUsage`、`AlphaMode`。 |
| `ExternalAdapterIdentity(long adapterLuid)` / `ExternalDomainId` | アダプターとドメインを識別します。 |

### 共有資源

| メンバー | 説明 |
|---|---|
| `InteropServices.AllocateSharedReadWriteTexture2D<T>(device, width, height)` | 共有可能な読み書きテクスチャを確保します。 |
| `InteropServices.AllocateSharedReadWriteTexture2D<T, TPixel>(device, width, height)` | 共有可能な正規化読み書きテクスチャを確保します。 |
| `InteropServices.AllocateSharedReadOnlyTexture2D<T>(device, width, height)` | 共有可能な読み取り専用テクスチャを確保します。 |
| `InteropServices.OpenSharedReadWriteTexture2D<T>(device, handle)` | ハンドルから共有テクスチャを開きます。 |
| `InteropServices.OpenSharedReadWriteTexture2D<T, TPixel>(device, handle)` | ハンドルから共有の正規化テクスチャを開きます。 |
| `InteropServices.OpenSharedReadOnlyTexture2D<T>(device, handle)` | ハンドルから共有の読み取り専用テクスチャを開きます。 |
| `InteropServices.CreateSharedHandle<T>(Texture2D<T> texture)` | テクスチャの共有ハンドルを作ります。 |
| `InteropServices.CreateSharedFence(device, riid, ppvFence, sharedHandle)` | 共有フェンスとそのハンドルを作ります。 |
| `InteropServices.OpenSharedFence(device, handle, riid, ppvFence)` | ハンドルから共有フェンスを開きます。 |
| `InteropServices.SignalSharedFence(device, d3D12Fence, value)` | 計算キュー上で共有フェンスへ信号を出します。 |
| `InteropServices.WaitForSharedFence(device, d3D12Fence, value)` | 計算キュー上で共有フェンスを待ちます。 |

### メモリ

| メンバー | 説明 |
|---|---|
| `GraphicsDevice.SetMemoryPolicy(in GraphicsMemoryPolicy policy)` | 予算の方針を設定します。 |
| `GraphicsDevice.GetMemoryStatistics()` | メモリ状態の断面を返します。 |
| `GraphicsDevice.TrimMemory()` | 退役して待機中のメモリを解放します。 |
| `GraphicsMemoryPolicy` | `BudgetBroker`、`LocalOwnedHardLimitBytes`、`NonLocalOwnedHardLimitBytes`。 |
| `GraphicsMemoryStatistics` | `Epoch`、`Local`、`NonLocal`、`ActiveGenerationCount`、`RetiredGenerationCount`、`ManagedPoolSurplusCount`。 |
| `IGraphicsMemoryBudgetBroker.RegisterClient(in GraphicsMemoryClientDescriptor)` | 予算の利用者を登録します。 |
| `IGraphicsMemoryBudgetClient.TryGetGrant(GraphicsMemorySegment, out GraphicsMemoryGrant)` | 区分に対する割り当てを要求します。 |
| `GraphicsMemoryAllocationException` | 予算が確保を拒んだときに送出されます。 |

### 列挙型

| 型 | メンバー |
|---|---|
| `ComputeResourceAccess` | `Read`、`Write`、`ReadWrite` |
| `ComputeResourceResizePolicy` | `Exact`、`GrowOnly` |
| `ComputeResourceRecovery` | `Discardable`、`RecreateFromHost`、`Recompute`、`CapacityOnly` |
| `ComputeResourceSharing` / `ComputeResourceAliasing` | `[ComputeResource]` の選択肢 |
| `ComputeSharedTextureInitialOwner` | `Compute`、`External` |
| `ExternalResourceAccess` | `Read`、`Write`、`ReadWrite` |
| `ExternalTextureUsage` | `Sampled`、`RenderTarget` |
| `ComputeAlphaMode` | `Ignore`、`Premultiplied`、`Straight` |
| `ComputeQueueKind` | `None`、`Compute`、`Copy` |
| `ComputeSubmissionStatus` | `Succeeded`、`Pending`、`Faulted` |
| `ExternalTextureFormat` | `Bgra8Unorm` |
| `ExternalInteropCapabilities` | `None`、`SharedFence`、`SharedTexture2D`、`SingleImmediateContextOrdering`、`PersistentExternalViewOrdering` |
| `GraphicsMemorySegment` | `Local`、`NonLocal` |
| `MemoryBudgetStatus` | `Unknown`、`Valid`、`Unsupported`、`DeviceLost` |

---

## 制限事項

- Windows 専用です。Direct3D 12 を利用するため、他のOSでは動作しません。
- `ExternalTextureFormat` が宣言するメンバーは現在 `Bgra8Unorm` の1件です。共有テクスチャはこの形式に限られます。
- `ExternalTextureUsage` が宣言するのは `Sampled` と `RenderTarget` だけです。
- 計算シェーダーの本体に書ける C# の構文は、ジェネレーターが HLSL へ変換できる範囲に限られます。範囲外の構文はコンパイル時に診断として報告します。
- `ComputeWeave.Dxc` は `dxcompiler.dll` と `dxil.dll` を同梱するため、x64 と Arm64 以外のプロセスでは動作しません。

---

## 注意事項

- 正準記述子はジェネレーターと実行時の間の契約です。両者は同一のバージョンで組になっており、あるバージョンが書いた記述子を別のバージョンが読むことは想定していません。
- 投入は暗黙には待ちません。結果が必要な時点で `ComputeSubmission.Wait()` を呼んでください。
- `Dispose` は登録の解除を要求し、`WaitForDisposal` はそれが完了するまで待ちます。実行中の処理は捕捉した世代を生かし続けます。
- `GraphicsDevice.GetDefault()` はプロセス内でデバイスをキャッシュし、破棄されるまで同じインスタンスを返します。
- `GraphicsDevice` の `DeviceLost` イベントは、1つのインスタンスにつき最大1回だけ発火します。デバイスの消失後、公開APIは `InvalidOperationException` を送出します。

---

## 免責事項

本ライブラリはMITライセンスのもとで公開されています。

本ソフトウェアは「現状のまま」提供されており、明示または黙示を問わず、商品性、特定目的への適合性、および権利非侵害に関する保証を含む、いかなる種類の保証も行いません。

作者は、本ライブラリの使用または使用不能に起因するいかなる損害についても、一切の責任を負いません。

---

## サードパーティライセンス

ライセンスの全文は、リポジトリの [`.github/LICENSE`](.github/LICENSE) と、NuGetパッケージの `THIRD-PARTY-NOTICES` に収録しています。

| ソフトウェア | 用途 | ライセンス | 著作権表示 |
|---|---|---|---|
| [ComputeSharp](https://github.com/Sergio0694/ComputeSharp) | 本ライブラリの派生元 | [MIT License](.github/LICENSE/ComputeSharp.txt) | Copyright (c) 2024 Sergio Pedri |
| [DirectX Shader Compiler](https://github.com/microsoft/DirectXShaderCompiler) | HLSLのコンパイル。`dxcompiler.dll` と `dxil.dll` を同梱 | [University of Illinois/NCSA Open Source License](.github/LICENSE/DirectXShaderCompiler.txt)（[第三者表記](.github/LICENSE/DirectXShaderCompiler.ThirdPartyNotices.txt)） | Copyright (c) 2003-2015 University of Illinois at Urbana-Champaign |

本リポジトリは [Sergio0694/ComputeSharp](https://github.com/Sergio0694/ComputeSharp) から派生した独立の実装であり、原作者との関係はありません。原作の ComputeSharp は [DX12GameEngine](https://github.com/Aminator/DirectX12GameEngine) のコードを一部の基礎としています。

---

## ライセンス

[MIT License](LICENSE)
