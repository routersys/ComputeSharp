using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace ComputeSharp.Graphics.Pipelines;

internal ref struct Csp1Reader(ReadOnlySpan<byte> data)
{
    private const uint NullStringMarker = 0xFFFFFFFFu;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly ReadOnlySpan<byte> data = data;

    private int position;

    public readonly int Position => this.position;

    public readonly int Length => this.data.Length;

    public readonly bool IsAtEnd => this.position == this.data.Length;

    public byte ReadByte()
    {
        return Take(1)[0];
    }

    public ushort ReadUInt16()
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(Take(2));
    }

    public uint ReadUInt32()
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(Take(4));
    }

    public int ReadNonNegativeInt32()
    {
        uint value = ReadUInt32();

        if (value > int.MaxValue)
        {
            ThrowInvalidData();
        }

        return (int)value;
    }

    public ReadOnlySpan<byte> ReadBytes(int count)
    {
        return Take(count);
    }

    public string ReadString()
    {
        uint length = ReadUInt32();

        if (length is NullStringMarker or > int.MaxValue)
        {
            ThrowInvalidData();
        }

        string value = StrictUtf8.GetString(Take((int)length));

        if (!value.IsNormalized(NormalizationForm.FormC))
        {
            ThrowInvalidData();
        }

        return value;
    }

    public readonly void EnsureFullyConsumed()
    {
        if (this.position != this.data.Length)
        {
            ThrowInvalidData();
        }
    }

    private ReadOnlySpan<byte> Take(int count)
    {
        if ((uint)count > (uint)(this.data.Length - this.position))
        {
            ThrowInvalidData();
        }

        ReadOnlySpan<byte> slice = this.data.Slice(this.position, count);

        this.position += count;

        return slice;
    }

    private static void ThrowInvalidData()
    {
        throw new InvalidDataException("The canonical pipeline descriptor payload is malformed.");
    }
}
