namespace ComputeSharp.Resources.Lifetime;

internal enum ResourceReleaseAuthority : byte
{
    NormalCompletion = 0,
    DomainTeardown = 1,
    DeviceTeardown = 2
}
