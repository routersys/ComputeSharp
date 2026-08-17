// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

// Ported from um/d2d1_1.h in the Windows SDK for Windows 10.0.26100.0
// Original source is Copyright © Microsoft. All rights reserved.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ComputeWeave.Win32;

[NativeTypeName("struct ID2D1DeviceContext : ID2D1RenderTarget")]
[NativeInheritance("ID2D1RenderTarget")]
internal unsafe partial struct ID2D1DeviceContext : IComObject
{
    /// <inheritdoc/>
    static Guid* IComObject.IID
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ReadOnlySpan<byte> data =
            [
                0x7A, 0xFE, 0xF7, 0xE8,
                0x1C, 0x19,
                0x6D, 0x46,
                0xAD,
                0x95,
                0x97,
                0x56,
                0x78,
                0xBD,
                0xA9,
                0x98
            ];

            return (Guid*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(data));
        }
    }

    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(0)]
    public HRESULT QueryInterface([NativeTypeName("const IID &")] Guid* riid, void** ppvObject)
    {
        return ((delegate* unmanaged[MemberFunction]<ID2D1DeviceContext*, Guid*, void**, int>)(lpVtbl[0]))((ID2D1DeviceContext*)Unsafe.AsPointer(ref this), riid, ppvObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(1)]
    [return: NativeTypeName("ULONG")]
    public uint AddRef()
    {
        return ((delegate* unmanaged[MemberFunction]<ID2D1DeviceContext*, uint>)(lpVtbl[1]))((ID2D1DeviceContext*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(2)]
    [return: NativeTypeName("ULONG")]
    public uint Release()
    {
        return ((delegate* unmanaged[MemberFunction]<ID2D1DeviceContext*, uint>)(lpVtbl[2]))((ID2D1DeviceContext*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(62)]
    public HRESULT CreateBitmapFromDxgiSurface(IDXGISurface* surface, [NativeTypeName("const D2D1_BITMAP_PROPERTIES1 *")] D2D1_BITMAP_PROPERTIES1* bitmapProperties, ID2D1Bitmap1** bitmap)
    {
        return ((delegate* unmanaged[MemberFunction]<ID2D1DeviceContext*, IDXGISurface*, D2D1_BITMAP_PROPERTIES1*, ID2D1Bitmap1**, int>)(lpVtbl[62]))((ID2D1DeviceContext*)Unsafe.AsPointer(ref this), surface, bitmapProperties, bitmap);
    }
}
