# ComputeWeave.Dxc

English | [日本語](https://github.com/routersys/ComputeWeave/blob/main/src/ComputeWeave.Dxc/README.ja.md)

An extension package for [ComputeWeave](https://www.nuget.org/packages/ComputeWeave). It bundles the DXC compiler and enables shader reflection.

When the generated HLSL source or the statistics exposed by the Direct3D 12 reflection APIs are needed, `ReflectionServices` gathers them for a shader type.

```csharp
ShaderInfo shaderInfo = ReflectionServices.GetShaderInfo<MyShader>();

string hlslSource = shaderInfo.HlslSource;
uint numberOfResources = shaderInfo.BoundResourceCount;
uint instructionCount = shaderInfo.InstructionCount;
```

This package bundles `dxcompiler.dll` and `dxil.dll` and therefore runs only in x64 and Arm64 processes.

The declarative layer this fork adds is in the [ComputeWeave](https://www.nuget.org/packages/ComputeWeave) package; this package adds nothing to it.

## More

The complete API reference is in the [repository](https://github.com/routersys/ComputeWeave).
