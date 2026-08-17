// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

// Ported from um/d3d11_1.h in the Windows SDK for Windows 10.0.26100.0
// Original source is Copyright © Microsoft. All rights reserved.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ComputeWeave.Win32;

[NativeTypeName("struct ID3D11Device1 : ID3D11Device")]
[NativeInheritance("ID3D11Device")]
internal unsafe partial struct ID3D11Device1 : IComObject
{
    /// <inheritdoc/>
    static Guid* IComObject.IID
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ReadOnlySpan<byte> data =
            [
                0x29, 0xFB, 0x4B, 0xA0,
                0xEF, 0x08,
                0xD6, 0x43,
                0xA4,
                0x9C,
                0xA9,
                0xBD,
                0xBD,
                0xCB,
                0xE6,
                0x86
            ];

            return (Guid*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(data));
        }
    }

    public void** lpVtbl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(0)]
    public HRESULT QueryInterface([NativeTypeName("const IID &")] Guid* riid, void** ppvObject)
    {
        return ((delegate* unmanaged[MemberFunction]<ID3D11Device1*, Guid*, void**, int>)(lpVtbl[0]))((ID3D11Device1*)Unsafe.AsPointer(ref this), riid, ppvObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(1)]
    [return: NativeTypeName("ULONG")]
    public uint AddRef()
    {
        return ((delegate* unmanaged[MemberFunction]<ID3D11Device1*, uint>)(lpVtbl[1]))((ID3D11Device1*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(2)]
    [return: NativeTypeName("ULONG")]
    public uint Release()
    {
        return ((delegate* unmanaged[MemberFunction]<ID3D11Device1*, uint>)(lpVtbl[2]))((ID3D11Device1*)Unsafe.AsPointer(ref this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [VtblIndex(48)]
    public HRESULT OpenSharedResource1(HANDLE hResource, [NativeTypeName("const IID &")] Guid* returnedInterface, void** ppResource)
    {
        return ((delegate* unmanaged[MemberFunction]<ID3D11Device1*, HANDLE, Guid*, void**, int>)(lpVtbl[48]))((ID3D11Device1*)Unsafe.AsPointer(ref this), hResource, returnedInterface, ppResource);
    }
}
