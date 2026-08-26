using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using ComputeWeave;
using ComputeWeave.Interop;

[assembly: SupportedOSPlatform("windows6.2")]

float[] array = [.. Enumerable.Range(1, 100)];

// Create the graphics buffer
using ReadWriteBuffer<float> gpuBuffer = GraphicsDevice.GetDefault().AllocateReadWriteBuffer(array);

// Run the shader
GraphicsDevice.GetDefault().For(100, new MultiplyByTwo(gpuBuffer));

// Get the data back
float[] result = gpuBuffer.ToArray();

// Validate results
for (int i = 0; i < array.Length; i++)
{
    Trace.Assert(result[i] == array[i] * 2);
}

// Also get the shader info (this requires DXC to be present)
ShaderInfo shaderInfo = ReflectionServices.GetShaderInfo<MultiplyByTwo>();

// Validate a couple properties as a sanity check
Trace.Assert(shaderInfo.HlslSource is { Length: > 0 });
Trace.Assert(shaderInfo.BoundResourceCount == 2);

// Collect the DXC libraries now mapped into the process
ProcessModule[] dxcModules = [.. Process.GetCurrentProcess().Modules
    .Cast<ProcessModule>()
    .Where(static module => module.ModuleName.Equals("dxcompiler.dll", StringComparison.OrdinalIgnoreCase))];

ProcessModule[] dxilModules = [.. Process.GetCurrentProcess().Modules
    .Cast<ProcessModule>()
    .Where(static module => module.ModuleName.Equals("dxil.dll", StringComparison.OrdinalIgnoreCase))];

Trace.Assert(dxcModules.Length == 1);
Trace.Assert(dxilModules.Length == 1);

// The two are deployed as a pair, so a dxil.dll from anywhere else is another copy found on the machine
Trace.Assert(string.Equals(
    Path.GetDirectoryName(dxilModules[0].FileName),
    Path.GetDirectoryName(dxcModules[0].FileName),
    StringComparison.OrdinalIgnoreCase));

/// <summary>
/// A sample kernel that requires dynamic compilation, as it's not precompiled.
/// </summary>
[ThreadGroupSize(DefaultThreadGroupSizes.X)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct MultiplyByTwo(ReadWriteBuffer<float> buffer) : IComputeShader
{
    /// <inheritdoc/>
    public void Execute()
    {
        buffer[ThreadIds.X] *= 2;
    }
}