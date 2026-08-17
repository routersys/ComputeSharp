// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

// Ported from um/d3d11_4.h in the Windows SDK for Windows 10.0.26100.0
// Original source is Copyright © Microsoft. All rights reserved.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ComputeWeave.Win32;

[NativeTypeName("struct ID3D11Device5 : ID3D11Device4")]
[NativeInheritance("ID3D11Device4")]
internal unsafe partial struct ID3D11Device5 : IComObject
{
    /// <inheritdoc/>
    static Guid* IComObject.IID
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ReadOnlySpan<byte> data =
            [
                0x02, 0xE2, 0xFD, 0x8F,
                0xE7, 0xA0,
                0xDF, 0x45,
                0x9E,
                0x01,
                0xE8,
                0x37,
                0x80,
                0x1B,
                0x5E,
                0xA0
            ];

            return (Guid*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(data));
        }
    }

    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(0)]
    public HRESULT QueryInterface([NativeTypeName("const IID &")] Guid* riid, void** ppvObject)
    {
        return ((delegate* unmanaged[MemberFunction]<ID3D11Device5*, Guid*, void**, int>)(lpVtbl[0]))((ID3D11Device5*)Unsafe.AsPointer(ref this), riid, ppvObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(1)]
    [return: NativeTypeName("ULONG")]
    public uint AddRef()
    {
        return ((delegate* unmanaged[MemberFunction]<ID3D11Device5*, uint>)(lpVtbl[1]))((ID3D11Device5*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(2)]
    [return: NativeTypeName("ULONG")]
    public uint Release()
    {
        return ((delegate* unmanaged[MemberFunction]<ID3D11Device5*, uint>)(lpVtbl[2]))((ID3D11Device5*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(67)]
    public HRESULT OpenSharedFence(HANDLE hFence, [NativeTypeName("const IID &")] Guid* ReturnedInterface, void** ppFence)
    {
        return ((delegate* unmanaged[MemberFunction]<ID3D11Device5*, HANDLE, Guid*, void**, int>)(lpVtbl[67]))((ID3D11Device5*)Unsafe.AsPointer(ref this), hFence, ReturnedInterface, ppFence);
    }
}
