using System.Collections.Generic;
using System.Reflection;
using ComputeWeave.Core.Intrinsics;
using ComputeWeave.D2D1;

namespace ComputeWeave.SourceGeneration.Mappings;

/// <inheritdoc/>
partial class HlslKnownKeywords
{
    /// <summary>
    /// The identifiers FXC rejects that DXC accepts.
    /// </summary>
    /// <remarks>
    /// The shared list is measured against DXC, which the Direct2D path does not use. These names were
    /// found by declaring every candidate identifier as a shader global and compiling it with FXC. The
    /// candidates were every identifier-like string in the three <c>d3dcompiler_47.dll</c> binaries
    /// installed on the measuring machine, plus every identifier of at most three characters. All three
    /// versions reject the same set. Most of these are names of the effect framework FXC still parses.
    /// <para>
    /// The sweep also rejects <c>Execute</c>, which is left out on purpose. It is not a name FXC
    /// reserves; it is rejected because a global of that name collides with the entry point the
    /// generator emits. Reserving it renames the entry point declaration itself, at which point
    /// <c>D2D_PS_ENTRY</c> declares a function nothing implements and every shader fails to compile.
    /// A captured field cannot carry that name in the first place, the shader type already having a
    /// member called <c>Execute</c>.
    /// </para>
    /// </remarks>
    private static readonly string[] FxcReservedNames =
    [
        "BlendState", "CompileShader", "ComputeShader", "D3D10_COMPILER", "D3DX", "D3DX_VERSION",
        "DIRECT3D", "DepthStencilState", "DepthStencilView", "DomainShader", "GeometryShader",
        "HLSL_VERSION", "HullShader", "PixelShader", "RasterizerOrderedByteAddressBuffer",
        "RasterizerState", "RenderTargetView", "SamplerComparisonState", "SamplerState", "String",
        "VertexShader", "pixelshader", "sampler1D", "sampler2D", "sampler3D", "samplerCUBE",
        "texture1D", "texture2D", "texture3D", "textureCUBE", "vertexshader"
    ];

    /// <summary>
    /// The identifiers FXC rejects in every casing, and not just in the one it is spelled with.
    /// </summary>
    /// <remarks>
    /// Sweeping every casing of every rejected name of at most ten characters, 20804 of them, found
    /// exactly these four. Reserving one casing of them would leave a field named <c>Pass</c> or
    /// <c>Technique</c> failing to compile with no indication of why.
    /// </remarks>
    private static readonly string[] FxcCaseInsensitiveNames = ["asm", "decl", "pass", "technique"];

    /// <inheritdoc/>
    static partial void AddKnownKeywords(ICollection<string> knownKeywords)
    {
        // D2D1 intrinsics method names
        foreach (MethodInfo? method in typeof(D2D).GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            string name = method.GetCustomAttribute<HlslIntrinsicNameAttribute>()?.Name ?? method.Name;

            knownKeywords.Add(name);
        }

        // Names FXC reserves and DXC does not
        foreach (string name in FxcReservedNames)
        {
            knownKeywords.Add(name);
        }

        // Every casing of the names FXC matches without regard to case
        foreach (string name in FxcCaseInsensitiveNames)
        {
            foreach (string casing in EnumerateCasings(name))
            {
                knownKeywords.Add(casing);
            }
        }
    }

    /// <summary>
    /// Enumerates every casing of an identifier made up of letters.
    /// </summary>
    /// <param name="name">The identifier to enumerate the casings of.</param>
    /// <returns>Every casing of <paramref name="name"/>, including <paramref name="name"/> itself.</returns>
    private static IEnumerable<string> EnumerateCasings(string name)
    {
        char[] characters = new char[name.Length];

        for (int mask = 0; mask < 1 << name.Length; mask++)
        {
            for (int i = 0; i < characters.Length; i++)
            {
                characters[i] = ((mask >> i) & 1) == 0
                    ? char.ToLowerInvariant(name[i])
                    : char.ToUpperInvariant(name[i]);
            }

            yield return new string(characters);
        }
    }
}
