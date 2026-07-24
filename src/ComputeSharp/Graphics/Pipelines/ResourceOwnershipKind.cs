namespace ComputeSharp.Graphics.Pipelines;

internal enum ResourceOwnershipKind : byte
{
    Borrowed = 0,
    OwnedSlot = 1,
    OwnedGroupSlot = 2,
    SharedTextureSlot = 3
}
