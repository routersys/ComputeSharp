using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests;

[TestClass]
public partial class IsolatedSignDoubleReproTests
{
    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_IsolatedSignDouble(Device device)
    {
        if (!device.Get().IsDoublePrecisionSupportAvailable())
        {
            Assert.Inconclusive();
        }

        using ReadWriteBuffer<int> results = device.Get().AllocateReadWriteBuffer<int>(1);

        device.Get().For(1, new IsolatedSignDoubleShader(results, -3.5));

        int[] values = results.ToArray();

        Assert.AreEqual(-1, values[0]);
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [RequiresDoublePrecisionSupport]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct IsolatedSignDoubleShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> results;
        public readonly double value;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = Hlsl.Sign(this.value);
        }
    }
}
