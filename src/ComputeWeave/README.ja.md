# ComputeWeave

[English](https://github.com/routersys/ComputeWeave/blob/main/src/ComputeWeave/README.md) | 日本語

ComputeWeave は、DirectX 12 の計算シェーダーを C# だけで記述できる [ComputeSharp](https://github.com/Sergio0694/ComputeSharp) から派生したライブラリです。基盤部分は変更していません。シェーダーは `IComputeShader` を実装する `partial struct` で、`GraphicsDevice.GetDefault()` がデバイスを返し、`For` がディスパッチします。

本パッケージには、このフォークが追加した部分も収めています。追加したのは宣言的な層です。計算パイプラインとその資源を属性で宣言すると、ソースジェネレーターが宣言を正準記述子という一つのバイト列へ変換してアセンブリへ埋め込み、実行時はその記述子を読んで資源の束縛、コマンドリストの記録、完了の追跡を行います。同じ層が Direct3D 11 と Direct3D 12 の境界を越えて共有テクスチャと共有フェンスを受け渡し、GPUメモリの予算管理を加えます。

## 宣言による計算パイプライン

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

ジェネレーターは同じ partial へ、静的な `Create`、`Dispose`、`WaitForDisposal` と、各パイプラインについて同名のオーバーロードを出力します。オーバーロードは文脈を除いた宣言どおりの引数を取り、`ComputeSubmission` を返します。

```csharp
using Host host = Host.Create(GraphicsDevice.GetDefault(), maximumPendingSubmissions: 4);

ComputeSubmission submission = host.Run();

submission.Wait();
```

待機は明示的に行うもので、破棄の時点で暗黙に待つことはありません。

## 所有資源スロット

ホストが所有する資源は、`ComputeResourceSlot<TResource>` または `ComputeResourceGroupSlot<TGroup>` のフィールドとして宣言します。ジェネレーターは `TryEnsure<スロット名>(in <計画> plan, out bool changed)` を出力し、資源が単一のスロットについては `Get<スロット名>ComputeBinding()` も出力します。

資源は直接保持せず、世代を発行するスロットに入ります。新しい世代は、要求した計画が実際に変わったときだけ発行されます。実行中の処理は捕捉した世代を生かし続けるため、資源の再確保が記録済みの投入を無効にすることはありません。世代を差し替えたときの内容の扱いは `ComputeResourceRecovery` が決め、`Discardable`、`RecreateFromHost`、`Recompute`、`CapacityOnly` から選びます。

## Direct3D 11との相互運用

Direct3D 11 の即時コンテキストを外部キューとして使う場合、実装を書く必要はありません。`ComputeExternalDirect3D11Provider` が共有フェンスの開示、信号と待機と Flush の投入、共有テクスチャの開示と外部ビューの生成を引き受けます。デバイスと即時コンテキストと描画対象を生のCOMポインタとして渡すため、利用している束縛の種類に依存しません。

```csharp
using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();
ComputeExternalDirect3D11Provider provider = new(device, immediateContext, renderTarget, scheduler);
using ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(provider);
```

`ComputeExternalQueueScheduler.Create()` は、一つの即時コンテキストへの予約を直列化する Scheduler を返します。同じ即時コンテキストへ積む Provider は同じインスタンスを共有します。その対応付けは利用者が保ちます。

生成される `ExternalDirect3D11TextureView` の `Texture` と `Bitmap` は借用であり解放してはなりません。自分の束縛へ渡す場合は `AddRefTexture()` と `AddRefBitmap()` を使ってください。参照数を1つ増やして返すため、束縛にそのまま所有させられます。

外部側が自前デバイスの Direct3D 12 コマンドキューであるホストは、`ComputeExternalDirect3D12Provider` を同じ形で使います。共有フェンスと共有テクスチャを自分のデバイスで開き、キューへ信号と待機を積みます。生成されるビューは開いた資源を `Resource` と `AddRefResource()` で公開します。いずれのProviderでも、その `ExternalAdapterIdentity` から `GraphicsDevice.TryGetDevice` が同じアダプター上のデバイスを解決します。

他のAPIを使う場合は `IComputeExternalInteropProvider<TView>` を自分で実装し、`GraphicsDevice.RegisterExternalDomain` で登録します。実装側は、共有タイムラインの初期化、自身のキューへの信号と待機の投入、共有テクスチャを自身のビュー型として開く処理を求められます。

共有テクスチャは、`[ComputeInteropResourceSet]` を付けた `partial` な型の中で、`[ComputeSharedTexture]` を付けた `SharedTextureSlot<T, TPixel, TView>` のフィールドとして宣言します。

```csharp
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

`TryGet<スロット名>AllocatedSize` は `SharedTextureSlot.TryGetAllocatedSize` へ委譲し、公開中のテクスチャに確保されている幅と高さを返します。`GrowOnly` では確保サイズが論理寸法より大きい場合があります。結果は世代を固定しないスナップショットです。世代交換が並行しうる場合は、別に取得した束縛や貸し出しや貸与のサイズを表しません。`ExternalTextureLease<TView>` の `Width` と `Height` は、その貸与が保持する世代の確保寸法を表します。所有権は共有フェンスを介して受け渡します。`BeginExternalOperation` が外部API向けにビューを一時的に貸し出し、`AcquireExternalViewLease` が単一の操作を越えて保持する貸与を取り、`GetComputeBinding` が計算側の束縛を返します。

共有テクスチャの世代を退役させると、外部ビューを解放する前に外部キューを排出します。この排出は呼び出し元のスレッドではなくデバイス側で走るため、`TryEnsure` や `Dispose` から戻った時点では退役した世代がまだ保持されています。内部の保守処理が一時的にドメインを保持している場合、前景処理はその完了を待ちます。別の前景処理が保持している場合は競合した利用として拒否します。実装側が例外を投げるとそのドメインは汚染され、以後そのドメインへの操作はすべて失敗を報告します。拒否は識別子を持ちます。`ComputeDiagnosticException` は `InvalidOperationException` から派生し、`DiagnosticId` に `CMPW3004` のような安定した識別子を載せます。再試行してよいのか、資源を作り直すべきなのか、ドメインごと畳むべきなのかは識別子ごとに異なるため、例外のメッセージ文字列で判別しないでください。

ハンドルを自分で管理する場合のために、`InteropServices` が共有テクスチャと共有フェンスの基本操作を直接公開しています。

## GPUメモリの予算管理

`GraphicsDevice.SetMemoryPolicy` は、メモリ区分ごとの上限と、必要であれば利用者間を調停する `IGraphicsMemoryBudgetBroker` を設定します。`GraphicsDevice.GetMemoryStatistics` は状態の断面を返し、`GraphicsDevice.TrimMemory` は退役して待機中の資源を解放します。世代が待機中になるのは、それを保持していた処理と外部キューが用済みになった後なので、退役させた直後に整理しても何も回収されません。予算による確保の失敗は `GraphicsMemoryAllocationException` として現れます。予算の対象はデバイス自身が作成した資源です。`AllocationServices.ConfigureAllocatorFactory` で設定したアロケーターを使うデバイスは資源をそちら経由で作成し、それらは方針による審査も統計への計上も受けません。外部アロケーターと宣言的な層は併用できません。世代、整理、予算はいずれもデバイスが確保を所有していることが前提であり、そのようなデバイスでは `Create` が `NotSupportedException` を投げます。基盤部分と `InteropServices` は影響を受けません。

## コンパイル時の検証

以上の宣言はアナライザーが検査し、接頭辞 `CMPW` の診断111種類として報告します。実行時の拒否も同じ `CMPW` 接頭辞を使い、番号帯で区別します。対象は属性の位置、ホストとパイプラインメソッドの形、スロットの宣言、資源の契約、生成されるオーバーロードの衝突です。一部にはコード修正が付きます。

## 詳細

APIの一覧は[リポジトリ](https://github.com/routersys/ComputeWeave)にあります。
