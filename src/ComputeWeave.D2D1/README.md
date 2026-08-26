# ComputeWeave.D2D1

English | [日本語](https://github.com/routersys/ComputeWeave/blob/main/src/ComputeWeave.D2D1/README.ja.md)

A companion package to [ComputeWeave](https://www.nuget.org/packages/ComputeWeave). It writes Direct2D pixel shaders entirely in C# and registers them as `ID2D1Effect`.

This is not an extension of the [ComputeWeave](https://www.nuget.org/packages/ComputeWeave) package and does not reference it. What the two share is the base layer in [ComputeWeave.Core](https://www.nuget.org/packages/ComputeWeave.Core): the primitive types and the `Hlsl` intrinsics. The shaders here are executed by Direct2D rather than on the Direct3D 12 compute queue, so nothing in this package creates or uses a `GraphicsDevice`, and the declarative layer the fork adds does not apply to them.

A pixel shader is a `partial struct` implementing `ID2D1PixelShader`.

```csharp
[D2DInputCount(1)]
[D2DInputSimple(0)]
[D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
[D2DGeneratedPixelShaderDescriptor]
public readonly partial struct DifferenceEffect(float amount) : ID2D1PixelShader
{
    public float4 Execute()
    {
        float4 color = D2D.GetInput(0);
        float3 rgb = Hlsl.Saturate(this.amount - color.RGB);

        return new(rgb, 1);
    }
}
```

The bytecode and the constant buffer are then available without a device.

```csharp
ReadOnlyMemory<byte> bytecode = D2D1PixelShader.LoadBytecode<DifferenceEffect>();
ReadOnlyMemory<byte> buffer = D2D1PixelShader.GetConstantBuffer(new DifferenceEffect(1));
```

`D2D1PixelShaderEffect` registers a shader as a Direct2D effect and creates `ID2D1Effect` instances from it, with a custom draw transform when one is needed. `D2D1ReflectionServices` reports the generated HLSL source and the shader statistics.

Shaders are compiled to DXBC with FXC, which is what Direct2D accepts. `d3dcompiler_47.dll` ships with Windows, so this package bundles no compiler of its own.

## More

The complete API reference is in the [repository](https://github.com/routersys/ComputeWeave).
