using System;
using ComputeSharp.Graphics.Extensions;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Win32;
using static ComputeSharp.Win32.D3D12_COMMAND_LIST_TYPE;
using static ComputeSharp.Win32.D3D12_RESOURCE_STATES;

namespace ComputeSharp.Graphics.Commands;

internal unsafe struct ManualCopyCommandList : IDisposable
{
    private CommandList commandList;

    private GraphicsResourceLeaseSet? resourceLeases;

    private ID3D12Resource* firstResource;

    private D3D12_RESOURCE_STATES firstState;

    private ID3D12Resource* secondResource;

    private D3D12_RESOURCE_STATES secondState;

    private int resourceCount;

    public ManualCopyCommandList(GraphicsDevice device)
    {
        this = default;
        this.resourceLeases = GraphicsResourceLeaseSet.Rent();

        try
        {
            this.commandList = new CommandList(device, D3D12_COMMAND_LIST_TYPE_COPY);
        }
        catch
        {
            this.resourceLeases.Release();
            this.resourceLeases = null;

            throw;
        }
    }

    public readonly ID3D12GraphicsCommandList* D3D12GraphicsCommandList => this.commandList.D3D12GraphicsCommandList;

    public void TrackResource(IGraphicsResource resource, ID3D12Resource* d3D12Resource, ComputeResourceAccess access)
    {
        default(ArgumentNullException).ThrowIfNull(resource);
        default(ArgumentNullException).ThrowIf(d3D12Resource is null, nameof(d3D12Resource));
        default(InvalidOperationException).ThrowIf(this.resourceLeases is null);
        default(InvalidOperationException).ThrowIf(this.resourceCount == 2);

        D3D12_RESOURCE_STATES state = access switch
        {
            ComputeResourceAccess.Read => D3D12_RESOURCE_STATE_COPY_SOURCE,
            ComputeResourceAccess.Write => D3D12_RESOURCE_STATE_COPY_DEST,
            _ => default(ArgumentException).Throw<D3D12_RESOURCE_STATES>(nameof(access))
        };

        new ResourceUsageRecorder(this.resourceLeases).RecordCopy(resource, access);

        this.commandList.D3D12GraphicsCommandList->TransitionBarrier(
            d3D12Resource,
            D3D12_RESOURCE_STATE_COMMON,
            state);

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
        default(InvalidOperationException).ThrowIf(this.resourceCount == 0);

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

        this.commandList.ExecuteAndWaitForCompletion(this.resourceLeases);
    }

    public void Dispose()
    {
        GraphicsResourceLeaseSet? resourceLeases = this.resourceLeases;

        this.resourceLeases = null;
        this.commandList.Dispose();
        resourceLeases?.Release();
    }
}
