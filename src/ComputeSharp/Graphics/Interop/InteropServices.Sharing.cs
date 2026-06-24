using System;
using ComputeSharp.Core.Extensions;
using ComputeSharp.Resources;
using ComputeSharp.Win32;

namespace ComputeSharp.Interop;

/// <inheritdoc/>
public static unsafe partial class InteropServices
{
    /// <summary>
    /// クロス API 共有が可能な <see cref="ReadWriteTexture2D{T}"/> を確保します。
    /// </summary>
    /// <typeparam name="T">テクスチャに格納する要素の型。</typeparam>
    /// <param name="device">テクスチャの確保に使用する <see cref="GraphicsDevice"/>。</param>
    /// <param name="width">テクスチャの幅。</param>
    /// <param name="height">テクスチャの高さ。</param>
    /// <returns>共有可能な <see cref="ReadWriteTexture2D{T}"/> インスタンス。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="device"/> が <see langword="null"/> の場合にスローされます。</exception>
    /// <remarks>
    /// 返されるテクスチャは <see cref="CreateSharedHandle{T}(Texture2D{T})"/> で共有 NT ハンドルをエクスポートでき、
    /// D3D11 など他 API から <c>OpenSharedResource1</c> で開けます。クロス API の同期は共有フェンスで行ってください。
    /// </remarks>
    public static ReadWriteTexture2D<T> AllocateSharedReadWriteTexture2D<T>(GraphicsDevice device, int width, int height)
        where T : unmanaged
    {
        default(ArgumentNullException).ThrowIfNull(device);

        return new ReadWriteTexture2D<T>(device, width, height);
    }

    /// <summary>
    /// クロス API 共有が可能な <see cref="ReadOnlyTexture2D{T}"/> を確保します。
    /// </summary>
    /// <typeparam name="T">テクスチャに格納する要素の型。</typeparam>
    /// <param name="device">テクスチャの確保に使用する <see cref="GraphicsDevice"/>。</param>
    /// <param name="width">テクスチャの幅。</param>
    /// <param name="height">テクスチャの高さ。</param>
    /// <returns>共有可能な <see cref="ReadOnlyTexture2D{T}"/> インスタンス。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="device"/> が <see langword="null"/> の場合にスローされます。</exception>
    public static ReadOnlyTexture2D<T> AllocateSharedReadOnlyTexture2D<T>(GraphicsDevice device, int width, int height)
        where T : unmanaged
    {
        default(ArgumentNullException).ThrowIfNull(device);

        return new ReadOnlyTexture2D<T>(device, width, height);
    }

    /// <summary>
    /// 外部 API が所有する共有 NT ハンドルを <see cref="ReadWriteTexture2D{T}"/> として開きます。
    /// </summary>
    /// <typeparam name="T">テクスチャに格納する要素の型。</typeparam>
    /// <param name="device">リソースを開くために使用する <see cref="GraphicsDevice"/>。</param>
    /// <param name="handle">開く対象の共有 NT ハンドル。</param>
    /// <returns>共有リソースをラップする <see cref="ReadWriteTexture2D{T}"/> インスタンス。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="device"/> が <see langword="null"/> の場合にスローされます。</exception>
    /// <exception cref="ArgumentException">共有リソースが 2D テクスチャでない、またはフォーマットが <typeparamref name="T"/> と一致しない場合にスローされます。</exception>
    /// <remarks>
    /// インポートしたリソースを <see cref="ReadWriteTexture2D{T}"/> として書き込み用に使用する場合、元のリソースは UAV 対応かつ
    /// 同時アクセス可能 (simultaneous access) として作成されている必要があります。クロス API の同期は共有フェンスで行ってください。
    /// </remarks>
    public static ReadWriteTexture2D<T> OpenSharedReadWriteTexture2D<T>(GraphicsDevice device, nint handle)
        where T : unmanaged
    {
        default(ArgumentNullException).ThrowIfNull(device);

        using ReferenceTracker.Lease _0 = device.GetReferenceTracker().GetLease();
        using ComPtr<ID3D12Resource> d3D12Resource = device.OpenSharedResource(new HANDLE((void*)handle));

        return new ReadWriteTexture2D<T>(device, d3D12Resource.Get());
    }

