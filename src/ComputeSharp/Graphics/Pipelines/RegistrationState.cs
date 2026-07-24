namespace ComputeSharp.Graphics.Pipelines;

internal enum RegistrationState : byte
{
    Constructing = 0,
    Active = 1,
    DisposeRequested = 2,
    Releasing = 3,
    Released = 4
}
