// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

// Ported from um/d3d11_3.h in the Windows SDK for Windows 10.0.26100.0
// Original source is Copyright © Microsoft. All rights reserved.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ComputeWeave.Win32;

[NativeTypeName("struct ID3D11DeviceContext4 : ID3D11DeviceContext3")]
[NativeInheritance("ID3D11DeviceContext3")]
internal unsafe partial struct ID3D11DeviceContext4 : IComObject
{
    /// <inheritdoc/>
    static Guid* IComObject.IID
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ReadOnlySpan<byte> data =
            [
                0xDA, 0x00, 0x76, 0x91,
                0x8C, 0xF5,
                0x33, 0x4C,
                0x98,
                0xD8,
                0x3E,
                0x15,
                0xB3,
                0x90,
                0xFA,
                0x24
            ];

            return (Guid*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(data));
        }
    }

    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(0)]
    public HRESULT QueryInterface([NativeTypeName("const IID &")] Guid* riid, void** ppvObject)
    {
        return ((delegate* unmanaged[MemberFunction]<ID3D11DeviceContext4*, Guid*, void**, int>)(lpVtbl[0]))((ID3D11DeviceContext4*)Unsafe.AsPointer(ref this), riid, ppvObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(1)]
    [return: NativeTypeName("ULONG")]
    public uint AddRef()
    {
        return ((delegate* unmanaged[MemberFunction]<ID3D11DeviceContext4*, uint>)(lpVtbl[1]))((ID3D11DeviceContext4*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(2)]
    [return: NativeTypeName("ULONG")]
    public uint Release()
    {
        return ((delegate* unmanaged[MemberFunction]<ID3D11DeviceContext4*, uint>)(lpVtbl[2]))((ID3D11DeviceContext4*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(111)]
    public void Flush()
    {
        ((delegate* unmanaged[MemberFunction]<ID3D11DeviceContext4*, void>)(lpVtbl[111]))((ID3D11DeviceContext4*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(147)]
    public HRESULT Signal(ID3D11Fence* pFence, [NativeTypeName("UINT64")] ulong Value)
    {
        return ((delegate* unmanaged[MemberFunction]<ID3D11DeviceContext4*, ID3D11Fence*, ulong, int>)(lpVtbl[147]))((ID3D11DeviceContext4*)Unsafe.AsPointer(ref this), pFence, Value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(148)]
    public HRESULT Wait(ID3D11Fence* pFence, [NativeTypeName("UINT64")] ulong Value)
    {
        return ((delegate* unmanaged[MemberFunction]<ID3D11DeviceContext4*, ID3D11Fence*, ulong, int>)(lpVtbl[148]))((ID3D11DeviceContext4*)Unsafe.AsPointer(ref this), pFence, Value);
    }
}
