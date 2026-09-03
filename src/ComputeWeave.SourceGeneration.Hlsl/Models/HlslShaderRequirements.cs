namespace ComputeWeave.SourceGeneration.Models;

/// <summary>
/// The requirements a shader raises while its source is rewritten into HLSL.
/// </summary>
/// <remarks>
/// <para>
/// A rewriter handles one declaration, and reaching a called method, a constructor or a static field
/// initializer creates another rewriter for that declaration in turn. What any of them raises belongs to
/// the shader and not to the rewriter that happened to reach it, so every rewriter for one shader shares
/// a single instance of this type. A requirement raised anywhere is then already in the instance the
/// generator reads afterwards, with nothing to carry back out along the way it was reached.
/// </para>
/// <para>
/// Which requirements there are differs between the two generators, so they are declared in the partial
/// declaration each one carries, and this declaration holds the type they share.
/// </para>
/// </remarks>
internal sealed partial class HlslShaderRequirements
{
}