using System;

namespace ComputeSharp.Interop;

internal readonly struct DomainOperationLease(
    ComputeInteropDomain? domain,
    ExternalDomainReference reference,
    ulong token) : IDisposable
{
    public bool IsValid => domain is not null;

    public ulong Token => token;

    public void Dispose()
    {
        domain?.ReleaseOperation(reference, token);
    }
}