    /// <summary>
    /// 外部 API が所有する共有 NT ハンドルを <see cref="ReadOnlyTexture2D{T}"/> として開きます。
    /// </summary>
    /// <typeparam name="T">テクスチャに格納する要素の型。</typeparam>
    /// <param name="device">リソースを開くために使用する <see cref="GraphicsDevice"/>。</param>
    /// <param name="handle">開く対象の共有 NT ハンドル。</param>
    /// <returns>共有リソースをラップする <see cref="ReadOnlyTexture2D{T}"/> インスタンス。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="device"/> が <see langword="null"/> の場合にスローされます。</exception>
    /// <exception cref="ArgumentException">共有リソースが 2D テクスチャでない、またはフォーマットが <typeparamref name="T"/> と一致しない場合にスローされます。</exception>
    public static ReadOnlyTexture2D<T> OpenSharedReadOnlyTexture2D<T>(GraphicsDevice device, nint handle)
        where T : unmanaged
    {
        default(ArgumentNullException).ThrowIfNull(device);

        using ReferenceTracker.Lease _0 = device.GetReferenceTracker().GetLease();
        using ComPtr<ID3D12Resource> d3D12Resource = device.OpenSharedResource(new HANDLE((void*)handle));

        return new ReadOnlyTexture2D<T>(device, d3D12Resource.Get());
    }

    /// <summary>
    /// 共有可能なテクスチャの基になる <see cref="ID3D12Resource"/> に対する共有 NT ハンドルをエクスポートします。
    /// </summary>
    /// <typeparam name="T">テクスチャに格納する要素の型。</typeparam>
    /// <param name="texture">共有ハンドルをエクスポートする対象の <see cref="Texture2D{T}"/>。</param>
    /// <returns>エクスポートされた共有 NT ハンドル。呼び出し側が <c>CloseHandle</c> で解放する責任を持ちます。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="texture"/> が <see langword="null"/> の場合にスローされます。</exception>
    /// <remarks>
    /// このメソッドは、<see cref="AllocateSharedReadWriteTexture2D{T}(GraphicsDevice, int, int)"/> など共有可能として
    /// 作成されたテクスチャに対してのみ成功します。共有用に作成されていないテクスチャに対しては失敗します。
    /// </remarks>
    public static nint CreateSharedHandle<T>(Texture2D<T> texture)
        where T : unmanaged
    {
        default(ArgumentNullException).ThrowIfNull(texture);

        using ReferenceTracker.Lease _0 = texture.GetReferenceTracker().GetLease();

        HANDLE handle = texture.GraphicsDevice.CreateSharedHandle((IUnknown*)texture.D3D12Resource);

        return (nint)handle.Value;
    }

    /// <summary>
    /// クロス API 同期に使用する共有フェンスを生成し、要求されたインターフェイスとして取得します。
    /// </summary>
    /// <param name="device">フェンスの生成に使用する <see cref="GraphicsDevice"/>。</param>
    /// <param name="riid">取得するフェンス インターフェイスの識別子 (IID) への参照。</param>
    /// <param name="ppvFence"><paramref name="riid"/> で指定したインターフェイスへのポインターのアドレス。</param>
    /// <param name="sharedHandle">生成されたフェンスの共有 NT ハンドルを書き込む先のアドレス。呼び出し側が <c>CloseHandle</c> で解放する責任を持ちます。</param>
    /// <exception cref="ArgumentNullException"><paramref name="device"/> が <see langword="null"/> の場合にスローされます。</exception>
    /// <exception cref="System.ComponentModel.Win32Exception">フェンスの生成、ハンドルのエクスポート、またはインターフェイスの取得に失敗した場合にスローされます。</exception>
    /// <remarks>
    /// エクスポートされた <paramref name="sharedHandle"/> は、D3D11 側で <c>ID3D11Device5::OpenSharedFence</c> により開けます。
    /// 取得した <paramref name="ppvFence"/> は <see cref="SignalSharedFence(GraphicsDevice, void*, ulong)"/> および
    /// <see cref="WaitForSharedFence(GraphicsDevice, void*, ulong)"/> に渡して、コンピュート キュー上での同期に使用できます。
    /// </remarks>
    public static void CreateSharedFence(GraphicsDevice device, Guid* riid, void** ppvFence, nint* sharedHandle)
    {
        default(ArgumentNullException).ThrowIfNull(device);

        using ReferenceTracker.Lease _0 = device.GetReferenceTracker().GetLease();
        using ComPtr<ID3D12Fence> d3D12Fence = device.CreateSharedFence();

        HANDLE handle = device.CreateSharedHandle((IUnknown*)d3D12Fence.Get());

        *sharedHandle = (nint)handle.Value;

        d3D12Fence.Get()->QueryInterface(riid, ppvFence).Assert();
    }

