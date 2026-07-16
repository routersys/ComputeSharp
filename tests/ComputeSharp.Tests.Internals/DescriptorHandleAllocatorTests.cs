using System;
using ComputeSharp.Graphics.Commands.Interop;
using ComputeSharp.Tests.Attributes;
using ComputeSharp.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public unsafe partial class DescriptorHandleAllocatorTests
{
    [CombinatorialTestMethod]
    [AllDevices]
    public void DescriptorHandleAllocator_DefaultReturnIsIgnoredAndDuplicateReturnThrows(Device device)
    {
        ID3D12DescriptorHandleAllocator allocator = new(device.Get().D3D12Device);

        try
        {
            allocator.Return(default);
            allocator.Rent(out ID3D12ResourceDescriptorHandles handles);
            allocator.Return(in handles);

            _ = Assert.ThrowsExactly<InvalidOperationException>(() => allocator.Return(in handles));
        }
        finally
        {
            allocator.Dispose();
        }
    }
}
