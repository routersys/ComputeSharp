namespace ComputeSharp.Interop;

internal struct ComputeInteropDomainRecord
{
    public ComputeInteropDomainState State;

    public DomainReferenceCounts References;

    public ComputeInteropDomainRecord()
    {
        this.State = ComputeInteropDomainState.Active;
        this.References = new DomainReferenceCounts();
    }

    public readonly bool IsDisposeRequested => this.References.Owner == 0;

    public readonly bool IsDisposed => this.State is ComputeInteropDomainState.Disposed;

    public bool TryAcquire(ExternalDomainReference reference)
    {
        if (reference is ExternalDomainReference.Maintenance)
        {
            if (this.State is ComputeInteropDomainState.ReleasingNative or ComputeInteropDomainState.Disposed)
            {
                return false;
            }

            return this.References.TryAcquire(reference);
        }

        if (this.State is not ComputeInteropDomainState.Active)
        {
            return false;
        }

        return this.References.TryAcquire(reference);
    }

    public bool TryRelease(ExternalDomainReference reference)
    {
        return this.References.TryRelease(reference);
    }

    public bool TryRequestDispose()
    {
        if (this.State is not ComputeInteropDomainState.Active)
        {
            return false;
        }

        this.State = ComputeInteropDomainState.DisposeRequested;

        return true;
    }

    public bool TryReleaseOwner()
    {
        if (!this.References.TryRelease(ExternalDomainReference.Owner))
        {
            return false;
        }

        if (this.State is ComputeInteropDomainState.DisposeRequested)
        {
            this.State = ComputeInteropDomainState.TeardownStarted;
        }

        return true;
    }

    public bool TryMarkPoisoned()
    {
        if (this.State is not (ComputeInteropDomainState.Active or ComputeInteropDomainState.DisposeRequested))
        {
            return false;
        }

        this.State = ComputeInteropDomainState.Poisoned;

        return true;
    }

    public bool TryBeginTeardown()
    {
        if (this.State is not ComputeInteropDomainState.Poisoned)
        {
            return false;
        }

        this.State = ComputeInteropDomainState.TeardownStarted;

        return true;
    }

    public bool TryMarkTerminal()
    {
        if (this.State is ComputeInteropDomainState.Terminal
            or ComputeInteropDomainState.ReleasingNative
            or ComputeInteropDomainState.Disposed)
        {
            return false;
        }

        this.State = ComputeInteropDomainState.Terminal;

        return true;
    }

    public bool TryBeginReleasingNative()
    {
        if (this.State is not ComputeInteropDomainState.TeardownStarted || !this.References.IsZero)
        {
            return false;
        }

        this.State = ComputeInteropDomainState.ReleasingNative;

        return true;
    }

    public bool TryBeginReleasingNativeForDeviceTeardown()
    {
        if (this.State is not ComputeInteropDomainState.Terminal)
        {
            return false;
        }

        this.State = ComputeInteropDomainState.ReleasingNative;

        return true;
    }

    public bool TryCompleteDisposal()
    {
        if (this.State is not ComputeInteropDomainState.ReleasingNative)
        {
            return false;
        }

        this.State = ComputeInteropDomainState.Disposed;

        return true;
    }
}
