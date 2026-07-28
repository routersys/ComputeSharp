using ComputeSharp.Graphics.Pipelines;

namespace ComputeSharp.Interop;

internal enum DomainOperationStatus : byte
{
    Acquired = 0,
    DomainUnavailable = 1,
    TokenExhausted = 2,
    PermitBusy = 3,
    SchedulerBusy = 4
}

internal struct DomainOperationRecord(ExternalDomainId domain)
{
    private const int ActiveState = 1;

    private const int ReleasingState = 2;

    public ulong NextToken;

    public ulong ActiveToken;

    public int Active;

    public ExternalDomainId Domain = domain;

    public ResourceGenerationId BoundGeneration;

    public int ReleaseExternalReferenceOnDispose;

    public DomainOperationStatus TryAcquire(
        ResourceGenerationId boundGeneration,
        bool releaseExternalReferenceOnDispose,
        out ulong token)
    {
        token = 0;

        if (this.NextToken == ulong.MaxValue)
        {
            return DomainOperationStatus.TokenExhausted;
        }

        if (this.Active != 0)
        {
            return DomainOperationStatus.PermitBusy;
        }

        this.NextToken++;
        this.ActiveToken = this.NextToken;
        this.Active = ActiveState;
        this.BoundGeneration = boundGeneration;
        this.ReleaseExternalReferenceOnDispose = releaseExternalReferenceOnDispose ? 1 : 0;

        token = this.ActiveToken;

        return DomainOperationStatus.Acquired;
    }

    public readonly bool IsActive(ulong token)
    {
        return this.Active == ActiveState && token != 0 && this.ActiveToken == token;
    }

    public bool TryRelease(ulong token)
    {
        if (!TryBeginRelease(token))
        {
            return false;
        }

        CompleteRelease();

        return true;
    }

    public bool TryBeginRelease(ulong token)
    {
        if (this.Active != ActiveState || token == 0 || this.ActiveToken != token)
        {
            return false;
        }

        this.Active = ReleasingState;

        return true;
    }

    public void CompleteRelease()
    {
        this.Active = 0;
        this.BoundGeneration = default;
        this.ReleaseExternalReferenceOnDispose = 0;
    }
}
