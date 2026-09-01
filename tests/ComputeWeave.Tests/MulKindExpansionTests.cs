using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests;

[TestClass]
public partial class MulKindExpansionTests
{
    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_MulUIntAllShapes(Device device)
    {
        using ReadWriteBuffer<uint2> results = device.Get().AllocateReadWriteBuffer<uint2>(130);

        device.Get().For(1, new MulUIntAllShapesShader(results, 2u));

        uint2[] pairs = results.ToArray();

        for (int i = 0; i < pairs.Length; i++)
        {
            Assert.AreEqual(pairs[i].Y, pairs[i].X, $"slot {i}");
        }
    }

    // Every one of the 130 UInt shapes Mul now declares, one slot each. The operand value
    // arrives through the constant buffer so the call cannot fold to a constant. Both operands
    // carry it, so the result is that value squared times the dimension the two shapes contract
    // over, which is one wherever a scalar is involved
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct MulUIntAllShapesShader : IComputeShader
    {
        public readonly ReadWriteBuffer<uint2> results;
        public readonly uint value;

        /// <inheritdoc/>
        public void Execute()
        {
            {
                uint r = Hlsl.Mul(this.value, this.value);

                this.results[0] = new uint2(r, this.value * this.value);
            }

            {
                UInt2 r = Hlsl.Mul(this.value, new UInt2(this.value, this.value));

                this.results[1] = new uint2(r.Y, this.value * this.value);
            }

            {
                UInt3 r = Hlsl.Mul(this.value, new UInt3(this.value, this.value, this.value));

                this.results[2] = new uint2(r.Z, this.value * this.value);
            }

            {
                UInt4 r = Hlsl.Mul(this.value, new UInt4(this.value, this.value, this.value, this.value));

                this.results[3] = new uint2(r.W, this.value * this.value);
            }

            {
                UInt1x1 r = Hlsl.Mul(this.value, new UInt1x1(this.value));

                this.results[4] = new uint2(r.M11, this.value * this.value);
            }

            {
                UInt1x2 r = Hlsl.Mul(this.value, new UInt1x2(this.value, this.value));

                this.results[5] = new uint2(r.M12, this.value * this.value);
            }

            {
                UInt1x3 r = Hlsl.Mul(this.value, new UInt1x3(this.value, this.value, this.value));

                this.results[6] = new uint2(r.M13, this.value * this.value);
            }

            {
                UInt1x4 r = Hlsl.Mul(this.value, new UInt1x4(this.value, this.value, this.value, this.value));

                this.results[7] = new uint2(r.M14, this.value * this.value);
            }

            {
                UInt2x1 r = Hlsl.Mul(this.value, new UInt2x1(this.value, this.value));

                this.results[8] = new uint2(r.M21, this.value * this.value);
            }

            {
                UInt2x2 r = Hlsl.Mul(this.value, new UInt2x2(this.value, this.value, this.value, this.value));

                this.results[9] = new uint2(r.M22, this.value * this.value);
            }

            {
                UInt2x3 r = Hlsl.Mul(this.value, new UInt2x3(this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[10] = new uint2(r.M23, this.value * this.value);
            }

            {
                UInt2x4 r = Hlsl.Mul(this.value, new UInt2x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[11] = new uint2(r.M24, this.value * this.value);
            }

            {
                UInt3x1 r = Hlsl.Mul(this.value, new UInt3x1(this.value, this.value, this.value));

                this.results[12] = new uint2(r.M31, this.value * this.value);
            }

            {
                UInt3x2 r = Hlsl.Mul(this.value, new UInt3x2(this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[13] = new uint2(r.M32, this.value * this.value);
            }

            {
                UInt3x3 r = Hlsl.Mul(this.value, new UInt3x3(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[14] = new uint2(r.M33, this.value * this.value);
            }

            {
                UInt3x4 r = Hlsl.Mul(this.value, new UInt3x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[15] = new uint2(r.M34, this.value * this.value);
            }

            {
                UInt4x1 r = Hlsl.Mul(this.value, new UInt4x1(this.value, this.value, this.value, this.value));

                this.results[16] = new uint2(r.M41, this.value * this.value);
            }

            {
                UInt4x2 r = Hlsl.Mul(this.value, new UInt4x2(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[17] = new uint2(r.M42, this.value * this.value);
            }

            {
                UInt4x3 r = Hlsl.Mul(this.value, new UInt4x3(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[18] = new uint2(r.M43, this.value * this.value);
            }

            {
                UInt4x4 r = Hlsl.Mul(this.value, new UInt4x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[19] = new uint2(r.M44, this.value * this.value);
            }

            {
                UInt2 r = Hlsl.Mul(new UInt2(this.value, this.value), this.value);

                this.results[20] = new uint2(r.Y, this.value * this.value);
            }

            {
                UInt3 r = Hlsl.Mul(new UInt3(this.value, this.value, this.value), this.value);

                this.results[21] = new uint2(r.Z, this.value * this.value);
            }

            {
                UInt4 r = Hlsl.Mul(new UInt4(this.value, this.value, this.value, this.value), this.value);

                this.results[22] = new uint2(r.W, this.value * this.value);
            }

            {
                uint r = Hlsl.Mul(new UInt2(this.value, this.value), new UInt2(this.value, this.value));

                this.results[23] = new uint2(r, this.value * this.value * 2);
            }

            {
                uint r = Hlsl.Mul(new UInt3(this.value, this.value, this.value), new UInt3(this.value, this.value, this.value));

                this.results[24] = new uint2(r, this.value * this.value * 3);
            }

            {
                uint r = Hlsl.Mul(new UInt4(this.value, this.value, this.value, this.value), new UInt4(this.value, this.value, this.value, this.value));

                this.results[25] = new uint2(r, this.value * this.value * 4);
            }

            {
                uint r = Hlsl.Mul(new UInt2(this.value, this.value), new UInt2x1(this.value, this.value));

                this.results[26] = new uint2(r, this.value * this.value * 2);
            }

            {
                UInt2 r = Hlsl.Mul(new UInt2(this.value, this.value), new UInt2x2(this.value, this.value, this.value, this.value));

                this.results[27] = new uint2(r.Y, this.value * this.value * 2);
            }

            {
                UInt3 r = Hlsl.Mul(new UInt2(this.value, this.value), new UInt2x3(this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[28] = new uint2(r.Z, this.value * this.value * 2);
            }

            {
                UInt4 r = Hlsl.Mul(new UInt2(this.value, this.value), new UInt2x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[29] = new uint2(r.W, this.value * this.value * 2);
            }

            {
                uint r = Hlsl.Mul(new UInt3(this.value, this.value, this.value), new UInt3x1(this.value, this.value, this.value));

                this.results[30] = new uint2(r, this.value * this.value * 3);
            }

            {
                UInt2 r = Hlsl.Mul(new UInt3(this.value, this.value, this.value), new UInt3x2(this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[31] = new uint2(r.Y, this.value * this.value * 3);
            }

            {
                UInt3 r = Hlsl.Mul(new UInt3(this.value, this.value, this.value), new UInt3x3(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[32] = new uint2(r.Z, this.value * this.value * 3);
            }

            {
                UInt4 r = Hlsl.Mul(new UInt3(this.value, this.value, this.value), new UInt3x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[33] = new uint2(r.W, this.value * this.value * 3);
            }

            {
                uint r = Hlsl.Mul(new UInt4(this.value, this.value, this.value, this.value), new UInt4x1(this.value, this.value, this.value, this.value));

                this.results[34] = new uint2(r, this.value * this.value * 4);
            }

            {
                UInt2 r = Hlsl.Mul(new UInt4(this.value, this.value, this.value, this.value), new UInt4x2(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[35] = new uint2(r.Y, this.value * this.value * 4);
            }

            {
                UInt3 r = Hlsl.Mul(new UInt4(this.value, this.value, this.value, this.value), new UInt4x3(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[36] = new uint2(r.Z, this.value * this.value * 4);
            }

            {
                UInt4 r = Hlsl.Mul(new UInt4(this.value, this.value, this.value, this.value), new UInt4x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[37] = new uint2(r.W, this.value * this.value * 4);
            }

            {
                UInt1x1 r = Hlsl.Mul(new UInt1x1(this.value), this.value);

                this.results[38] = new uint2(r.M11, this.value * this.value);
            }

            {
                UInt1x2 r = Hlsl.Mul(new UInt1x2(this.value, this.value), this.value);

                this.results[39] = new uint2(r.M12, this.value * this.value);
            }

            {
                UInt1x3 r = Hlsl.Mul(new UInt1x3(this.value, this.value, this.value), this.value);

                this.results[40] = new uint2(r.M13, this.value * this.value);
            }

            {
                UInt1x4 r = Hlsl.Mul(new UInt1x4(this.value, this.value, this.value, this.value), this.value);

                this.results[41] = new uint2(r.M14, this.value * this.value);
            }

            {
                UInt2x1 r = Hlsl.Mul(new UInt2x1(this.value, this.value), this.value);

                this.results[42] = new uint2(r.M21, this.value * this.value);
            }

            {
                UInt2x2 r = Hlsl.Mul(new UInt2x2(this.value, this.value, this.value, this.value), this.value);

                this.results[43] = new uint2(r.M22, this.value * this.value);
            }

            {
                UInt2x3 r = Hlsl.Mul(new UInt2x3(this.value, this.value, this.value, this.value, this.value, this.value), this.value);

                this.results[44] = new uint2(r.M23, this.value * this.value);
            }

            {
                UInt2x4 r = Hlsl.Mul(new UInt2x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), this.value);

                this.results[45] = new uint2(r.M24, this.value * this.value);
            }

            {
                UInt3x1 r = Hlsl.Mul(new UInt3x1(this.value, this.value, this.value), this.value);

                this.results[46] = new uint2(r.M31, this.value * this.value);
            }

            {
                UInt3x2 r = Hlsl.Mul(new UInt3x2(this.value, this.value, this.value, this.value, this.value, this.value), this.value);

                this.results[47] = new uint2(r.M32, this.value * this.value);
            }

            {
                UInt3x3 r = Hlsl.Mul(new UInt3x3(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), this.value);

                this.results[48] = new uint2(r.M33, this.value * this.value);
            }

            {
                UInt3x4 r = Hlsl.Mul(new UInt3x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), this.value);

                this.results[49] = new uint2(r.M34, this.value * this.value);
            }

            {
                UInt4x1 r = Hlsl.Mul(new UInt4x1(this.value, this.value, this.value, this.value), this.value);

                this.results[50] = new uint2(r.M41, this.value * this.value);
            }

            {
                UInt4x2 r = Hlsl.Mul(new UInt4x2(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), this.value);

                this.results[51] = new uint2(r.M42, this.value * this.value);
            }

            {
                UInt4x3 r = Hlsl.Mul(new UInt4x3(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), this.value);

                this.results[52] = new uint2(r.M43, this.value * this.value);
            }

            {
                UInt4x4 r = Hlsl.Mul(new UInt4x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), this.value);

                this.results[53] = new uint2(r.M44, this.value * this.value);
            }

            {
                uint r = Hlsl.Mul(new UInt1x2(this.value, this.value), new UInt2(this.value, this.value));

                this.results[54] = new uint2(r, this.value * this.value * 2);
            }

            {
                uint r = Hlsl.Mul(new UInt1x3(this.value, this.value, this.value), new UInt3(this.value, this.value, this.value));

                this.results[55] = new uint2(r, this.value * this.value * 3);
            }

            {
                uint r = Hlsl.Mul(new UInt1x4(this.value, this.value, this.value, this.value), new UInt4(this.value, this.value, this.value, this.value));

                this.results[56] = new uint2(r, this.value * this.value * 4);
            }

            {
                UInt2 r = Hlsl.Mul(new UInt2x2(this.value, this.value, this.value, this.value), new UInt2(this.value, this.value));

                this.results[57] = new uint2(r.Y, this.value * this.value * 2);
            }

            {
                UInt2 r = Hlsl.Mul(new UInt2x3(this.value, this.value, this.value, this.value, this.value, this.value), new UInt3(this.value, this.value, this.value));

                this.results[58] = new uint2(r.Y, this.value * this.value * 3);
            }

            {
                UInt2 r = Hlsl.Mul(new UInt2x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt4(this.value, this.value, this.value, this.value));

                this.results[59] = new uint2(r.Y, this.value * this.value * 4);
            }

            {
                UInt3 r = Hlsl.Mul(new UInt3x2(this.value, this.value, this.value, this.value, this.value, this.value), new UInt2(this.value, this.value));

                this.results[60] = new uint2(r.Z, this.value * this.value * 2);
            }

            {
                UInt3 r = Hlsl.Mul(new UInt3x3(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt3(this.value, this.value, this.value));

                this.results[61] = new uint2(r.Z, this.value * this.value * 3);
            }

            {
                UInt3 r = Hlsl.Mul(new UInt3x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt4(this.value, this.value, this.value, this.value));

                this.results[62] = new uint2(r.Z, this.value * this.value * 4);
            }

            {
                UInt4 r = Hlsl.Mul(new UInt4x2(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt2(this.value, this.value));

                this.results[63] = new uint2(r.W, this.value * this.value * 2);
            }

            {
                UInt4 r = Hlsl.Mul(new UInt4x3(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt3(this.value, this.value, this.value));

                this.results[64] = new uint2(r.W, this.value * this.value * 3);
            }

            {
                UInt4 r = Hlsl.Mul(new UInt4x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt4(this.value, this.value, this.value, this.value));

                this.results[65] = new uint2(r.W, this.value * this.value * 4);
            }

            {
                UInt1x1 r = Hlsl.Mul(new UInt1x1(this.value), new UInt1x1(this.value));

                this.results[66] = new uint2(r.M11, this.value * this.value);
            }

            {
                UInt1x2 r = Hlsl.Mul(new UInt1x1(this.value), new UInt1x2(this.value, this.value));

                this.results[67] = new uint2(r.M12, this.value * this.value);
            }

            {
                UInt1x3 r = Hlsl.Mul(new UInt1x1(this.value), new UInt1x3(this.value, this.value, this.value));

                this.results[68] = new uint2(r.M13, this.value * this.value);
            }

            {
                UInt1x4 r = Hlsl.Mul(new UInt1x1(this.value), new UInt1x4(this.value, this.value, this.value, this.value));

                this.results[69] = new uint2(r.M14, this.value * this.value);
            }

            {
                UInt1x1 r = Hlsl.Mul(new UInt1x2(this.value, this.value), new UInt2x1(this.value, this.value));

                this.results[70] = new uint2(r.M11, this.value * this.value * 2);
            }

            {
                UInt1x2 r = Hlsl.Mul(new UInt1x2(this.value, this.value), new UInt2x2(this.value, this.value, this.value, this.value));

                this.results[71] = new uint2(r.M12, this.value * this.value * 2);
            }

            {
                UInt1x3 r = Hlsl.Mul(new UInt1x2(this.value, this.value), new UInt2x3(this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[72] = new uint2(r.M13, this.value * this.value * 2);
            }

            {
                UInt1x4 r = Hlsl.Mul(new UInt1x2(this.value, this.value), new UInt2x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[73] = new uint2(r.M14, this.value * this.value * 2);
            }

            {
                UInt1x1 r = Hlsl.Mul(new UInt1x3(this.value, this.value, this.value), new UInt3x1(this.value, this.value, this.value));

                this.results[74] = new uint2(r.M11, this.value * this.value * 3);
            }

            {
                UInt1x2 r = Hlsl.Mul(new UInt1x3(this.value, this.value, this.value), new UInt3x2(this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[75] = new uint2(r.M12, this.value * this.value * 3);
            }

            {
                UInt1x3 r = Hlsl.Mul(new UInt1x3(this.value, this.value, this.value), new UInt3x3(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[76] = new uint2(r.M13, this.value * this.value * 3);
            }

            {
                UInt1x4 r = Hlsl.Mul(new UInt1x3(this.value, this.value, this.value), new UInt3x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[77] = new uint2(r.M14, this.value * this.value * 3);
            }

            {
                UInt1x1 r = Hlsl.Mul(new UInt1x4(this.value, this.value, this.value, this.value), new UInt4x1(this.value, this.value, this.value, this.value));

                this.results[78] = new uint2(r.M11, this.value * this.value * 4);
            }

            {
                UInt1x2 r = Hlsl.Mul(new UInt1x4(this.value, this.value, this.value, this.value), new UInt4x2(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[79] = new uint2(r.M12, this.value * this.value * 4);
            }

            {
                UInt1x3 r = Hlsl.Mul(new UInt1x4(this.value, this.value, this.value, this.value), new UInt4x3(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[80] = new uint2(r.M13, this.value * this.value * 4);
            }

            {
                UInt1x4 r = Hlsl.Mul(new UInt1x4(this.value, this.value, this.value, this.value), new UInt4x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[81] = new uint2(r.M14, this.value * this.value * 4);
            }

            {
                UInt2x1 r = Hlsl.Mul(new UInt2x1(this.value, this.value), new UInt1x1(this.value));

                this.results[82] = new uint2(r.M21, this.value * this.value);
            }

            {
                UInt2x2 r = Hlsl.Mul(new UInt2x1(this.value, this.value), new UInt1x2(this.value, this.value));

                this.results[83] = new uint2(r.M22, this.value * this.value);
            }

            {
                UInt2x3 r = Hlsl.Mul(new UInt2x1(this.value, this.value), new UInt1x3(this.value, this.value, this.value));

                this.results[84] = new uint2(r.M23, this.value * this.value);
            }

            {
                UInt2x4 r = Hlsl.Mul(new UInt2x1(this.value, this.value), new UInt1x4(this.value, this.value, this.value, this.value));

                this.results[85] = new uint2(r.M24, this.value * this.value);
            }

            {
                UInt2x1 r = Hlsl.Mul(new UInt2x2(this.value, this.value, this.value, this.value), new UInt2x1(this.value, this.value));

                this.results[86] = new uint2(r.M21, this.value * this.value * 2);
            }

            {
                UInt2x2 r = Hlsl.Mul(new UInt2x2(this.value, this.value, this.value, this.value), new UInt2x2(this.value, this.value, this.value, this.value));

                this.results[87] = new uint2(r.M22, this.value * this.value * 2);
            }

            {
                UInt2x3 r = Hlsl.Mul(new UInt2x2(this.value, this.value, this.value, this.value), new UInt2x3(this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[88] = new uint2(r.M23, this.value * this.value * 2);
            }

            {
                UInt2x4 r = Hlsl.Mul(new UInt2x2(this.value, this.value, this.value, this.value), new UInt2x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[89] = new uint2(r.M24, this.value * this.value * 2);
            }

            {
                UInt2x1 r = Hlsl.Mul(new UInt2x3(this.value, this.value, this.value, this.value, this.value, this.value), new UInt3x1(this.value, this.value, this.value));

                this.results[90] = new uint2(r.M21, this.value * this.value * 3);
            }

            {
                UInt2x2 r = Hlsl.Mul(new UInt2x3(this.value, this.value, this.value, this.value, this.value, this.value), new UInt3x2(this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[91] = new uint2(r.M22, this.value * this.value * 3);
            }

            {
                UInt2x3 r = Hlsl.Mul(new UInt2x3(this.value, this.value, this.value, this.value, this.value, this.value), new UInt3x3(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[92] = new uint2(r.M23, this.value * this.value * 3);
            }

            {
                UInt2x4 r = Hlsl.Mul(new UInt2x3(this.value, this.value, this.value, this.value, this.value, this.value), new UInt3x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[93] = new uint2(r.M24, this.value * this.value * 3);
            }

            {
                UInt2x1 r = Hlsl.Mul(new UInt2x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt4x1(this.value, this.value, this.value, this.value));

                this.results[94] = new uint2(r.M21, this.value * this.value * 4);
            }

            {
                UInt2x2 r = Hlsl.Mul(new UInt2x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt4x2(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[95] = new uint2(r.M22, this.value * this.value * 4);
            }

            {
                UInt2x3 r = Hlsl.Mul(new UInt2x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt4x3(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[96] = new uint2(r.M23, this.value * this.value * 4);
            }

            {
                UInt2x4 r = Hlsl.Mul(new UInt2x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt4x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[97] = new uint2(r.M24, this.value * this.value * 4);
            }

            {
                UInt3x1 r = Hlsl.Mul(new UInt3x1(this.value, this.value, this.value), new UInt1x1(this.value));

                this.results[98] = new uint2(r.M31, this.value * this.value);
            }

            {
                UInt3x2 r = Hlsl.Mul(new UInt3x1(this.value, this.value, this.value), new UInt1x2(this.value, this.value));

                this.results[99] = new uint2(r.M32, this.value * this.value);
            }

            {
                UInt3x3 r = Hlsl.Mul(new UInt3x1(this.value, this.value, this.value), new UInt1x3(this.value, this.value, this.value));

                this.results[100] = new uint2(r.M33, this.value * this.value);
            }

            {
                UInt3x4 r = Hlsl.Mul(new UInt3x1(this.value, this.value, this.value), new UInt1x4(this.value, this.value, this.value, this.value));

                this.results[101] = new uint2(r.M34, this.value * this.value);
            }

            {
                UInt3x1 r = Hlsl.Mul(new UInt3x2(this.value, this.value, this.value, this.value, this.value, this.value), new UInt2x1(this.value, this.value));

                this.results[102] = new uint2(r.M31, this.value * this.value * 2);
            }

            {
                UInt3x2 r = Hlsl.Mul(new UInt3x2(this.value, this.value, this.value, this.value, this.value, this.value), new UInt2x2(this.value, this.value, this.value, this.value));

                this.results[103] = new uint2(r.M32, this.value * this.value * 2);
            }

            {
                UInt3x3 r = Hlsl.Mul(new UInt3x2(this.value, this.value, this.value, this.value, this.value, this.value), new UInt2x3(this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[104] = new uint2(r.M33, this.value * this.value * 2);
            }

            {
                UInt3x4 r = Hlsl.Mul(new UInt3x2(this.value, this.value, this.value, this.value, this.value, this.value), new UInt2x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[105] = new uint2(r.M34, this.value * this.value * 2);
            }

            {
                UInt3x1 r = Hlsl.Mul(new UInt3x3(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt3x1(this.value, this.value, this.value));

                this.results[106] = new uint2(r.M31, this.value * this.value * 3);
            }

            {
                UInt3x2 r = Hlsl.Mul(new UInt3x3(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt3x2(this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[107] = new uint2(r.M32, this.value * this.value * 3);
            }

            {
                UInt3x3 r = Hlsl.Mul(new UInt3x3(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt3x3(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[108] = new uint2(r.M33, this.value * this.value * 3);
            }

            {
                UInt3x4 r = Hlsl.Mul(new UInt3x3(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt3x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[109] = new uint2(r.M34, this.value * this.value * 3);
            }

            {
                UInt3x1 r = Hlsl.Mul(new UInt3x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt4x1(this.value, this.value, this.value, this.value));

                this.results[110] = new uint2(r.M31, this.value * this.value * 4);
            }

            {
                UInt3x2 r = Hlsl.Mul(new UInt3x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt4x2(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[111] = new uint2(r.M32, this.value * this.value * 4);
            }

            {
                UInt3x3 r = Hlsl.Mul(new UInt3x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt4x3(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[112] = new uint2(r.M33, this.value * this.value * 4);
            }

            {
                UInt3x4 r = Hlsl.Mul(new UInt3x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt4x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[113] = new uint2(r.M34, this.value * this.value * 4);
            }

            {
                UInt4x1 r = Hlsl.Mul(new UInt4x1(this.value, this.value, this.value, this.value), new UInt1x1(this.value));

                this.results[114] = new uint2(r.M41, this.value * this.value);
            }

            {
                UInt4x2 r = Hlsl.Mul(new UInt4x1(this.value, this.value, this.value, this.value), new UInt1x2(this.value, this.value));

                this.results[115] = new uint2(r.M42, this.value * this.value);
            }

            {
                UInt4x3 r = Hlsl.Mul(new UInt4x1(this.value, this.value, this.value, this.value), new UInt1x3(this.value, this.value, this.value));

                this.results[116] = new uint2(r.M43, this.value * this.value);
            }

            {
                UInt4x4 r = Hlsl.Mul(new UInt4x1(this.value, this.value, this.value, this.value), new UInt1x4(this.value, this.value, this.value, this.value));

                this.results[117] = new uint2(r.M44, this.value * this.value);
            }

            {
                UInt4x1 r = Hlsl.Mul(new UInt4x2(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt2x1(this.value, this.value));

                this.results[118] = new uint2(r.M41, this.value * this.value * 2);
            }

            {
                UInt4x2 r = Hlsl.Mul(new UInt4x2(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt2x2(this.value, this.value, this.value, this.value));

                this.results[119] = new uint2(r.M42, this.value * this.value * 2);
            }

            {
                UInt4x3 r = Hlsl.Mul(new UInt4x2(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt2x3(this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[120] = new uint2(r.M43, this.value * this.value * 2);
            }

            {
                UInt4x4 r = Hlsl.Mul(new UInt4x2(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt2x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[121] = new uint2(r.M44, this.value * this.value * 2);
            }

            {
                UInt4x1 r = Hlsl.Mul(new UInt4x3(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt3x1(this.value, this.value, this.value));

                this.results[122] = new uint2(r.M41, this.value * this.value * 3);
            }

            {
                UInt4x2 r = Hlsl.Mul(new UInt4x3(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt3x2(this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[123] = new uint2(r.M42, this.value * this.value * 3);
            }

            {
                UInt4x3 r = Hlsl.Mul(new UInt4x3(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt3x3(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[124] = new uint2(r.M43, this.value * this.value * 3);
            }

            {
                UInt4x4 r = Hlsl.Mul(new UInt4x3(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt3x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[125] = new uint2(r.M44, this.value * this.value * 3);
            }

            {
                UInt4x1 r = Hlsl.Mul(new UInt4x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt4x1(this.value, this.value, this.value, this.value));

                this.results[126] = new uint2(r.M41, this.value * this.value * 4);
            }

            {
                UInt4x2 r = Hlsl.Mul(new UInt4x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt4x2(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[127] = new uint2(r.M42, this.value * this.value * 4);
            }

            {
                UInt4x3 r = Hlsl.Mul(new UInt4x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt4x3(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[128] = new uint2(r.M43, this.value * this.value * 4);
            }

            {
                UInt4x4 r = Hlsl.Mul(new UInt4x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value), new UInt4x4(this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value, this.value));

                this.results[129] = new uint2(r.M44, this.value * this.value * 4);
            }
        }
    }
}
