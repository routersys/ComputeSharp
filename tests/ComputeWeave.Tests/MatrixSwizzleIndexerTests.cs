using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static ComputeWeave.MatrixIndex;

#pragma warning disable CS0649, CS8618

namespace ComputeWeave.Tests;

/// <summary>
/// Tests pinning the elements a swizzled matrix indexer reads.
/// </summary>
/// <remarks>
/// A swizzled index is written in the source as a base-1 row and column, and the generator turns it into the
/// base-0 pair HLSL spells. Exchanging the two halves of that pair compiles and reads the transposed element,
/// which is a plausible number and nothing else gives it away. Every matrix here therefore holds nine or four
/// distinct values, and every index picked is off the diagonal: an index on the diagonal reads the same
/// element either way round and would hide exactly the mistake these tests are for.
/// </remarks>
[TestClass]
public partial class MatrixSwizzleIndexerTests
{
    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_SwizzledIndexerReadsTheNamedElements(Device device)
    {
        // rows (1, 2) and (3, 4), so no two elements are equal
        float2x2 square2 = new(1, 2, 3, 4);

        // rows (1, 2, 3), (4, 5, 6) and (7, 8, 9)
        float3x3 square3 = new(1, 2, 3, 4, 5, 6, 7, 8, 9);

        using ReadWriteBuffer<float> results = device.Get().AllocateReadWriteBuffer<float>(9);

        device.Get().For(1, new MatrixSwizzleShader(results, square2, square3));

        float[] values = results.ToArray();

        // the pair off the diagonal of the two by two matrix, in the order it was named
        Assert.AreEqual(2.0f, values[0], "M12 of the two by two matrix");
        Assert.AreEqual(3.0f, values[1], "M21 of the two by two matrix");

        // all four elements of the two by two matrix, in row order
        Assert.AreEqual(1.0f, values[2], "M11 of the two by two matrix");
        Assert.AreEqual(2.0f, values[3], "M12 of the two by two matrix");
        Assert.AreEqual(3.0f, values[4], "M21 of the two by two matrix");
        Assert.AreEqual(4.0f, values[5], "M22 of the two by two matrix");

        // the anti diagonal of the three by three matrix, where a row and column exchange is visible
        Assert.AreEqual(3.0f, values[6], "M13 of the three by three matrix");
        Assert.AreEqual(5.0f, values[7], "M22 of the three by three matrix");
        Assert.AreEqual(7.0f, values[8], "M31 of the three by three matrix");
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct MatrixSwizzleShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float> results;
        public readonly float2x2 square2;
        public readonly float3x3 square3;

        /// <inheritdoc/>
        public void Execute()
        {
            float2 pair = this.square2[M12, M21];
            float4 all = this.square2[M11, M12, M21, M22];
            float3 antiDiagonal = this.square3[M13, M22, M31];

            this.results[0] = pair.X;
            this.results[1] = pair.Y;

            this.results[2] = all.X;
            this.results[3] = all.Y;
            this.results[4] = all.Z;
            this.results[5] = all.W;

            this.results[6] = antiDiagonal.X;
            this.results[7] = antiDiagonal.Y;
            this.results[8] = antiDiagonal.Z;
        }
    }
}
