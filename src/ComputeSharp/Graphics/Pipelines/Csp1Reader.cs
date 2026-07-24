using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace ComputeSharp.Graphics.Pipelines;

internal ref struct Csp1Reader(ReadOnlySpan<byte> data)
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly ReadOnlySpan<byte> data = data;

    private int position;

    public readonly int Position => this.position;

    public readonly int Length => this.data.Length;

    public readonly int Remaining => this.data.Length - this.position;

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

    public ReadOnlySpan<byte> ReadBytes(int count)
    {
        return Take(count);
    }

    public string ReadUtf8(int byteLength)
    {
        return StrictUtf8.GetString(Take(byteLength));
    }

    private ReadOnlySpan<byte> Take(int count)
    {
        if ((uint)count > (uint)(this.data.Length - this.position))
        {
            throw new InvalidDataException("The canonical pipeline descriptor payload is malformed.");
        }

        ReadOnlySpan<byte> slice = this.data.Slice(this.position, count);

        this.position += count;

        return slice;
    }
}
