using System;
using System.Collections.Generic;
using System.Text;

namespace ComputeSharp.SourceGenerators.Helpers;

/// <summary>
/// A writer of the low level primitives of the canonical descriptor binary format.
/// </summary>
internal sealed class Csp1Writer
{
    /// <summary>
    /// The marker written for a null string.
    /// </summary>
    private const uint NullStringMarker = 0xFFFFFFFFu;

    /// <summary>
    /// The written bytes.
    /// </summary>
    private readonly List<byte> bytes = [];

    /// <summary>
    /// Gets the number of written bytes.
    /// </summary>
    public int Length => this.bytes.Count;

    /// <summary>
    /// Writes a single byte.
    /// </summary>
    /// <param name="value">The value to write.</param>
    public void WriteByte(byte value)
    {
        this.bytes.Add(value);
    }

    /// <summary>
    /// Writes a boolean as a single byte.
    /// </summary>
    /// <param name="value">The value to write.</param>
    public void WriteBoolean(bool value)
    {
        this.bytes.Add(value ? (byte)1 : (byte)0);
    }

    /// <summary>
    /// Writes an unsigned 16 bit integer in little-endian order.
    /// </summary>
    /// <param name="value">The value to write.</param>
    public void WriteUInt16(ushort value)
    {
        this.bytes.Add((byte)(value & 0xFF));
        this.bytes.Add((byte)((value >> 8) & 0xFF));
    }

    /// <summary>
    /// Writes an unsigned 32 bit integer in little-endian order.
    /// </summary>
    /// <param name="value">The value to write.</param>
    public void WriteUInt32(uint value)
    {
        this.bytes.Add((byte)(value & 0xFF));
        this.bytes.Add((byte)((value >> 8) & 0xFF));
        this.bytes.Add((byte)((value >> 16) & 0xFF));
        this.bytes.Add((byte)((value >> 24) & 0xFF));
    }

    /// <summary>
    /// Writes a signed 32 bit integer in little-endian order.
    /// </summary>
    /// <param name="value">The value to write.</param>
    public void WriteInt32(int value)
    {
        WriteUInt32((uint)value);
    }

    /// <summary>
    /// Writes a normalized UTF-8 string with its byte length.
    /// </summary>
    /// <param name="value">The value to write.</param>
    public void WriteString(string? value)
    {
        if (value is null)
        {
            WriteUInt32(NullStringMarker);

            return;
        }

        string normalizedValue = value.IsNormalized(NormalizationForm.FormC)
            ? value
            : value.Normalize(NormalizationForm.FormC);

        byte[] encoded = Encoding.UTF8.GetBytes(normalizedValue);

        WriteUInt32((uint)encoded.Length);

        this.bytes.AddRange(encoded);
    }

    /// <summary>
    /// Writes an enum value as a single byte.
    /// </summary>
    /// <typeparam name="T">The type of the enum value.</typeparam>
    /// <param name="value">The value to write.</param>
    public void WriteEnumByte<T>(T value)
        where T : struct, Enum
    {
        this.bytes.Add(Convert.ToByte(value));
    }

    /// <summary>
    /// Gets the written bytes.
    /// </summary>
    /// <returns>The written bytes.</returns>
    public byte[] ToArray()
    {
        return [.. this.bytes];
    }
}
