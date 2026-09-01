using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests;

[TestClass]
public partial class PredicateKindExpansionTests
{
    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_AllDoubleShapes(Device device)
    {
        if (!device.Get().IsDoublePrecisionSupportAvailable())
        {
            Assert.Inconclusive();
        }

        int[] expected = [1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0];

        using ReadWriteBuffer<int> results = device.Get().AllocateReadWriteBuffer<int>(expected.Length);

        device.Get().For(1, new AllDoubleShapesShader(results, 2.0, 0.0));

        int[] values = results.ToArray();

        for (int i = 0; i < values.Length; i++)
        {
            Assert.AreEqual(expected[i], values[i], $"slot {i}");
        }
    }

    // Every Double shape All now declares, two slots each. The values arrive through the
    // constant buffer so the call cannot fold to a constant, and each shape is read once
    // where every component is non-zero and once where the last component is zero
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [RequiresDoublePrecisionSupport]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct AllDoubleShapesShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> results;
        public readonly double nonZero;
        public readonly double zero;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = Hlsl.All(this.nonZero) ? 1 : 0;
            this.results[1] = Hlsl.All(this.zero) ? 1 : 0;
            this.results[2] = Hlsl.All(new Double2(this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[3] = Hlsl.All(new Double2(this.nonZero, this.zero)) ? 1 : 0;
            this.results[4] = Hlsl.All(new Double3(this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[5] = Hlsl.All(new Double3(this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
            this.results[6] = Hlsl.All(new Double4(this.nonZero, this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[7] = Hlsl.All(new Double4(this.nonZero, this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
            this.results[8] = Hlsl.All(new Double1x1(this.nonZero)) ? 1 : 0;
            this.results[9] = Hlsl.All(new Double1x1(this.zero)) ? 1 : 0;
            this.results[10] = Hlsl.All(new Double1x2(this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[11] = Hlsl.All(new Double1x2(this.nonZero, this.zero)) ? 1 : 0;
            this.results[12] = Hlsl.All(new Double1x3(this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[13] = Hlsl.All(new Double1x3(this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
            this.results[14] = Hlsl.All(new Double1x4(this.nonZero, this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[15] = Hlsl.All(new Double1x4(this.nonZero, this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
            this.results[16] = Hlsl.All(new Double2x1(this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[17] = Hlsl.All(new Double2x1(this.nonZero, this.zero)) ? 1 : 0;
            this.results[18] = Hlsl.All(new Double2x2(this.nonZero, this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[19] = Hlsl.All(new Double2x2(this.nonZero, this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
            this.results[20] = Hlsl.All(new Double2x3(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[21] = Hlsl.All(new Double2x3(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
            this.results[22] = Hlsl.All(new Double2x4(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[23] = Hlsl.All(new Double2x4(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
            this.results[24] = Hlsl.All(new Double3x1(this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[25] = Hlsl.All(new Double3x1(this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
            this.results[26] = Hlsl.All(new Double3x2(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[27] = Hlsl.All(new Double3x2(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
            this.results[28] = Hlsl.All(new Double3x3(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[29] = Hlsl.All(new Double3x3(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
            this.results[30] = Hlsl.All(new Double3x4(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[31] = Hlsl.All(new Double3x4(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
            this.results[32] = Hlsl.All(new Double4x1(this.nonZero, this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[33] = Hlsl.All(new Double4x1(this.nonZero, this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
            this.results[34] = Hlsl.All(new Double4x2(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[35] = Hlsl.All(new Double4x2(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
            this.results[36] = Hlsl.All(new Double4x3(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[37] = Hlsl.All(new Double4x3(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
            this.results[38] = Hlsl.All(new Double4x4(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[39] = Hlsl.All(new Double4x4(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_AllUIntShapes(Device device)
    {
        int[] expected = [1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0];

        using ReadWriteBuffer<int> results = device.Get().AllocateReadWriteBuffer<int>(expected.Length);

        device.Get().For(1, new AllUIntShapesShader(results, 2u, 0u));

        int[] values = results.ToArray();

        for (int i = 0; i < values.Length; i++)
        {
            Assert.AreEqual(expected[i], values[i], $"slot {i}");
        }
    }

    // Every UInt shape All now declares, on the same plan as the double probe
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct AllUIntShapesShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> results;
        public readonly uint nonZero;
        public readonly uint zero;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = Hlsl.All(this.nonZero) ? 1 : 0;
            this.results[1] = Hlsl.All(this.zero) ? 1 : 0;
            this.results[2] = Hlsl.All(new UInt2(this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[3] = Hlsl.All(new UInt2(this.nonZero, this.zero)) ? 1 : 0;
            this.results[4] = Hlsl.All(new UInt3(this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[5] = Hlsl.All(new UInt3(this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
            this.results[6] = Hlsl.All(new UInt4(this.nonZero, this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[7] = Hlsl.All(new UInt4(this.nonZero, this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
            this.results[8] = Hlsl.All(new UInt1x1(this.nonZero)) ? 1 : 0;
            this.results[9] = Hlsl.All(new UInt1x1(this.zero)) ? 1 : 0;
            this.results[10] = Hlsl.All(new UInt1x2(this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[11] = Hlsl.All(new UInt1x2(this.nonZero, this.zero)) ? 1 : 0;
            this.results[12] = Hlsl.All(new UInt1x3(this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[13] = Hlsl.All(new UInt1x3(this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
            this.results[14] = Hlsl.All(new UInt1x4(this.nonZero, this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[15] = Hlsl.All(new UInt1x4(this.nonZero, this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
            this.results[16] = Hlsl.All(new UInt2x1(this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[17] = Hlsl.All(new UInt2x1(this.nonZero, this.zero)) ? 1 : 0;
            this.results[18] = Hlsl.All(new UInt2x2(this.nonZero, this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[19] = Hlsl.All(new UInt2x2(this.nonZero, this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
            this.results[20] = Hlsl.All(new UInt2x3(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[21] = Hlsl.All(new UInt2x3(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
            this.results[22] = Hlsl.All(new UInt2x4(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[23] = Hlsl.All(new UInt2x4(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
            this.results[24] = Hlsl.All(new UInt3x1(this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[25] = Hlsl.All(new UInt3x1(this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
            this.results[26] = Hlsl.All(new UInt3x2(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[27] = Hlsl.All(new UInt3x2(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
            this.results[28] = Hlsl.All(new UInt3x3(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[29] = Hlsl.All(new UInt3x3(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
            this.results[30] = Hlsl.All(new UInt3x4(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[31] = Hlsl.All(new UInt3x4(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
            this.results[32] = Hlsl.All(new UInt4x1(this.nonZero, this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[33] = Hlsl.All(new UInt4x1(this.nonZero, this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
            this.results[34] = Hlsl.All(new UInt4x2(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[35] = Hlsl.All(new UInt4x2(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
            this.results[36] = Hlsl.All(new UInt4x3(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[37] = Hlsl.All(new UInt4x3(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
            this.results[38] = Hlsl.All(new UInt4x4(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero)) ? 1 : 0;
            this.results[39] = Hlsl.All(new UInt4x4(this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.nonZero, this.zero)) ? 1 : 0;
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_AnyDoubleShapes(Device device)
    {
        if (!device.Get().IsDoublePrecisionSupportAvailable())
        {
            Assert.Inconclusive();
        }

        int[] expected = [0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1];

        using ReadWriteBuffer<int> results = device.Get().AllocateReadWriteBuffer<int>(expected.Length);

        device.Get().For(1, new AnyDoubleShapesShader(results, 2.0, 0.0));

        int[] values = results.ToArray();

        for (int i = 0; i < values.Length; i++)
        {
            Assert.AreEqual(expected[i], values[i], $"slot {i}");
        }
    }

    // Every Double shape Any now declares, two slots each. The mirror of the All probe:
    // once where every component is zero and once where only the last one is non-zero
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [RequiresDoublePrecisionSupport]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct AnyDoubleShapesShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> results;
        public readonly double nonZero;
        public readonly double zero;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = Hlsl.Any(this.zero) ? 1 : 0;
            this.results[1] = Hlsl.Any(this.nonZero) ? 1 : 0;
            this.results[2] = Hlsl.Any(new Double2(this.zero, this.zero)) ? 1 : 0;
            this.results[3] = Hlsl.Any(new Double2(this.zero, this.nonZero)) ? 1 : 0;
            this.results[4] = Hlsl.Any(new Double3(this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[5] = Hlsl.Any(new Double3(this.zero, this.zero, this.nonZero)) ? 1 : 0;
            this.results[6] = Hlsl.Any(new Double4(this.zero, this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[7] = Hlsl.Any(new Double4(this.zero, this.zero, this.zero, this.nonZero)) ? 1 : 0;
            this.results[8] = Hlsl.Any(new Double1x1(this.zero)) ? 1 : 0;
            this.results[9] = Hlsl.Any(new Double1x1(this.nonZero)) ? 1 : 0;
            this.results[10] = Hlsl.Any(new Double1x2(this.zero, this.zero)) ? 1 : 0;
            this.results[11] = Hlsl.Any(new Double1x2(this.zero, this.nonZero)) ? 1 : 0;
            this.results[12] = Hlsl.Any(new Double1x3(this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[13] = Hlsl.Any(new Double1x3(this.zero, this.zero, this.nonZero)) ? 1 : 0;
            this.results[14] = Hlsl.Any(new Double1x4(this.zero, this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[15] = Hlsl.Any(new Double1x4(this.zero, this.zero, this.zero, this.nonZero)) ? 1 : 0;
            this.results[16] = Hlsl.Any(new Double2x1(this.zero, this.zero)) ? 1 : 0;
            this.results[17] = Hlsl.Any(new Double2x1(this.zero, this.nonZero)) ? 1 : 0;
            this.results[18] = Hlsl.Any(new Double2x2(this.zero, this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[19] = Hlsl.Any(new Double2x2(this.zero, this.zero, this.zero, this.nonZero)) ? 1 : 0;
            this.results[20] = Hlsl.Any(new Double2x3(this.zero, this.zero, this.zero, this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[21] = Hlsl.Any(new Double2x3(this.zero, this.zero, this.zero, this.zero, this.zero, this.nonZero)) ? 1 : 0;
            this.results[22] = Hlsl.Any(new Double2x4(this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[23] = Hlsl.Any(new Double2x4(this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.nonZero)) ? 1 : 0;
            this.results[24] = Hlsl.Any(new Double3x1(this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[25] = Hlsl.Any(new Double3x1(this.zero, this.zero, this.nonZero)) ? 1 : 0;
            this.results[26] = Hlsl.Any(new Double3x2(this.zero, this.zero, this.zero, this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[27] = Hlsl.Any(new Double3x2(this.zero, this.zero, this.zero, this.zero, this.zero, this.nonZero)) ? 1 : 0;
            this.results[28] = Hlsl.Any(new Double3x3(this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[29] = Hlsl.Any(new Double3x3(this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.nonZero)) ? 1 : 0;
            this.results[30] = Hlsl.Any(new Double3x4(this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[31] = Hlsl.Any(new Double3x4(this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.nonZero)) ? 1 : 0;
            this.results[32] = Hlsl.Any(new Double4x1(this.zero, this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[33] = Hlsl.Any(new Double4x1(this.zero, this.zero, this.zero, this.nonZero)) ? 1 : 0;
            this.results[34] = Hlsl.Any(new Double4x2(this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[35] = Hlsl.Any(new Double4x2(this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.nonZero)) ? 1 : 0;
            this.results[36] = Hlsl.Any(new Double4x3(this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[37] = Hlsl.Any(new Double4x3(this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.nonZero)) ? 1 : 0;
            this.results[38] = Hlsl.Any(new Double4x4(this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[39] = Hlsl.Any(new Double4x4(this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.nonZero)) ? 1 : 0;
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_AnyUIntShapes(Device device)
    {
        int[] expected = [0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1];

        using ReadWriteBuffer<int> results = device.Get().AllocateReadWriteBuffer<int>(expected.Length);

        device.Get().For(1, new AnyUIntShapesShader(results, 2u, 0u));

        int[] values = results.ToArray();

        for (int i = 0; i < values.Length; i++)
        {
            Assert.AreEqual(expected[i], values[i], $"slot {i}");
        }
    }

    // Every UInt shape Any now declares, on the same plan as the double probe
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct AnyUIntShapesShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> results;
        public readonly uint nonZero;
        public readonly uint zero;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = Hlsl.Any(this.zero) ? 1 : 0;
            this.results[1] = Hlsl.Any(this.nonZero) ? 1 : 0;
            this.results[2] = Hlsl.Any(new UInt2(this.zero, this.zero)) ? 1 : 0;
            this.results[3] = Hlsl.Any(new UInt2(this.zero, this.nonZero)) ? 1 : 0;
            this.results[4] = Hlsl.Any(new UInt3(this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[5] = Hlsl.Any(new UInt3(this.zero, this.zero, this.nonZero)) ? 1 : 0;
            this.results[6] = Hlsl.Any(new UInt4(this.zero, this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[7] = Hlsl.Any(new UInt4(this.zero, this.zero, this.zero, this.nonZero)) ? 1 : 0;
            this.results[8] = Hlsl.Any(new UInt1x1(this.zero)) ? 1 : 0;
            this.results[9] = Hlsl.Any(new UInt1x1(this.nonZero)) ? 1 : 0;
            this.results[10] = Hlsl.Any(new UInt1x2(this.zero, this.zero)) ? 1 : 0;
            this.results[11] = Hlsl.Any(new UInt1x2(this.zero, this.nonZero)) ? 1 : 0;
            this.results[12] = Hlsl.Any(new UInt1x3(this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[13] = Hlsl.Any(new UInt1x3(this.zero, this.zero, this.nonZero)) ? 1 : 0;
            this.results[14] = Hlsl.Any(new UInt1x4(this.zero, this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[15] = Hlsl.Any(new UInt1x4(this.zero, this.zero, this.zero, this.nonZero)) ? 1 : 0;
            this.results[16] = Hlsl.Any(new UInt2x1(this.zero, this.zero)) ? 1 : 0;
            this.results[17] = Hlsl.Any(new UInt2x1(this.zero, this.nonZero)) ? 1 : 0;
            this.results[18] = Hlsl.Any(new UInt2x2(this.zero, this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[19] = Hlsl.Any(new UInt2x2(this.zero, this.zero, this.zero, this.nonZero)) ? 1 : 0;
            this.results[20] = Hlsl.Any(new UInt2x3(this.zero, this.zero, this.zero, this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[21] = Hlsl.Any(new UInt2x3(this.zero, this.zero, this.zero, this.zero, this.zero, this.nonZero)) ? 1 : 0;
            this.results[22] = Hlsl.Any(new UInt2x4(this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[23] = Hlsl.Any(new UInt2x4(this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.nonZero)) ? 1 : 0;
            this.results[24] = Hlsl.Any(new UInt3x1(this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[25] = Hlsl.Any(new UInt3x1(this.zero, this.zero, this.nonZero)) ? 1 : 0;
            this.results[26] = Hlsl.Any(new UInt3x2(this.zero, this.zero, this.zero, this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[27] = Hlsl.Any(new UInt3x2(this.zero, this.zero, this.zero, this.zero, this.zero, this.nonZero)) ? 1 : 0;
            this.results[28] = Hlsl.Any(new UInt3x3(this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[29] = Hlsl.Any(new UInt3x3(this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.nonZero)) ? 1 : 0;
            this.results[30] = Hlsl.Any(new UInt3x4(this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[31] = Hlsl.Any(new UInt3x4(this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.nonZero)) ? 1 : 0;
            this.results[32] = Hlsl.Any(new UInt4x1(this.zero, this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[33] = Hlsl.Any(new UInt4x1(this.zero, this.zero, this.zero, this.nonZero)) ? 1 : 0;
            this.results[34] = Hlsl.Any(new UInt4x2(this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[35] = Hlsl.Any(new UInt4x2(this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.nonZero)) ? 1 : 0;
            this.results[36] = Hlsl.Any(new UInt4x3(this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[37] = Hlsl.Any(new UInt4x3(this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.nonZero)) ? 1 : 0;
            this.results[38] = Hlsl.Any(new UInt4x4(this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero)) ? 1 : 0;
            this.results[39] = Hlsl.Any(new UInt4x4(this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.zero, this.nonZero)) ? 1 : 0;
        }
    }
}
