using System;
using ComputeWeave.Graphics.Extensions;
using ComputeWeave.Graphics.Pipelines;
using ComputeWeave.Resources.Lifetime;
using ComputeWeave.Win32;
using static ComputeWeave.Win32.D3D12_COMMAND_LIST_TYPE;
using static ComputeWeave.Win32.D3D12_RESOURCE_STATES;

namespace ComputeWeave.Graphics.Commands;

internal unsafe struct ManualCopyCommandList : IDisposable
{
    private readonly GraphicsDevice device;

    private CommandList commandList;

    private GraphicsResourceLeaseSet? resourceLeases;

    private ID3D12Resource* firstResource;

    private D3D12_RESOURCE_STATES firstState;

    private ID3D12Resource* secondResource;

    private D3D12_RESOURCE_STATES secondState;

    private int resourceCount;

    private bool isComputeQueueRequired;

    public ManualCopyCommandList(GraphicsDevice device)
    {
        default(ArgumentNullException).ThrowIfNull(device);

        this = default;
        this.device = device;
        this.resourceLeases = GraphicsResourceLeaseSet.Rent();
    }

    public ID3D12GraphicsCommandList* D3D12GraphicsCommandList
    {
        get
        {
            EnsureCommandList();

            return this.commandList.D3D12GraphicsCommandList;
        }
    }

    public void TrackResource(IGraphicsResource resource, ID3D12Resource* d3D12Resource, ComputeResourceAccess access)
    {
        default(ArgumentNullException).ThrowIfNull(resource);
        default(ArgumentNullException).ThrowIf(d3D12Resource is null, nameof(d3D12Resource));
        default(InvalidOperationException).ThrowIf(this.resourceLeases is null);
        default(InvalidOperationException).ThrowIf(this.commandList.IsAllocated);
        default(InvalidOperationException).ThrowIf(this.resourceCount == 2);

        D3D12_RESOURCE_STATES state = access switch
        {
            ComputeResourceAccess.Read => D3D12_RESOURCE_STATE_COPY_SOURCE,
            ComputeResourceAccess.Write => D3D12_RESOURCE_STATE_COPY_DEST,
            _ => default(ArgumentException).Throw<D3D12_RESOURCE_STATES>(nameof(access))
        };

        TrackedResourceState residentState = new ResourceUsageRecorder(this.resourceLeases).RecordCopy(resource, access);

        this.isComputeQueueRequired |= residentState is not TrackedResourceState.Common;

        if (this.resourceCount++ == 0)
        {
            this.firstResource = d3D12Resource;
            this.firstState = state;
        }
        else
        {
            this.secondResource = d3D12Resource;
            this.secondState = state;
        }
    }

    public void ExecuteAndWaitForCompletion()
    {
        default(InvalidOperationException).ThrowIf(this.resourceLeases is null);

        EnsureCommandList();

        if (this.commandList.D3D12CommandListType == D3D12_COMMAND_LIST_TYPE_COMPUTE)
        {
            this.commandList.D3D12GraphicsCommandList->TransitionBarrier(
                this.firstResource,
                this.firstState,
                D3D12_RESOURCE_STATE_COMMON);

            if (this.resourceCount == 2)
            {
                this.commandList.D3D12GraphicsCommandList->TransitionBarrier(
                    this.secondResource,
                    this.secondState,
                    D3D12_RESOURCE_STATE_COMMON);
            }
        }

        this.commandList.ExecuteAndWaitForCompletion(this.resourceLeases);
    }

    public void Dispose()
    {
        GraphicsResourceLeaseSet? resourceLeases = this.resourceLeases;

        this.resourceLeases = null;
        this.commandList.Dispose();
        resourceLeases?.Release();
    }

    private void EnsureCommandList()
    {
        if (this.commandList.IsAllocated)
        {
            return;
        }

        default(InvalidOperationException).ThrowIf(this.resourceCount == 0);

        this.commandList = new CommandList(
            this.device,
            this.isComputeQueueRequired ? D3D12_COMMAND_LIST_TYPE_COMPUTE : D3D12_COMMAND_LIST_TYPE_COPY);

        if (this.commandList.D3D12CommandListType == D3D12_COMMAND_LIST_TYPE_COMPUTE)
        {
            this.commandList.D3D12GraphicsCommandList->TransitionBarrier(
                this.firstResource,
                D3D12_RESOURCE_STATE_COMMON,
                this.firstState);

            if (this.resourceCount == 2)
            {
                this.commandList.D3D12GraphicsCommandList->TransitionBarrier(
                    this.secondResource,
                    D3D12_RESOURCE_STATE_COMMON,
                    this.secondState);
            }
        }
    }
}
