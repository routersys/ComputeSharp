using System;
using ComputeWeave.Descriptors;

namespace ComputeWeave;

/// <summary>
/// Rounds the extent of a dispatch up to whole thread groups, for the shaders that are dispatched that way.
/// </summary>
/// <remarks>
/// <para>
/// A shader that waits for every thread of its thread group can only be dispatched over an extent holding whole
/// groups on every axis, which <see cref="IComputeShaderDescriptor{T}.RequiresFullThreadGroups"/> declares and
/// the dispatch rejects an extent for. These methods answer with the extent to ask for, and leave the extent of
/// every other shader as it is, so a call site can be written once and stays right whether or not the shader it
/// names waits for its group.
/// </para>
/// <para>
/// The entry point runs the body for every thread inside the extent that was asked for, so the threads the
/// rounding adds run it as well, over coordinates past the extent being worked on. A shader dispatched over a
/// rounded extent has to hold for those coordinates, by carrying the extent it works on itself or by writing
/// only through resources that drop a write outside them.
/// </para>
/// <para>
/// A pixel shader takes its extent from the texture it is run over, which no caller can round up, so a texture
/// whose sides are not whole groups has to be allocated at the rounded size instead.
/// </para>
/// </remarks>
public static class ThreadGroupAlignment
{
    /// <summary>
    /// Gets the extent to dispatch <typeparamref name="T"/> over on the X axis to cover a number of iterations.
    /// </summary>
    /// <typeparam name="T">The type of compute shader the extent is for.</typeparam>
    /// <param name="x">The number of iterations to cover on the X axis.</param>
    /// <returns>
    /// <paramref name="x"/> rounded up to whole thread groups, or <paramref name="x"/> as it is when
    /// <typeparamref name="T"/> is dispatched over an extent of any size.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="x"/> is not greater than zero.</exception>
    /// <exception cref="OverflowException">Thrown when the rounded extent does not fit an <see cref="int"/>.</exception>
    public static int AlignX<T>(int x)
        where T : struct, IComputeShaderDescriptor<T>
    {
        default(ArgumentOutOfRangeException).ThrowIfNegativeOrZero(x);

        return Align(x, T.ThreadsX, T.RequiresFullThreadGroups);
    }

    /// <summary>
    /// Gets the extent to dispatch <typeparamref name="T"/> over on the Y axis to cover a number of iterations.
    /// </summary>
    /// <typeparam name="T">The type of compute shader the extent is for.</typeparam>
    /// <param name="y">The number of iterations to cover on the Y axis.</param>
    /// <returns>
    /// <paramref name="y"/> rounded up to whole thread groups, or <paramref name="y"/> as it is when
    /// <typeparamref name="T"/> is dispatched over an extent of any size.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="y"/> is not greater than zero.</exception>
    /// <exception cref="OverflowException">Thrown when the rounded extent does not fit an <see cref="int"/>.</exception>
    public static int AlignY<T>(int y)
        where T : struct, IComputeShaderDescriptor<T>
    {
        default(ArgumentOutOfRangeException).ThrowIfNegativeOrZero(y);

        return Align(y, T.ThreadsY, T.RequiresFullThreadGroups);
    }

    /// <summary>
    /// Gets the extent to dispatch <typeparamref name="T"/> over on the Z axis to cover a number of iterations.
    /// </summary>
    /// <typeparam name="T">The type of compute shader the extent is for.</typeparam>
    /// <param name="z">The number of iterations to cover on the Z axis.</param>
    /// <returns>
    /// <paramref name="z"/> rounded up to whole thread groups, or <paramref name="z"/> as it is when
    /// <typeparamref name="T"/> is dispatched over an extent of any size.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="z"/> is not greater than zero.</exception>
    /// <exception cref="OverflowException">Thrown when the rounded extent does not fit an <see cref="int"/>.</exception>
    public static int AlignZ<T>(int z)
        where T : struct, IComputeShaderDescriptor<T>
    {
        default(ArgumentOutOfRangeException).ThrowIfNegativeOrZero(z);

        return Align(z, T.ThreadsZ, T.RequiresFullThreadGroups);
    }

    /// <summary>
    /// Rounds an extent up to whole thread groups, when the shader it is for is dispatched that way.
    /// </summary>
    /// <param name="extent">The number of iterations to cover on the axis.</param>
    /// <param name="threads">The number of threads a thread group holds on the axis.</param>
    /// <param name="isRequired">Whether the shader needs the extent to hold whole thread groups.</param>
    /// <returns>The extent to dispatch over.</returns>
    private static int Align(int extent, int threads, bool isRequired)
    {
        if (!isRequired)
        {
            return extent;
        }

        int remainder = extent % threads;

        return remainder == 0 ? extent : checked(extent + (threads - remainder));
    }
}