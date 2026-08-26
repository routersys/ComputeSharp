using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ComputeWeave.D2D1;
using ComputeWeave.D2D1.Interop;

[assembly: SupportedOSPlatform("windows6.1")]

// Load the precompiled bytecode. The shader is compiled while this project is built, which only
// happens if the source generator travelled in the package, and the result is pinned memory read
// from the PE image rather than an array.
ReadOnlyMemory<byte> precompiledBytecode = D2D1PixelShader.LoadBytecode<Desaturate>();

Trace.Assert(MemoryMarshal.TryGetMemoryManager(precompiledBytecode, out MemoryManager<byte>? _));
Trace.Assert(!MemoryMarshal.TryGetArray(precompiledBytecode, out ArraySegment<byte> _));

// Ask for another profile, which rejects the precompiled bytecode and compiles the generated HLSL
// through FXC. That compiler ships with Windows, as the package does not carry one.
ReadOnlyMemory<byte> compiledBytecode = D2D1PixelShader.LoadBytecode<Desaturate>(D2D1ShaderProfile.PixelShader40);

Trace.Assert(!MemoryMarshal.TryGetMemoryManager(compiledBytecode, out MemoryManager<byte>? _));
Trace.Assert(MemoryMarshal.TryGetArray(compiledBytecode, out ArraySegment<byte> compiledSegment));
Trace.Assert(compiledSegment.Count > 0);

// Validate the descriptor the generator produced for the shader
Trace.Assert(D2D1PixelShader.GetInputCount<Desaturate>() == 1);
Trace.Assert(D2D1PixelShader.GetConstantBufferSize<Desaturate>() == sizeof(float));

// Validate that the constant buffer round trips through the generated marshalling code
Desaturate shader = new(0.75f);
ReadOnlyMemory<byte> constantBuffer = D2D1PixelShader.GetConstantBuffer(in shader);

Trace.Assert(constantBuffer.Length == sizeof(float));
Trace.Assert(D2D1PixelShader.CreateFromConstantBuffer<Desaturate>(constantBuffer.Span).Amount == 0.75f);

/// <summary>
/// A sample pixel shader that is precompiled.
/// </summary>
/// <param name="amount">The amount to scale the input color by.</param>
[D2DInputCount(1)]
[D2DInputSimple(0)]
[D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
[D2DGeneratedPixelShaderDescriptor]
internal readonly partial struct Desaturate(float amount) : ID2D1PixelShader
{
    /// <summary>
    /// The amount to scale the input color by.
    /// </summary>
    public readonly float Amount = amount;

    /// <inheritdoc/>
    public float4 Execute()
    {
        float4 color = D2D.GetInput(0);

        return color * this.Amount;
    }
}