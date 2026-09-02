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
   - [宣言による計算パイプライン](#宣言による計算パイプライン)
   - [所有資源スロット](#所有資源スロット)
   - [Direct3D 11との相互運用](#direct3d-11との相互運用)
   - [共有テクスチャスロット](#共有テクスチャスロット)
   - [バッファの読み取り専用ビュー](#バッファの読み取り専用ビュー)
   - [GPUメモリの予算管理](#gpuメモリの予算管理)
   - [コンパイル時の検証](#コンパイル時の検証)
   - [Direct2Dのピクセルシェーダー](#direct2dのピクセルシェーダー)
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

このフォークが追加したのは、公開型67件と、`GraphicsDevice` および `InteropServices` への追加メンバーです。これらは一つの体系を成します。`[ComputePipelineHost]` を付けた型が、デバイスを保持するフィールドと、資源スロットの集合と、パイプラインメソッドの集合を宣言します。ソースジェネレーターはその宣言を読み、正準記述子をバイト配列として生成側の partial へ書き出し、実行時へ委譲する型付きのメンバーを出力します。実行時は構築の時点で記述子を解析し、あらゆる契約をそれと照合します。以降、序数、資源の参照権、構造上の上限は記述子だけが決めます。

資源は直接保持しません。世代を発行するスロットに入ります。`TryEnsure` はスロットへ要求した計画への一致を求め、計画が実際に変わったときだけ新しい世代を発行します。実行中の処理は捕捉した世代を生かし続けるため、資源の再確保が記録済みの投入を無効にすることはありません。

### 何を保証するか

このライブラリを通ってGPUへ到達する経路は全て追跡します。Lifetimeの追跡は「そのネイティブ資源を解放してよいか」に答え、Hazardの追跡は「その資源への参照がキュー間で順序付くか」に答えます。両者は別の性質であり、どちらを提供するかを経路ごとに明示します。

| 経路 | Lifetime | Hazard |
|---|---|---|
| 生成パイプライン、`ComputeContext`、資源のコピー、相互運用ドメイン | あり | あり |
| `InteropServices.AcquireNativeResource` と `AcquireNativeDevice` | あり | なし |
| `InteropServices.GetID3D12Resource`、`GetID3D12Device`、転送資源の写像ビュー | なし | なし |

ネイティブ参照は、ライブラリの外側にある対象がその資源を使っている間、資源の世代を生かし続けます。併せて、投入済みの処理の完了点を返すため、保持側はCPUを止めずに自分の処理を順序付けられます。順序付けそのものは行いません。順序が要る相互運用には相互運用ドメインを使います。

最後の行は基盤ライブラリから引き継いだ逃げ道です。互換性のために残しており、追跡の対象外です。

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

Direct2Dのピクセルシェーダーを書くための併走パッケージです。

```bash
dotnet add package ComputeWeave.D2D1
```

`ComputeWeave.Core` は推移的な依存先なので、直接参照する必要はありません。`ComputeWeave.D2D1` はこれを上の各パッケージと共有しますが、`ComputeWeave` は参照しません。

---

## 追加した機能

### 宣言による計算パイプライン

ホストは `[ComputePipelineHost]` を付けた `partial` な型です。第1引数はデバイスを保持するフィールド名、第2引数は確保する同時実行数です。パイプラインは `[ComputePipeline]` を付けたメソッドで、第1引数は `in ComputeContext` でなければなりません。

```csharp
using ComputeWeave;

[ComputePipelineHost("device", 1)]
public sealed partial class Host
{
    private readonly GraphicsDevice device;

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

### 所有資源スロット

ホストが所有する資源は、`ComputeResourceSlot<TResource>` または `ComputeResourceGroupSlot<TGroup>` のフィールドとして宣言し、`[ComputePipelineResource]` を付けて `new()` で初期化します。グループスロットの `TGroup` は `[ComputeResourceGroup]` を付けた `sealed partial class` であり、そのメンバーは `[ComputePipelineResource]` を付けた取得専用のプロパティです。ジェネレーターは `TryEnsure<スロット名>(in <計画> plan, out bool changed)` を出力し、資源が単一のスロットについては `ComputeResourceBinding<TResource>` を返す `Get<スロット名>ComputeBinding()` も出力します。

`TryEnsure` は所有資源が要求した計画に一致するかを返し、`changed` は新しい世代を発行したかを返します。世代を差し替えたときの内容の扱いは `ComputeResourceRecovery` が決め、`Discardable`、`RecreateFromHost`、`Recompute`、`CapacityOnly` から選びます。

パイプラインは、スロットのフィールド名を指定した `[ComputeOwnedResource]` を付けた引数で所有資源を受け取ります。`ComputeResourceSlot<TResource>` は `TResource` を、`ComputeResourceGroupSlot<TGroup>` は全メンバーを代入済みの `TGroup` を渡します。この引数は呼び出し側が与えるものではないため生成オーバーロードからは除かれ、本体の実行中に活性である世代ではなく、その呼び出しのために固定した世代を指します。

```csharp
[ComputePipeline]
private void Run(
    in ComputeContext context,
    [ComputeOwnedResource(nameof(index))] ReadWriteBuffer<int> index,
    [ComputeOwnedResource(nameof(grid))] GridResources grid)
{
    context.For(index.Length, new Shader(index, grid.Cells));
}
```

### Direct3D 11との相互運用

外部のAPIはドメインとして登録します。Direct3D 11 の即時コンテキストであれば実装は同梱されており、それ以外のAPIでは `IComputeExternalInteropProvider<TView>` を自分で実装します。実装側は、共有タイムラインの初期化、自身のキューへの信号と待機の投入、共有テクスチャを自身のビュー型として開く処理を求められます。

```csharp
using ComputeInteropDomain domain = device.RegisterExternalDomain(provider);
```

`ComputeInteropDomain` は `Device`、`Id`、`Capabilities` と、`Dispose` および `WaitForDisposal` の対を公開します。`ExternalInteropCapabilities` は `SharedFence`、`SharedTexture2D`、`SingleImmediateContextOrdering`、`PersistentExternalViewOrdering` を報告します。

Direct3D 11 の即時コンテキストを外部キューとして使う場合、実装を自分で書く必要はありません。`ComputeExternalDirect3D11Provider` が共有フェンスの開示、信号と待機と Flush の投入、共有テクスチャの開示と外部ビューの生成をすべて引き受けます。デバイスと即時コンテキストと描画対象を生のCOMポインタとして渡すため、利用者が使っている束縛の種類に依存しません。

```csharp
using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();
ComputeExternalDirect3D11Provider provider = new(device, immediateContext, renderTarget, scheduler);
using ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(provider);
```

`ComputeExternalQueueScheduler.Create()` は、一つの即時コンテキストに対する予約を単一飛行へ直列化するSchedulerを返します。同じ即時コンテキストへ積む Provider は同じインスタンスを共有します。その対応付けは利用者が保ちます。ライブラリは即時コンテキストを型として観測しないため、対応付けの正しさを検証できません。

生成される `ExternalDirect3D11TextureView` は、開いたテクスチャと、描画対象が与えられていればその上のビットマップを保持します。`Texture` と `Bitmap` は借用であり解放してはなりません。自分の束縛へ渡す場合は `AddRefTexture()` と `AddRefBitmap()` を使ってください。参照数を1つ増やして返すため、束縛にそのまま所有させられます。

外部側が自前デバイスの Direct3D 12 コマンドキューであるホストは、`ComputeExternalDirect3D12Provider` を同じ形で使います。共有フェンスと共有テクスチャを自分のデバイスで開き、キューへ信号と待機を積みます。Direct3D 12 のキューには後から掃き出す遅延バッチが無いため、`FlushAfterSignal` は何もしません。生成される `ExternalDirect3D12TextureView` は開いた資源を借用の `Resource` として公開し、`AddRefResource()` が呼び出し側の所有する参照を返します。

```csharp
using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();
ComputeExternalDirect3D12Provider provider = new(d3D12Device, d3D12Queue, scheduler);
using ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(provider);
```

ドメインを登録するグラフィックスデバイスは、Providerと同じアダプター上で動いている必要があります。`GraphicsDevice.TryGetDevice` が識別子からそのデバイスを解決するため、ホストが照合の方法を書き下す必要はありません。

```csharp
if (!GraphicsDevice.TryGetDevice(new ExternalAdapterIdentity(adapterLuid), out GraphicsDevice? graphicsDevice))
{
    return;
}
```

操作のたびにキューへの出入りが必要な実装を自分で書く場合は `ComputeExternalQueueScheduler` を継承します。

実装側が例外を投げると、外部キューの状態を実行時が判断できなくなるため、そのドメインは汚染されます。以後そのドメインへの操作と、そこから取得した貸し出しや貸与はすべて、実装側が投げた例外を報告します。同じデバイス上の他のドメインは影響を受けません。

拒否は識別子を持ちます。`ComputeDiagnosticException` は `InvalidOperationException` から派生し、`DiagnosticId` に `CMPW3004` のような安定した識別子を載せます。再試行してよいのか、資源を作り直すべきなのか、ドメインごと畳むべきなのかは識別子ごとに異なります。**例外のメッセージ文字列で判別しないでください。** メッセージは実装の都合で変わります。

### 共有テクスチャスロット

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

ジェネレーターは `Create(GraphicsDevice device, ComputeInteropDomain domain)` と、スロットごとの `TryEnsure<スロット名>(int width, int height, out bool changed)` を出力します。`TryGet<スロット名>AllocatedSize` は `SharedTextureSlot.TryGetAllocatedSize` へ委譲し、公開中のテクスチャに確保されている幅と高さを返します。`GrowOnly` では確保サイズが論理寸法より大きい場合があります。結果は世代を固定しないスナップショットです。世代交換が並行しうる場合は、別に取得した束縛や貸し出しや貸与のサイズを表しません。`ExternalTextureLease<TView>` の `Width` と `Height` は、その貸与が保持する世代の確保寸法を表します。所有権は共有フェンスを介して受け渡します。`BeginExternalOperation` が外部API向けにビューを一時的に貸し出し、`AcquireExternalViewLease` が単一の操作を越えて保持する貸与を取り、`GetComputeBinding` が計算側の束縛を返します。

共有テクスチャの世代を退役させるとき、大きさの変更でもスロットの破棄でも、外部ビューを解放する前に外部キューを排出します。この排出は呼び出し元のスレッドではなくデバイス側で走るため、`TryEnsure` や `Dispose` から戻った時点では退役した世代がまだ保持されています。内部の保守処理が一時的にドメインを保持している場合、前景処理はその完了を待ちます。別の前景処理が保持している場合は競合した利用として拒否します。実装側が例外を投げるとそのドメインは汚染され、以後そのドメインへの操作はすべて失敗を報告します。`WaitForDisposal` は退役と破棄の完了を待ちます。

### バッファの読み取り専用ビュー

`ReadWriteBuffer<T>.AsReadOnly()` が `IReadOnlyBuffer<T>` を返します。同じ資源をSRVとして束縛するビューで、これを受けるシェーダーは書き込めません。

```csharp
using ReadWriteBuffer<int> source = device.AllocateReadWriteBuffer<int>(length);

device.For(length, new ProduceShader(source));
device.For(length, new ConsumeShader(source.AsReadOnly(), destination));
```

GPUが書いたバッファを、以降のシェーダーへ読み取り専用として渡せます。読み取り専用バッファへ複製する必要はありません。`Buffer<T>` 同士の複製はCPUがGPUの完了を待つため、フレームごとの経路からその待ちを外せます。

テクスチャの読み取り専用ビューと違い、状態の遷移は要りません。バッファは常在状態が `COMMON` で、SRVとして読むために遷移しないためです。返るビューは資源の寿命の間ずっと有効で、保持して使い回せます。`ReadOnlyBuffer<T>` も `IReadOnlyBuffer<T>` を実装するので、同じ引数へどちらも渡せます。

### GPUメモリの予算管理

`GraphicsDevice` へ3つのメンバーが加わります。`SetMemoryPolicy` はメモリ区分ごとの上限と、必要であれば利用者間を調停する `IGraphicsMemoryBudgetBroker` を設定します。`GetMemoryStatistics` は、世代番号、区分ごとの統計、世代数を持つ `GraphicsMemoryStatistics` の断面を返します。`TrimMemory` は退役して待機中の資源を解放します。世代が待機中になるのは、それを保持していた処理と外部キューが用済みになった後なので、退役させた直後に整理しても何も回収されません。

予算による確保の失敗は `GraphicsMemoryAllocationException` として現れます。これは `InvalidOperationException` を継承します。

予算の対象はデバイス自身が作成した資源です。`AllocationServices.ConfigureAllocatorFactory` で設定したアロケーター、たとえば `ComputeWeave.D3D12MemoryAllocator` パッケージのものを使うデバイスは、資源をそのアロケーター経由で作成します。それらは方針による審査を受けず、統計にも計上されず、`TrimMemory` の回収対象にもなりません。予算はアロケーター側で管理してください。

**外部アロケーターと宣言的な層は併用できません。** 世代、整理、予算のいずれもデバイスが確保を所有していることが前提であり、外部アロケーターで確保するデバイスはそれらを載せられません。`ComputeHostRuntime.Create`、`ComputeInteropResourceSetRuntime.Create`、および生成された `Create` は `NotSupportedException` を投げます。基盤部分、`ComputeContext`、資源のコピー、`InteropServices` は影響を受けません。どちらか一方を選んでください。

### コンパイル時の検証

以上の宣言はアナライザーが検査し、接頭辞 `CMPW` の診断107種類として報告します。対象は属性の位置、ホストとパイプラインメソッドの形、スロットの宣言、資源の契約、生成されるオーバーロードの衝突です。一部にはコード修正が付きます。

---

### Direct2Dのピクセルシェーダー

`ComputeWeave.D2D1` は、Direct2Dのピクセルシェーダーを C# で書くためのパッケージです。拡張ではなく併走するパッケージで、`ComputeWeave.Core` を計算側と共有しますが `ComputeWeave` は参照しません。これらのシェーダーを実行するのは Direct2D であって Direct3D 12 の計算キューではないため、`GraphicsDevice` を作ることも使うこともなく、上に述べた宣言の層も適用されません。

ピクセルシェーダーは `ID2D1PixelShader` を実装した `partial struct` です。

```csharp
using ComputeWeave;
using ComputeWeave.D2D1;

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

バイトコードと定数バッファはデバイス無しで取得できます。`D2D1PixelShaderEffect` はシェーダーを Direct2D の効果として登録し、そこから `ID2D1Effect` を作ります。`D2D1ReflectionServices` は 生成された HLSL とシェーダーの統計を返します。

```csharp
ReadOnlyMemory<byte> bytecode = D2D1PixelShader.LoadBytecode<DifferenceEffect>();
ReadOnlyMemory<byte> buffer = D2D1PixelShader.GetConstantBuffer(new DifferenceEffect(1));
```

これらの宣言は、`CMPWD2D` を接頭辞とする95件の診断を報告するアナライザーが検査します。シェーダーは Direct2D が受け付ける DXBC へ FXC でコンパイルします。`d3dcompiler_47.dll` は Windows に同梱されているため、このパッケージはコンパイラーを同梱しません。

---

## APIリファレンス

### 宣言用の属性

| メンバー | 説明 |
|---|---|
| `[ComputePipelineHost(string deviceFieldName, int maximumConcurrentInvocations)]` | partial な型をパイプラインホストとして印付けます。 |
| `[ComputePipeline]` | メソッドをパイプラインとして印付けます。第1引数は `in ComputeContext` でなければなりません。 |
| `[ComputePipelineResource(ComputeResourceAccess access)]` | ホストが借りる資源、または資源グループのメンバーを宣言します。 |
| `[ComputePipelineResource(ComputeResourceAccess access, ComputeResourceRecovery recovery)]` | 復帰の方法とともに所有資源スロットを宣言します。 |
| `[ComputeResource(ComputeResourceAccess access)]` | パイプラインメソッドの資源引数の参照契約を宣言します。`Sharing` と `Aliasing` を設定できます。 |
| `[ComputeOwnedResource(string slotFieldName)]` | パイプラインの引数を所有スロットの資源へ束ねます。 |
| `[ComputeResourceGroup]` | `sealed partial class` を資源グループとして印付けます。 |
| `[ComputeInterop]` | パイプラインメソッドを外部との往復として印付けます。 |
| `[ComputeInteropResourceSet]` | partial な型を相互運用の資源集合として印付けます。 |
| `[ComputeSharedTexture(resizePolicy, computeAccess, externalAccess, externalUsage, alphaMode, initialOwner, recovery)]` | 共有テクスチャのスロットを宣言します。 |

### 生成されるメンバー

| メンバー | 説明 |
|---|---|
| `static THost Create(GraphicsDevice device, int maximumPendingSubmissions)` | ホストをデバイスへ登録します。 |
| `ComputeSubmission <パイプライン名>(...)` | パイプラインを1回記録して投入します。`Sharing.External` を宣言した資源の引数は `ComputeResourceBinding<T>` へ置き換わります。 |
| `bool TryEnsure<スロット名>(in TPlan plan, out bool changed)` | 所有資源を計画へ一致させます。 |
| `ComputeResourceBinding<T> Get<スロット名>ComputeBinding()` | 所有資源の束縛を返します。 |
| `static TSet Create(GraphicsDevice device, ComputeInteropDomain domain)` | 相互運用の資源集合を登録します。 |
| `bool TryEnsure<スロット名>(int width, int height, out bool changed)` | 共有テクスチャを寸法へ一致させます。 |
| `bool TryGet<スロット名>AllocatedSize(out int width, out int height)` | 発行済みの共有テクスチャの確保寸法を、世代を固定しないスナップショットとして返します。 |
| `ComputeResourceBinding<ReadWriteTexture2D<T, TPixel>> Get<スロット名>ComputeBinding()` | 共有テクスチャの計算側の束縛を返します。 |
| `BorrowedExternalTextureView<TView> Begin<スロット名>ExternalOperation()` | 1回の操作のために外部ビューを借ります。 |
| `ExternalTextureLease<TView> Acquire<スロット名>ExternalViewLease()` | 外部ビューの永続的な貸与を取ります。 |
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
| `SharedTextureSlot.TryGetAllocatedSize(out int width, out int height)` | 発行済み世代の確保寸法を、世代を固定しないスナップショットとして返します。 |
| `SharedTextureSlot.GetComputeBinding()` | 計算側の束縛を返します。 |
| `SharedTextureSlot.BeginExternalOperation()` | 1回の操作のために外部ビューを借ります。 |
| `SharedTextureSlot.AcquireExternalViewLease()` | 外部ビューの貸与を取ります。 |
| `SharedTextureSlot.Width` / `Height` / `IsAllocated` | 現在の論理寸法と、世代が発行されているかを報告します。 |
| `ComputeResourceBinding<TResource>` | 発行済みの資源世代への束縛です。生成元のスロットを保持します。 |
| `ComputePipelineBinder.TryPin(IGraphicsResource resource)` | 借用した資源の世代を固定します。 |
| `ComputePipelineBinder.TryPin<TResource>(in ComputeResourceBinding<TResource> binding, out TResource resource)` | 外部と共有する資源を、束縛が持つスロットの門で再検証してから固定します。 |
| `ComputePipelineBinder.TryPin<TResource>(int slotOrdinal, in ComputeResourceBinding<TResource> binding)` | ホストが所有するスロットの資源を固定します。 |
| `IComputeGenerationMaterializer.Materialize(ref ComputeGenerationContext)` | 生成される実体化器が実装します。 |
| `IReadOnlyBuffer<T>` | シェーダーが読み取り専用として受ける構造化バッファです。 |
| `ReadWriteBuffer<T>.AsReadOnly()` | 同じ資源をSRVとして束縛する読み取り専用ビューを返します。 |

### 相互運用

| メンバー | 説明 |
|---|---|
| `GraphicsDevice.RegisterExternalDomain<TView>(IComputeExternalInteropProvider<TView> provider)` | 外部APIを登録してドメインを返します。 |
| `GraphicsDevice.TryGetDevice(ExternalAdapterIdentity adapterIdentity, out GraphicsDevice? device)` | 与えた識別子のアダプター上で動くデバイスを解決します。 |
| `ComputeInteropDomain.Device` / `Id` / `Capabilities` | デバイス、ドメイン識別子、合意した能力を報告します。 |
| `IComputeExternalInteropProvider.Initialize(in ExternalTimelineInitialization)` | 共有タイムラインを初期化します。 |
| `IComputeExternalInteropProvider.EnqueueSignal(ulong)` / `EnqueueWait(ulong)` / `FlushAfterSignal()` | 外部キュー上で共有フェンスを駆動します。 |
| `IComputeExternalInteropProvider.OpenSharedTexture(BorrowedSharedHandle, in ExternalTextureDescriptor)` | 共有テクスチャを外部のビュー型として開きます。 |
| `IComputeExternalInteropProvider.OnDeviceTerminal(Exception)` | デバイスが終了状態へ入ったことを通知します。 |
| `ComputeExternalQueueScheduler` | 操作ごとにキューの出入りが必要な実装の基底クラスです。 |
| `ComputeExternalQueueScheduler.Create()` | 一つの即時コンテキストの予約を単一飛行へ直列化するSchedulerを返します。 |
| `ComputeExternalDirect3D11Provider(nint device, nint immediateContext, nint renderTarget, ComputeExternalQueueScheduler)` | Direct3D 11 の即時コンテキストを外部キューとするProviderです。 |
| `ExternalDirect3D11TextureView.Texture` / `Bitmap` | 開いたテクスチャとビットマップを借用として返します。解放しないでください。 |
| `ExternalDirect3D11TextureView.AddRefTexture()` / `AddRefBitmap()` | 参照数を1つ増やして返します。呼び出し側が解放します。 |
| `ComputeExternalDirect3D12Provider(nint device, nint queue, ComputeExternalQueueScheduler)` | 自前デバイスの Direct3D 12 コマンドキューを外部キューとするProviderです。 |
| `ExternalDirect3D12TextureView.Resource` / `AddRefResource()` | 開いた資源を借用として返します。AddRef側は呼び出し側の所有する参照を返します。 |
| `IComputeDiagnostic.DiagnosticId` / `ComputeDiagnosticException` | 拒否の識別子を報告します。 |
| `ExternalTextureLease<TView>.Width` / `Height` | 貸与が保持する世代の確保寸法を返します。 |
| `ExternalTextureLease<TView>.DangerousGetView()` / `BeginExternalQueueOperation()` | 貸与した外部ビューを使います。 |
| `ExternalTextureDescriptor` | `Width`、`Height`、`Format`、`ExternalUsage`、`AlphaMode`。 |
| `ExternalAdapterIdentity(long adapterLuid)` / `ExternalDomainId` | アダプターとドメインを識別します。 |
| `InteropServices.AcquireNativeResource(resource, out NativeResourceSynchronization, NativeResourceAcquisition)` | バッファ、テクスチャ、転送資源の世代を、外部の対象が使っている間だけ生かします。 |
| `NativeResourceReference.QueryInterface(Guid*, void**)` / `TryQueryInterface(Guid*, void**)` / `IsValid` / `Dispose()` | ネイティブ参照を使い、解放します。必ず破棄してください。 |
| `NativeResourceSynchronization.LastWrite` / `LastComputeRead` / `LastCopyRead` | その世代へ投入済みの処理の完了点を返します。 |
| `InteropServices.GetID3D12Fence(GraphicsDevice, ComputeQueueKind, Guid*, void**)` | キューのフェンスを取得します。外部の処理を上の完了点へ待たせるために使います。 |
| `InteropServices.AcquireNativeDevice(GraphicsDevice)` | 外部の対象がデバイスのネイティブオブジェクトを使っている間、デバイスを生かします。 |
| `NativeDeviceReference.QueryInterface(Guid*, void**)` / `TryQueryInterface(Guid*, void**)` / `IsValid` / `Dispose()` | デバイス参照を使い、解放します。必ず破棄してください。 |

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
| `GraphicsMemoryStatistics` | `Epoch`、`Local`、`NonLocal`、`ActiveGenerationCount`、`RetiredGenerationCount`、`ManagedPoolSurplusCount`、`NativeReferencedGenerationCount`。 |
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
- 共有テクスチャは `Bgra8Unorm` に固定されています。すべての共有テクスチャ世代のネイティブ記述子が固定されているため、`ExternalTextureFormat` はこの1件だけを宣言し、共有テクスチャのスロットはこの形式が対応する画素型だけを保持します。別の画素型で宣言したスロットは、資源集合の生成時に拒否します。
- `ExternalTextureUsage` は `Sampled` と `RenderTarget` を宣言します。実装が外部ビューを開く方法を選ぶ値であり、ネイティブ記述子を変えません。
- 計算シェーダーの本体に書ける C# の構文は、ジェネレーターが HLSL へ変換できる範囲に限られます。範囲外の構文はコンパイル時に報告されます。ジェネレーターがその構文の診断を持つ場合は原文を指す診断として、持たない場合は生成されたコードを名指しする HLSL コンパイラーの誤りとして現れます。
- `AllMemoryBarrierWithGroupSync`、`DeviceMemoryBarrierWithGroupSync`、`GroupMemoryBarrierWithGroupSync` のいずれかへ到達するシェーダーは、各軸の差し渡しが群の大きさの倍数である場合にだけ派遣できます。派遣は差し渡しを群へ切り上げ、入口は要求された範囲の中にあるスレッドだけに本体を走らせるため、群が部分的になるとその一部のスレッドが障壁へ届きません。ジェネレーターがそのようなシェーダーを印し、派遣は倍数でない差し渡しを `ArgumentException` で拒否します。基底のライブラリはこれを拒否しません。
- `ComputeWeave.Dxc` は `dxcompiler.dll` と `dxil.dll` を同梱するため、x64 と Arm64 以外のプロセスでは動作しません。
- `Hlsl.Abort` は Direct2D の効果では使えません。既定のコンパイル指定が要求する効果のリンクはシェーダーをライブラリとして構築しますが、FXC はそこで `abort` を受け付けません。リンクを外して構築した効果は、コンパイルできても読み込みに失敗します。

---

## 注意事項

- 正準記述子はジェネレーターと実行時の間の契約です。両者は同一のバージョンで組になっており、あるバージョンが書いた記述子を別のバージョンが読むことは想定していません。
- 投入は暗黙には待ちません。結果が必要な時点で `ComputeSubmission.Wait()` を呼んでください。
- `Dispose` は登録の解除を要求し、`WaitForDisposal` はそれが完了するまで待ちます。実行中の処理は捕捉した世代を生かし続けます。
- `GraphicsDevice.GetDefault()` はプロセス内でデバイスをキャッシュし、破棄されるまで同じインスタンスを返します。
- `GraphicsDevice` の `DeviceLost` イベントは、1つのインスタンスにつき最大1回だけ発火します。デバイスの消失後、公開APIは `InvalidOperationException` を送出します。
- 符号なしどうしの `Hlsl.Mul` の結果は符号なしです。その積を割る式は整数の除算になります。
- `AppContext` のスイッチ名は `COMPUTEWEAVE_ENABLE_DEBUG_OUTPUT`、`COMPUTEWEAVE_ENABLE_DEVICE_REMOVED_EXTENDED_DATA`、`COMPUTEWEAVE_ENABLE_GPU_TIMEOUT` です。MSBuild プロパティ `ComputeWeaveEnableDebugOutput`、`ComputeWeaveEnableDeviceRemovedExtendedData`、`ComputeWeaveEnableGpuTimeout` からも設定できます。

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
