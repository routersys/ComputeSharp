using ComputeSharp.Core.Extensions;
using ComputeSharp.Graphics.Extensions;
using ComputeSharp.Win32;
using ResourceType = ComputeSharp.Graphics.Resources.Enums.ResourceType;

namespace ComputeSharp;

/// <inheritdoc/>
unsafe partial class GraphicsDevice
{
    /// <summary>
    /// クロス API 共有が可能な 2D テクスチャ リソースを生成します。
    /// </summary>
    /// <param name="resourceType">生成するリソースの種別。</param>
    /// <param name="dxgiFormat">使用する <see cref="DXGI_FORMAT"/> 値。</param>
    /// <param name="width">テクスチャの幅。</param>
    /// <param name="height">テクスチャの高さ。</param>
    /// <param name="d3D12Resource">生成された <see cref="ID3D12Resource"/> オブジェクト。</param>
    /// <param name="d3D12ResourceStates">生成されたリソースの初期 <see cref="D3D12_RESOURCE_STATES"/> 値。</param>
    internal void CreateSharedResource(
        ResourceType resourceType,
        DXGI_FORMAT dxgiFormat,
        uint width,
        uint height,
        out ComPtr<ID3D12Resource> d3D12Resource,
        out D3D12_RESOURCE_STATES d3D12ResourceStates)
    {
        d3D12Resource = this.d3D12Device.Get()->CreateSharedCommittedResource(resourceType, dxgiFormat, width, height, out d3D12ResourceStates);
    }

    /// <summary>
    /// 指定した COM オブジェクトに対する共有 NT ハンドルを生成します。
    /// </summary>
    /// <param name="pObject">共有ハンドルを生成する対象の COM オブジェクト。</param>
    /// <returns>生成された共有 NT ハンドル。</returns>
    internal HANDLE CreateSharedHandle(IUnknown* pObject)
    {
        return this.d3D12Device.Get()->CreateSharedHandle(pObject);
    }

    /// <summary>
    /// 指定した共有 NT ハンドルから <see cref="ID3D12Resource"/> を開きます。
    /// </summary>
    /// <param name="handle">開く対象の共有 NT ハンドル。</param>
    /// <returns>開かれた <see cref="ID3D12Resource"/> への参照。</returns>
    internal ComPtr<ID3D12Resource> OpenSharedResource(HANDLE handle)
    {
        return this.d3D12Device.Get()->OpenSharedHandle<ID3D12Resource>(handle);
    }

    /// <summary>
    /// 他 API と共有可能な <see cref="ID3D12Fence"/> を生成します。
    /// </summary>
    /// <returns>共有可能な <see cref="ID3D12Fence"/> への参照。</returns>
    internal ComPtr<ID3D12Fence> CreateSharedFence()
    {
        return this.d3D12Device.Get()->CreateSharedFence();
    }

    /// <summary>
    /// 指定した共有 NT ハンドルから <see cref="ID3D12Fence"/> を開きます。
    /// </summary>
    /// <param name="handle">開く対象の共有 NT ハンドル。</param>
    /// <returns>開かれた <see cref="ID3D12Fence"/> への参照。</returns>
    internal ComPtr<ID3D12Fence> OpenSharedFence(HANDLE handle)
    {
        return this.d3D12Device.Get()->OpenSharedHandle<ID3D12Fence>(handle);
    }

    /// <summary>
    /// コンピュート キュー上で指定した共有フェンスに値をシグナルします。
    /// </summary>
    /// <param name="d3D12Fence">対象の <see cref="ID3D12Fence"/>。</param>
    /// <param name="value">シグナルする値。</param>
    internal void SignalSharedFence(ID3D12Fence* d3D12Fence, ulong value)
    {
        this.d3D12ComputeCommandQueue.Get()->Signal(d3D12Fence, value).Assert();
    }

    /// <summary>
    /// コンピュート キュー上で指定した共有フェンスが目標値に達するまで待機するよう登録します。
    /// </summary>
    /// <param name="d3D12Fence">対象の <see cref="ID3D12Fence"/>。</param>
    /// <param name="value">待機する目標値。</param>
    internal void WaitForSharedFence(ID3D12Fence* d3D12Fence, ulong value)
    {
        this.d3D12ComputeCommandQueue.Get()->Wait(d3D12Fence, value).Assert();
    }
}
