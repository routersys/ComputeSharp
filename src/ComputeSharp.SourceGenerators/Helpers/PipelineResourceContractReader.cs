using Microsoft.CodeAnalysis;

namespace ComputeSharp.SourceGenerators.Helpers;

/// <summary>
/// The reader of the contract values declared by a <c>[ComputePipelineResource]</c> attribute.
/// </summary>
internal static class PipelineResourceContractReader
{
    /// <summary>
    /// Tries to read the declared contract values of a <c>[ComputePipelineResource]</c> attribute.
    /// </summary>
    /// <param name="attribute">The attribute data to read.</param>
    /// <param name="access">The declared compute access.</param>
    /// <param name="hasRecovery">Whether a recovery class was declared.</param>
    /// <param name="recovery">The declared recovery class.</param>
    /// <returns>Whether the attribute declares a supported contract.</returns>
    public static bool TryRead(
        AttributeData attribute,
        out ComputeResourceAccess access,
        out bool hasRecovery,
        out ComputeResourceRecovery recovery)
    {
        access = default;
        hasRecovery = false;
        recovery = default;

        switch (attribute.ConstructorArguments)
        {
            case [{ Value: byte accessValue }]:
                access = (ComputeResourceAccess)accessValue;

                return IsKnownAccess(access);
            case [{ Value: byte accessValueWithRecovery }, { Value: byte recoveryValue }]:
                access = (ComputeResourceAccess)accessValueWithRecovery;
                hasRecovery = true;
                recovery = (ComputeResourceRecovery)recoveryValue;

                return IsKnownAccess(access) && IsKnownRecovery(recovery);
            default:
                return false;
        }
    }

    /// <summary>
    /// Checks whether a given compute access is a known value.
    /// </summary>
    /// <param name="access">The compute access to check.</param>
    /// <returns>Whether <paramref name="access"/> is a known value.</returns>
    private static bool IsKnownAccess(ComputeResourceAccess access)
    {
        return access is ComputeResourceAccess.Read or ComputeResourceAccess.Write or ComputeResourceAccess.ReadWrite;
    }

    /// <summary>
    /// Checks whether a given recovery class is a known value.
    /// </summary>
    /// <param name="recovery">The recovery class to check.</param>
    /// <returns>Whether <paramref name="recovery"/> is a known value.</returns>
    private static bool IsKnownRecovery(ComputeResourceRecovery recovery)
    {
        return recovery is
            ComputeResourceRecovery.Discardable or
            ComputeResourceRecovery.RecreateFromHost or
            ComputeResourceRecovery.Recompute or
            ComputeResourceRecovery.CapacityOnly;
    }
}
