namespace ComputeSharp.Resources.Lifetime;

internal enum TrackedResourceState : byte
{
    Unknown = 0,
    Common = 1,
    UnorderedAccess = 2,
    NonPixelShaderResource = 3,
    CopySource = 4,
    CopyDestination = 5,
    GenericRead = 6,
    ReadbackCopyDestination = 7
}
