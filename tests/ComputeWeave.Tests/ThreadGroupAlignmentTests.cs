using System;
using ComputeWeave.Descriptors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable CS0649, CS8618

namespace ComputeWeave.Tests;

[TestClass]
public partial class ThreadGroupAlignmentTests
{
    /// <summary>
    /// The extent is rounded up to the thread count of the axis it is asked for.
    /// </summary>
    /// <remarks>
    /// The thread group of the shader holds a different number of threads on each axis, so an answer that
    /// read the count of another axis would be a different number rather than the same one. One of the three
    /// is not a power of two, which the masking form of rounding gets wrong: on that axis it answers with 10
    /// rather than 12. A group whose every axis is a power of two would take that form as well.
    /// </remarks>
    [TestMethod]
    public void Verify_EachAxisIsRoundedToItsOwnThreadCount()
    {
        Assert.AreEqual(10, ThreadGroupAlignment.AlignX<AxisGroupHandoffShader>(9));
        Assert.AreEqual(12, ThreadGroupAlignment.AlignY<AxisGroupHandoffShader>(9));
        Assert.AreEqual(16, ThreadGroupAlignment.AlignZ<AxisGroupHandoffShader>(9));
    }

    /// <summary>
    /// An extent that already holds whole thread groups is answered with as it is.
    /// </summary>
    [TestMethod]
    public void Verify_AWholeNumberOfGroupsIsLeftAlone()
    {
        Assert.AreEqual(10, ThreadGroupAlignment.AlignX<AxisGroupHandoffShader>(10));
        Assert.AreEqual(12, ThreadGroupAlignment.AlignY<AxisGroupHandoffShader>(12));
        Assert.AreEqual(16, ThreadGroupAlignment.AlignZ<AxisGroupHandoffShader>(16));
    }

    /// <summary>
    /// A shader that does not wait for its whole thread group is answered with the extent as it is.
    /// </summary>
    /// <remarks>
    /// The two shaders hold the same number of threads on every axis and differ only in the barrier they
    /// reach, so this row and the one above disagree on nothing but the requirement itself. Without it,
    /// rounding every extent would answer the rows above just as well, and a shader with no need of whole
    /// groups would be dispatched over threads it never asked for.
    /// </remarks>
    [TestMethod]
    public void Verify_AShaderThatDoesNotWaitForItsGroupIsLeftAlone()
    {
        static bool WaitsForItsGroup<T>()
            where T : struct, IComputeShaderDescriptor<T>
        {
            return T.RequiresFullThreadGroups;
        }

        Assert.IsTrue(WaitsForItsGroup<AxisGroupHandoffShader>());
        Assert.IsFalse(WaitsForItsGroup<AxisPerThreadShader>());

        Assert.AreEqual(9, ThreadGroupAlignment.AlignX<AxisPerThreadShader>(9));
        Assert.AreEqual(9, ThreadGroupAlignment.AlignY<AxisPerThreadShader>(9));
        Assert.AreEqual(9, ThreadGroupAlignment.AlignZ<AxisPerThreadShader>(9));
    }

    /// <summary>
    /// An extent that is not greater than zero is rejected for the argument it was given as.
    /// </summary>
    /// <remarks>
    /// Rounding a number that is not greater than zero would answer with a positive extent the caller never
    /// asked for, so it is refused where the dispatch refuses it, and for the same argument.
    /// </remarks>
    [TestMethod]
    public void Verify_AnExtentThatIsNotGreaterThanZeroIsRejected()
    {
        ArgumentOutOfRangeException onX = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ThreadGroupAlignment.AlignX<AxisGroupHandoffShader>(0));

        ArgumentOutOfRangeException onY = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ThreadGroupAlignment.AlignY<AxisGroupHandoffShader>(-1));

        ArgumentOutOfRangeException onZ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ThreadGroupAlignment.AlignZ<AxisGroupHandoffShader>(-9));

        Assert.AreEqual("x", onX.ParamName);
        Assert.AreEqual("y", onY.ParamName);
        Assert.AreEqual("z", onZ.ParamName);
    }

    /// <summary>
    /// An extent whose rounded value does not fit an <see cref="int"/> is rejected rather than wrapped.
    /// </summary>
    [TestMethod]
    public void Verify_AnExtentThatCannotBeRoundedIsRejected()
    {
        _ = Assert.ThrowsExactly<OverflowException>(
            () => ThreadGroupAlignment.AlignX<AxisGroupHandoffShader>(int.MaxValue));
    }

    [AutoConstructor]
    [ThreadGroupSize(2, 6, 8)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct AxisGroupHandoffShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;

        [GroupShared(96)]
        private static readonly int[] cache;

        /// <inheritdoc/>
        public void Execute()
        {
            cache[GroupIds.Index] = ThreadIds.X;

            Hlsl.GroupMemoryBarrierWithGroupSync();

            this.buffer[ThreadIds.X] = cache[95 - GroupIds.Index];
        }
    }

    [AutoConstructor]
    [ThreadGroupSize(2, 6, 8)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct AxisPerThreadShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;

        [GroupShared(96)]
        private static readonly int[] cache;

        /// <inheritdoc/>
        public void Execute()
        {
            cache[GroupIds.Index] = ThreadIds.X;

            Hlsl.GroupMemoryBarrier();

            this.buffer[ThreadIds.X] = cache[GroupIds.Index];
        }
    }
}