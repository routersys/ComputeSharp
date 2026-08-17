// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

// Ported from um/d3d11.h in the Windows SDK for Windows 10.0.26100.0
// Original source is Copyright © Microsoft. All rights reserved.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ComputeWeave.Win32;

[NativeTypeName("struct ID3D11Texture2D : ID3D11Resource")]
[NativeInheritance("ID3D11Resource")]
internal unsafe partial struct ID3D11Texture2D : IComObject
{
    /// <inheritdoc/>
    static Guid* IComObject.IID
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ReadOnlySpan<byte> data =
            [
                0xF2, 0xAA, 0x15, 0x6F,
                0x08, 0xD2,
                0x89, 0x4E,
                0x9A,
                0xB4,
                0x48,
                0x95,
                0x35,
                0xD3,
                0x4F,
                0x9C
            ];

            return (Guid*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(data));
        }
    }

    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(0)]
    public HRESULT QueryInterface([NativeTypeName("const IID &")] Guid* riid, void** ppvObject)
    {
        return ((delegate* unmanaged[MemberFunction]<ID3D11Texture2D*, Guid*, void**, int>)(lpVtbl[0]))((ID3D11Texture2D*)Unsafe.AsPointer(ref this), riid, ppvObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(1)]
    [return: NativeTypeName("ULONG")]
    public uint AddRef()
    {
        return ((delegate* unmanaged[MemberFunction]<ID3D11Texture2D*, uint>)(lpVtbl[1]))((ID3D11Texture2D*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(2)]
    [return: NativeTypeName("ULONG")]
    public uint Release()
    {
        return ((delegate* unmanaged[MemberFunction]<ID3D11Texture2D*, uint>)(lpVtbl[2]))((ID3D11Texture2D*)Unsafe.AsPointer(ref this));
    }
}
