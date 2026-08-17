// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

// Ported from um/d2d1_1.h in the Windows SDK for Windows 10.0.26100.0
// Original source is Copyright © Microsoft. All rights reserved.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ComputeWeave.Win32;

[NativeTypeName("struct ID2D1Bitmap1 : ID2D1Bitmap")]
[NativeInheritance("ID2D1Bitmap")]
internal unsafe partial struct ID2D1Bitmap1 : IComObject
{
    /// <inheritdoc/>
    static Guid* IComObject.IID
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ReadOnlySpan<byte> data =
            [
                0x4C, 0xA8, 0x98, 0xA8,
                0x73, 0x38,
                0x88, 0x45,
                0xB0,
                0x8B,
                0xEB,
                0xBF,
                0x97,
                0x8D,
                0xF0,
                0x41
            ];

            return (Guid*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(data));
        }
    }

    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(0)]
    public HRESULT QueryInterface([NativeTypeName("const IID &")] Guid* riid, void** ppvObject)
    {
        return ((delegate* unmanaged[MemberFunction]<ID2D1Bitmap1*, Guid*, void**, int>)(lpVtbl[0]))((ID2D1Bitmap1*)Unsafe.AsPointer(ref this), riid, ppvObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(1)]
    [return: NativeTypeName("ULONG")]
    public uint AddRef()
    {
        return ((delegate* unmanaged[MemberFunction]<ID2D1Bitmap1*, uint>)(lpVtbl[1]))((ID2D1Bitmap1*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(2)]
    [return: NativeTypeName("ULONG")]
    public uint Release()
    {
        return ((delegate* unmanaged[MemberFunction]<ID2D1Bitmap1*, uint>)(lpVtbl[2]))((ID2D1Bitmap1*)Unsafe.AsPointer(ref this));
    }
}
