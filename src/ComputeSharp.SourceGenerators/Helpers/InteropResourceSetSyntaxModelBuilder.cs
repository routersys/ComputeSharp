using ComputeSharp.SourceGeneration.Helpers;
using ComputeSharp.SourceGenerators.Models;

namespace ComputeSharp.SourceGenerators.Helpers;

/// <summary>
/// The builder of the members generated for the shared texture slots of a compute interop resource set.
/// </summary>
internal static class InteropResourceSetSyntaxModelBuilder
{
    /// <summary>
    /// Tries to build the members generated for the shared texture slots of a given resource set.
    /// </summary>
    /// <param name="resourceSet">The interop resource set contract model to build the members of.</param>
    /// <param name="slots">The resulting shared texture slots, in canonical slot ordinal order.</param>
    /// <returns>Whether the members of every shared texture slot of <paramref name="resourceSet"/> could be built.</returns>
    public static bool TryBuild(InteropResourceSetContractInfo resourceSet, out EquatableArray<SharedTextureSlotSyntaxInfo> slots)
    {
        using ImmutableArrayBuilder<SharedTextureSlotSyntaxInfo> builder = new();

        foreach (SharedTextureContractInfo sharedTexture in resourceSet.SharedTextures)
        {
            if (!GeneratedIdentifier.TryCreateCanonicalName(sharedTexture.MemberMetadataName, out string canonicalName))
            {
                slots = default;

                return false;
            }

            builder.Add(new SharedTextureSlotSyntaxInfo(
                canonicalName,
                sharedTexture.MemberMetadataName,
                $"global::ComputeSharp.SharedTextureSlot<{sharedTexture.ElementTypeName}, {sharedTexture.PixelTypeName}, {sharedTexture.ViewTypeName}>",
                $"global::ComputeSharp.ReadWriteTexture2D<{sharedTexture.ElementTypeName}, {sharedTexture.PixelTypeName}>",
                sharedTexture.ViewTypeName,
                sharedTexture.BindingAccessibility,
                sharedTexture.ViewAccessibility));
        }

        slots = builder.ToImmutable();

        return true;
    }
}
