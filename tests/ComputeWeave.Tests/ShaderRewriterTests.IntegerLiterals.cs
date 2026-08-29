using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests;

/// <summary>
/// Tests that an integer literal reaches HLSL as its value rather than as the spelling the author used.
/// </summary>
/// <remarks>
/// <para>
/// A float or a double literal is written from the token value, so a digit separator never reaches the
/// shader compiler. An integer literal used to be written as it stands, which left the generated HLSL
/// depending on which spellings the compiler in front of it happens to accept: a digit separator is
/// rejected by both compilers, and a binary literal is accepted by DXC and rejected by FXC.
/// </para>
/// <para>
/// The unsigned case carries a value no signed literal can hold, so a suffix dropped on the way out is
/// visible as a wrong number rather than as a compiler error.
/// </para>
/// </remarks>
public partial class ShaderRewriterTests
{
    [CombinatorialTestMethod]
    [AllDevices]
    public void IntegerLiterals(Device device)
    {
        using ReadWriteBuffer<uint> buffer = device.Get().AllocateReadWriteBuffer<uint>(7);

        device.Get().For(1, new IntegerLiteralShader(buffer));

        uint[] results = buffer.ToArray();

        Assert.AreEqual(1000u, results[0], "a decimal literal with a digit separator");
        Assert.AreEqual(31u, results[1], "a hexadecimal literal with a digit separator");
        Assert.AreEqual(2u, results[2], "a binary literal with a digit separator");
        Assert.AreEqual(10u, results[3], "a binary literal");
        Assert.AreEqual(31u, results[4], "a hexadecimal literal");
        Assert.AreEqual(1u, results[5], "an unsigned literal");
        Assert.AreEqual(4294967295u, results[6], "an unsigned literal past the signed range");
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct IntegerLiteralShader : IComputeShader
    {
        public readonly ReadWriteBuffer<uint> buffer;

        public void Execute()
        {
            int separatedDecimal = 1_000;
            int separatedHexadecimal = 0x1_F;
            int separatedBinary = 0b1_0;
            int binary = 0b1010;
            int hexadecimal = 0x1F;
            uint unsignedValue = 1u;
            uint unsignedLarge = 4_294_967_295u;

            this.buffer[0] = (uint)separatedDecimal;
            this.buffer[1] = (uint)separatedHexadecimal;
            this.buffer[2] = (uint)separatedBinary;
            this.buffer[3] = (uint)binary;
            this.buffer[4] = (uint)hexadecimal;
            this.buffer[5] = unsignedValue;
            this.buffer[6] = unsignedLarge;
        }
    }
}
