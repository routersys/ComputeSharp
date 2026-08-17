// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

// Ported from shared/dxgi.h in the Windows SDK for Windows 10.0.26100.0
// Original source is Copyright © Microsoft. All rights reserved.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ComputeWeave.Win32;

[NativeTypeName("struct IDXGISurface : IDXGIDeviceSubObject")]
[NativeInheritance("IDXGIDeviceSubObject")]
internal unsafe partial struct IDXGISurface : IComObject
{
    /// <inheritdoc/>
    static Guid* IComObject.IID
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ReadOnlySpan<byte> data =
            [
                0x6C, 0xB5, 0xFC, 0xCA,
                0xC3, 0x6A,
                0x89, 0x48,
                0xBF,
                0x47,
                0x9E,
                0x23,
                0xBB,
                0xD2,
                0x60,
                0xEC
            ];

            return (Guid*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(data));
        }
    }

    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(0)]
    public HRESULT QueryInterface([NativeTypeName("const IID &")] Guid* riid, void** ppvObject)
    {
        return ((delegate* unmanaged[MemberFunction]<IDXGISurface*, Guid*, void**, int>)(lpVtbl[0]))((IDXGISurface*)Unsafe.AsPointer(ref this), riid, ppvObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(1)]
    [return: NativeTypeName("ULONG")]
    public uint AddRef()
    {
        return ((delegate* unmanaged[MemberFunction]<IDXGISurface*, uint>)(lpVtbl[1]))((IDXGISurface*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(2)]
    [return: NativeTypeName("ULONG")]
    public uint Release()
    {
        return ((delegate* unmanaged[MemberFunction]<IDXGISurface*, uint>)(lpVtbl[2]))((IDXGISurface*)Unsafe.AsPointer(ref this));
    }
}
