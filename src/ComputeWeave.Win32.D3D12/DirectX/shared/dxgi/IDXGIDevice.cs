// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

// Ported from shared/dxgi.h in the Windows SDK for Windows 10.0.26100.0
// Original source is Copyright © Microsoft. All rights reserved.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ComputeWeave.Win32;

[NativeTypeName("struct IDXGIDevice : IDXGIObject")]
[NativeInheritance("IDXGIObject")]
internal unsafe partial struct IDXGIDevice : IComObject
{
    /// <inheritdoc/>
    static Guid* IComObject.IID
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ReadOnlySpan<byte> data =
            [
                0xFA, 0x77, 0xEC, 0x54,
                0x77, 0x13,
                0xE6, 0x44,
                0x8C,
                0x32,
                0x88,
                0xFD,
                0x5F,
                0x44,
                0xC8,
                0x4C
            ];

            return (Guid*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(data));
        }
    }

    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(0)]
    public HRESULT QueryInterface([NativeTypeName("const IID &")] Guid* riid, void** ppvObject)
    {
        return ((delegate* unmanaged[MemberFunction]<IDXGIDevice*, Guid*, void**, int>)(lpVtbl[0]))((IDXGIDevice*)Unsafe.AsPointer(ref this), riid, ppvObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(1)]
    [return: NativeTypeName("ULONG")]
    public uint AddRef()
    {
        return ((delegate* unmanaged[MemberFunction]<IDXGIDevice*, uint>)(lpVtbl[1]))((IDXGIDevice*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(2)]
    [return: NativeTypeName("ULONG")]
    public uint Release()
    {
        return ((delegate* unmanaged[MemberFunction]<IDXGIDevice*, uint>)(lpVtbl[2]))((IDXGIDevice*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(7)]
    public HRESULT GetAdapter(IDXGIAdapter** pAdapter)
    {
        return ((delegate* unmanaged[MemberFunction]<IDXGIDevice*, IDXGIAdapter**, int>)(lpVtbl[7]))((IDXGIDevice*)Unsafe.AsPointer(ref this), pAdapter);
    }
}
