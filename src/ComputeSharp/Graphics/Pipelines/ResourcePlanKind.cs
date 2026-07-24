namespace ComputeSharp.Graphics.Pipelines;

internal enum ResourcePlanKind : byte
{
    Buffer = 0,
    Texture2D = 1,
    ResourceGroup = 2,
    SharedTexture2D = 3
}
