// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

// Ported from shared/dxgi1_4.h in the Windows SDK for Windows 10.0.20348.0
// Original source is Copyright © Microsoft. All rights reserved.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ComputeSharp.Win32;

[NativeTypeName("struct IDXGIAdapter3 : IDXGIAdapter2")]
[NativeInheritance("IDXGIAdapter2")]
internal unsafe partial struct IDXGIAdapter3 : IComObject
{
    /// <inheritdoc/>
    static Guid* IComObject.IID
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ReadOnlySpan<byte> data =
            [
                0xA4, 0x67, 0x59, 0x64,
                0x92, 0x13,
                0x10, 0x43,
                0xA7,
                0x98,
                0x80,
                0x53,
                0xCE,
                0x3E,
                0x93,
                0xFD
            ];

            return (Guid*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(data));
        }
    }

    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(0)]
    public HRESULT QueryInterface([NativeTypeName("const IID &")] Guid* riid, void** ppvObject)
    {
        return ((delegate* unmanaged[MemberFunction]<IDXGIAdapter3*, Guid*, void**, int>)(lpVtbl[0]))((IDXGIAdapter3*)Unsafe.AsPointer(ref this), riid, ppvObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(1)]
    [return: NativeTypeName("ULONG")]
    public uint AddRef()
    {
        return ((delegate* unmanaged[MemberFunction]<IDXGIAdapter3*, uint>)(lpVtbl[1]))((IDXGIAdapter3*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(2)]
    [return: NativeTypeName("ULONG")]
    public uint Release()
    {
        return ((delegate* unmanaged[MemberFunction]<IDXGIAdapter3*, uint>)(lpVtbl[2]))((IDXGIAdapter3*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(14)]
    public HRESULT QueryVideoMemoryInfo(uint NodeIndex, DXGI_MEMORY_SEGMENT_GROUP MemorySegmentGroup, DXGI_QUERY_VIDEO_MEMORY_INFO* pVideoMemoryInfo)
    {
        return ((delegate* unmanaged[MemberFunction]<IDXGIAdapter3*, uint, DXGI_MEMORY_SEGMENT_GROUP, DXGI_QUERY_VIDEO_MEMORY_INFO*, int>)(lpVtbl[14]))((IDXGIAdapter3*)Unsafe.AsPointer(ref this), NodeIndex, MemorySegmentGroup, pVideoMemoryInfo);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(16)]
    public HRESULT RegisterVideoMemoryBudgetChangeNotificationEvent(HANDLE hEvent, [NativeTypeName("DWORD *")] uint* pdwCookie)
    {
        return ((delegate* unmanaged[MemberFunction]<IDXGIAdapter3*, HANDLE, uint*, int>)(lpVtbl[16]))((IDXGIAdapter3*)Unsafe.AsPointer(ref this), hEvent, pdwCookie);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(17)]
    public void UnregisterVideoMemoryBudgetChangeNotification([NativeTypeName("DWORD")] uint dwCookie)
    {
        ((delegate* unmanaged[MemberFunction]<IDXGIAdapter3*, uint, void>)(lpVtbl[17]))((IDXGIAdapter3*)Unsafe.AsPointer(ref this), dwCookie);
    }
}