    /// <summary>
    /// 共有 NT ハンドルから他 API が生成した共有フェンスを開き、要求されたインターフェイスとして取得します。
    /// </summary>
    /// <param name="device">フェンスを開くために使用する <see cref="GraphicsDevice"/>。</param>
    /// <param name="handle">開く対象の共有 NT ハンドル。</param>
    /// <param name="riid">取得するフェンス インターフェイスの識別子 (IID) への参照。</param>
    /// <param name="ppvFence"><paramref name="riid"/> で指定したインターフェイスへのポインターのアドレス。</param>
    /// <exception cref="ArgumentNullException"><paramref name="device"/> が <see langword="null"/> の場合にスローされます。</exception>
    /// <exception cref="System.ComponentModel.Win32Exception">フェンスを開く、またはインターフェイスの取得に失敗した場合にスローされます。</exception>
    public static void OpenSharedFence(GraphicsDevice device, nint handle, Guid* riid, void** ppvFence)
    {
        default(ArgumentNullException).ThrowIfNull(device);

        using ReferenceTracker.Lease _0 = device.GetReferenceTracker().GetLease();
        using ComPtr<ID3D12Fence> d3D12Fence = device.OpenSharedFence(new HANDLE((void*)handle));

        d3D12Fence.Get()->QueryInterface(riid, ppvFence).Assert();
    }

    /// <summary>
    /// コンピュート キュー上で指定した共有フェンスに値をシグナルします。
    /// </summary>
    /// <param name="device">操作を発行する <see cref="GraphicsDevice"/>。</param>
    /// <param name="d3D12Fence">対象の <c>ID3D12Fence</c> COM オブジェクトへのポインター。</param>
    /// <param name="value">シグナルする値。</param>
    /// <exception cref="ArgumentNullException"><paramref name="device"/> が <see langword="null"/> の場合にスローされます。</exception>
    /// <exception cref="System.ComponentModel.Win32Exception">シグナルの発行に失敗した場合にスローされます。</exception>
    public static void SignalSharedFence(GraphicsDevice device, void* d3D12Fence, ulong value)
    {
        default(ArgumentNullException).ThrowIfNull(device);

        using ReferenceTracker.Lease _0 = device.GetReferenceTracker().GetLease();

        device.SignalSharedFence((ID3D12Fence*)d3D12Fence, value);
    }

    /// <summary>
    /// コンピュート キュー上で指定した共有フェンスが目標値に達するまで待機するよう登録します。
    /// </summary>
    /// <param name="device">操作を発行する <see cref="GraphicsDevice"/>。</param>
    /// <param name="d3D12Fence">対象の <c>ID3D12Fence</c> COM オブジェクトへのポインター。</param>
    /// <param name="value">待機する目標値。</param>
    /// <exception cref="ArgumentNullException"><paramref name="device"/> が <see langword="null"/> の場合にスローされます。</exception>
    /// <exception cref="System.ComponentModel.Win32Exception">待機の登録に失敗した場合にスローされます。</exception>
    public static void WaitForSharedFence(GraphicsDevice device, void* d3D12Fence, ulong value)
    {
        default(ArgumentNullException).ThrowIfNull(device);

        using ReferenceTracker.Lease _0 = device.GetReferenceTracker().GetLease();

        device.WaitForSharedFence((ID3D12Fence*)d3D12Fence, value);
    }
}
