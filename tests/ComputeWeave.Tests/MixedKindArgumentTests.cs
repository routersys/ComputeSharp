using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable CS0649, CS8618

namespace ComputeWeave.Tests;

/// <summary>
/// The value a shader computes for a call that mixes a signed and an unsigned integer. C# converts both to
/// the floating point overload, and the shader compiler resolves the call again over the types before the
/// conversion, where the unsigned one wins.
/// </summary>
/// <remarks>
/// Each slot is read by hand rather than against an identity: the two readings are far apart, so an equality
/// against the C# value is what separates them. Under the unsigned reading the first slot is 4294967295, the
/// second reads its factor as 4294967295 and wraps at 32 bits, and the third is 4294967295 again.
/// </remarks>
[TestClass]
public partial class MixedKindArgumentTests
{
    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_MixedKindArgumentsMeanWhatCSharpMeans(Device device)
    {
        using ReadWriteBuffer<float> results = device.Get().AllocateReadWriteBuffer<float>(3);

        device.Get().For(1, new MixedKindShader(
            results,
            -1,
            5u,
            new Int2(-1, -1),
            new UInt2(65536, 65536),
            new Bool2(true, true)));

        float[] values = results.ToArray();

        Assert.AreEqual(5f, values[0], 0.0001f, "the maximum was read as unsigned");

        // Each product is -65536 and their sum -131072, which a float holds exactly. Read as unsigned the
        // factor becomes 4294967295 and the sum wraps at 32 bits, so the two readings are nowhere near
        Assert.AreEqual(-131072f, values[1], 0.0001f, "the dot product was read as unsigned");

        Assert.AreEqual(-1f, values[2], 0.0001f, "the chosen value was read as unsigned");
    }

    // The three shapes an argument reaches: the plain mapping, a product wide enough to wrap, and a named
    // intrinsic, which the rewriting lowers to a construct of its own before the plain mapping is reached
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct MixedKindShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float> results;

        public readonly int negative;

        public readonly uint positive;

        public readonly Int2 signed;

        public readonly UInt2 unsigned;

        public readonly Bool2 mask;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = Hlsl.Max(this.negative, this.positive);
            this.results[1] = Hlsl.Dot(this.signed, this.unsigned);
            this.results[2] = Hlsl.Select(this.mask, this.signed, this.unsigned).X;
        }
    }
}
