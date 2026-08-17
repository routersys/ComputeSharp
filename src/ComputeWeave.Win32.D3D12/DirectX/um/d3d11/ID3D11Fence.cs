// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

// Ported from um/d3d11_3.h in the Windows SDK for Windows 10.0.26100.0
// Original source is Copyright © Microsoft. All rights reserved.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ComputeWeave.Win32;

[NativeTypeName("struct ID3D11Fence : ID3D11DeviceChild")]
[NativeInheritance("ID3D11DeviceChild")]
internal unsafe partial struct ID3D11Fence : IComObject
{
    /// <inheritdoc/>
    static Guid* IComObject.IID
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ReadOnlySpan<byte> data =
            [
                0xD1, 0xE9, 0xFD, 0xAF,
                0xF7, 0x1D,
                0xB7, 0x4B,
                0x8A,
                0x34,
                0x0F,
                0x46,
                0x25,
                0x1D,
                0xAB,
                0x80
            ];

            return (Guid*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(data));
        }
    }

    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(0)]
    public HRESULT QueryInterface([NativeTypeName("const IID &")] Guid* riid, void** ppvObject)
    {
        return ((delegate* unmanaged[MemberFunction]<ID3D11Fence*, Guid*, void**, int>)(lpVtbl[0]))((ID3D11Fence*)Unsafe.AsPointer(ref this), riid, ppvObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(1)]
    [return: NativeTypeName("ULONG")]
    public uint AddRef()
    {
        return ((delegate* unmanaged[MemberFunction]<ID3D11Fence*, uint>)(lpVtbl[1]))((ID3D11Fence*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(2)]
    [return: NativeTypeName("ULONG")]
    public uint Release()
    {
        return ((delegate* unmanaged[MemberFunction]<ID3D11Fence*, uint>)(lpVtbl[2]))((ID3D11Fence*)Unsafe.AsPointer(ref this));
    }
}
