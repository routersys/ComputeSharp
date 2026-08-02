using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Canonicalization;

[TestClass]
public class PipelineHostEmissionTests
{
    private const string GroupSource = """
        using ComputeWeave;

        namespace Ukiyoe;

        [ComputeResourceGroup]
        public sealed partial class GridResources
        {
            [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
            public ReadWriteBuffer<double> ColorIn { get; }

            [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
            public ReadWriteTexture2D<Bgra32, Float4> Mask { get; }
        }
        """;

    private const string HostSource = """
        using ComputeWeave;

        namespace Ukiyoe;

        [ComputePipelineHost("device", 1)]
        public sealed partial class Host
        {
            private readonly GraphicsDevice device;

            [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
            private readonly ComputeResourceSlot<ReadWriteBuffer<int>> index = new();

            [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
            private readonly ComputeResourceSlot<ReadWriteTexture2D<float>> silhouette = new();

            [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
            private readonly ComputeResourceGroupSlot<GridResources> grid = new();

            [ComputePipeline]
            private void Run(in ComputeContext context)
            {
            }
        }
        """;

    private const string ParameterHostSource = """
        using ComputeWeave;

        namespace Ukiyoe;

        [ComputePipelineHost("device", 1)]
        public sealed partial class Host
        {
            private readonly GraphicsDevice device;

            [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
            private readonly ComputeResourceGroupSlot<GridResources> grid = new();

            [ComputePipeline]
            private void Run(in ComputeContext context, [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> source)
            {
            }
        }
        """;

    private const string OwnedResourceHostSource = """
        using ComputeWeave;

        namespace Ukiyoe;

        [ComputePipelineHost("device", 1)]
        public sealed partial class Host
        {
            private readonly GraphicsDevice device;

            [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
            private readonly ComputeResourceSlot<ReadWriteBuffer<int>> index = new();

            [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
            private readonly ComputeResourceGroupSlot<GridResources> grid = new();

            [ComputePipeline]
            private void Run(
                in ComputeContext context,
                [ComputeOwnedResource(nameof(index))] ReadWriteBuffer<int> index,
                [ComputeOwnedResource(nameof(grid))] GridResources grid,
                int length)
            {
            }
        }
        """;

    private static string RunAndGetSource(string[] sources, string assemblyName, string hintNameFragment)
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation(sources, assemblyName);
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new PipelineDescriptorGenerator());

        return GeneratorHelper.GetGeneratedSource(GeneratorHelper.Run(driver, compilation, out _), hintNameFragment);
    }

    [TestMethod]
    public void EmitsTheRegistrationFactoryInSlotOrdinalOrder()
    {
        string source = RunAndGetSource([GroupSource, HostSource], "HostFactoryTests", "Ukiyoe.Host");

        Assert.IsTrue(source.Contains("private readonly global::ComputeWeave.ComputeHostRuntime computeHostRuntime;"), source);
        Assert.IsTrue(source.Contains("private Host(global::ComputeWeave.GraphicsDevice device, int maximumPendingSubmissions)"), source);
        Assert.IsTrue(source.Contains("this.@device = device;"), source);
        Assert.IsTrue(
            source.Contains(
                "this.computeHostRuntime = global::ComputeWeave.ComputeHostRuntime.Create(device, CanonicalDescriptor, " +
                "maximumPendingSubmissions, [this.@grid, this.@index, this.@silhouette]);"),
            source);
        Assert.IsTrue(source.Contains("public static Host Create(global::ComputeWeave.GraphicsDevice device, int maximumPendingSubmissions)"), source);
        Assert.IsTrue(source.Contains("return new Host(device, maximumPendingSubmissions);"), source);
    }

    [TestMethod]
    public void EmitsTheDisposalMembers()
    {
        string source = RunAndGetSource([GroupSource, HostSource], "HostDisposalTests", "Ukiyoe.Host");

        Assert.IsTrue(source.Contains("partial class Host : global::System.IDisposable"), source);
        Assert.IsTrue(source.Contains("this.computeHostRuntime.Dispose();"), source);
        Assert.IsTrue(source.Contains("this.@grid.Dispose();"), source);
        Assert.IsTrue(source.Contains("this.@index.Dispose();"), source);
        Assert.IsTrue(source.Contains("this.@silhouette.Dispose();"), source);
        Assert.IsTrue(source.Contains("this.computeHostRuntime.WaitForDisposal();"), source);
    }

    [TestMethod]
    public void EmitsTheInvocationWrapperInContractOrdinalOrder()
    {
        string source = RunAndGetSource([GroupSource, ParameterHostSource], "HostInvocationTests", "Ukiyoe.Host");

        Assert.IsTrue(
            source.Contains("public global::ComputeWeave.ComputeSubmission Run(global::ComputeWeave.ReadWriteBuffer<int> @source)"),
            source);
        Assert.IsTrue(source.Contains("return this.computeHostRuntime.Submit(new RunInvocation(this, @source));"), source);
        Assert.IsTrue(source.Contains("private readonly struct RunInvocation : global::ComputeWeave.IComputePipelineInvocation"), source);
        Assert.IsTrue(source.Contains("public static int PipelineOrdinal => 0;"), source);
        Assert.IsTrue(source.Contains("if (!binder.TryPin(this.@source))"), source);
        Assert.IsTrue(
            source.Contains(
                "global::ComputeWeave.ComputeResourceBinding<global::ComputeWeave.ReadWriteBuffer<double>> binding1 = " +
                "this.host.computeHostRuntime.GetBinding<global::ComputeWeave.ReadWriteBuffer<double>>(0, 0);"),
            source);
        Assert.IsTrue(source.Contains("if (!binder.TryPin(0, in binding1))"), source);
        Assert.IsTrue(source.Contains("this.host.Run(in context, this.@source);"), source);
    }

    [TestMethod]
    public void EmitsTheOwnedResourceArgumentsOutsideTheGeneratedOverload()
    {
        string source = RunAndGetSource([GroupSource, OwnedResourceHostSource], "HostOwnedResourceTests", "Ukiyoe.Host");

        Assert.IsTrue(source.Contains("public global::ComputeWeave.ComputeSubmission Run(int @length)"), source);
        Assert.IsTrue(source.Contains("return this.computeHostRuntime.Submit(new RunInvocation(this, @length));"), source);
        Assert.IsTrue(source.Contains("private struct RunInvocation : global::ComputeWeave.IComputePipelineInvocation"), source);
        Assert.IsTrue(source.Contains("private global::ComputeWeave.ReadWriteBuffer<int> @index;"), source);
        Assert.IsTrue(source.Contains("private global::Ukiyoe.GridResources @grid;"), source);
        Assert.IsTrue(source.Contains("private global::Ukiyoe.GridResources? @computeGenerationGrid;"), source);
        Assert.IsTrue(
            source.Contains(
                "if (!binder.TryPin(0, in binding0, out global::ComputeWeave.ReadWriteBuffer<double> resource0))"),
            source);
        Assert.IsTrue(source.Contains("this.@index = resource2;"), source);
        Assert.IsTrue(
            source.Contains(
                "this.@grid = global::Ukiyoe.GridResources.GetComputeGeneration(" +
                "ref this.host.@computeGenerationGrid, resource0, resource1);"),
            source);
        Assert.IsTrue(source.Contains("this.host.Run(in context, this.@index, this.@grid, this.@length);"), source);
    }

    [TestMethod]
    public void EmitsTypedPlanMethodsWithTheSlotOrdinalAndPlanVector()
    {
        string source = RunAndGetSource([GroupSource, HostSource], "HostPlanMethodTests", "Ukiyoe.Host");

        Assert.IsTrue(source.Contains("public bool TryEnsureIndex(in IndexPlan plan, out bool changed)"), source);
        Assert.IsTrue(
            source.Contains(
                "return this.computeHostRuntime.TryEnsureResource(1, [plan.IndexLength], new IndexMaterializer(plan.IndexLength), out changed);"),
            source);
        Assert.IsTrue(source.Contains("public bool TryEnsureSilhouette(in SilhouettePlan plan, out bool changed)"), source);
        Assert.IsTrue(
            source.Contains(
                "return this.computeHostRuntime.TryEnsureResource(2, [plan.SilhouetteWidth, plan.SilhouetteHeight], " +
                "new SilhouetteMaterializer(plan.SilhouetteWidth, plan.SilhouetteHeight), out changed);"),
            source);
    }

    [TestMethod]
    public void EmitsTheGroupPlanTypeForAnOwnedGroupSlot()
    {
        string source = RunAndGetSource([GroupSource, HostSource], "HostGroupPlanTests", "Ukiyoe.Host");

        Assert.IsTrue(source.Contains("public bool TryEnsureGrid(in global::Ukiyoe.GridResources.Plan plan, out bool changed)"), source);
        Assert.IsTrue(
            source.Contains(
                "return this.computeHostRuntime.TryEnsureResource(0, [plan.ColorInLength, plan.MaskWidth, plan.MaskHeight], " +
                "new GridMaterializer(plan.ColorInLength, plan.MaskWidth, plan.MaskHeight), out changed);"),
            source);
    }

    [TestMethod]
    public void EmitsComputeBindingAccessorsForSingleResourceSlotsOnly()
    {
        string source = RunAndGetSource([GroupSource, HostSource], "HostBindingTests", "Ukiyoe.Host");

        Assert.IsTrue(
            source.Contains(
                "public global::ComputeWeave.ComputeResourceBinding<global::ComputeWeave.ReadWriteBuffer<int>> GetIndexComputeBinding()"),
            source);
        Assert.IsTrue(
            source.Contains("return this.computeHostRuntime.GetBinding<global::ComputeWeave.ReadWriteBuffer<int>>(1, 0);"),
            source);
        Assert.IsTrue(
            source.Contains(
                "public global::ComputeWeave.ComputeResourceBinding<global::ComputeWeave.ReadWriteTexture2D<float>> GetSilhouetteComputeBinding()"),
            source);
        Assert.IsFalse(source.Contains("GetGridComputeBinding"), source);
    }

    [TestMethod]
    public void EmitsMaterializerDeclarationsInSlotResourceIndexOrder()
    {
        string source = RunAndGetSource([GroupSource, HostSource], "HostMaterializerTests", "Ukiyoe.Host");

        Assert.IsTrue(source.Contains("private readonly struct GridMaterializer : global::ComputeWeave.IComputeGenerationMaterializer"), source);
        Assert.IsTrue(source.Contains("public GridMaterializer(int colorInLength, int maskWidth, int maskHeight)"), source);
        Assert.IsTrue(source.Contains("context.DeclareBuffer<double>(this.colorInLength);"), source);
        Assert.IsTrue(
            source.Contains(
                "context.DeclareTexture2D<global::ComputeWeave.Bgra32, global::ComputeWeave.Float4>(this.maskWidth, this.maskHeight);"),
            source);
        Assert.IsTrue(
            source.IndexOf("context.DeclareBuffer<double>(this.colorInLength);") <
            source.IndexOf("context.DeclareTexture2D<global::ComputeWeave.Bgra32, global::ComputeWeave.Float4>(this.maskWidth, this.maskHeight);"),
            source);
        Assert.IsTrue(source.Contains("context.DeclareTexture2D<float>(this.silhouetteWidth, this.silhouetteHeight);"), source);
    }

    [TestMethod]
    public void ReportsDoublePrecisionSupportForEveryOwnedResource()
    {
        string source = RunAndGetSource([GroupSource, HostSource], "HostDoublePrecisionTests", "Ukiyoe.Host");

        Assert.IsTrue(
            source.IndexOf("private readonly struct GridMaterializer") <
            source.IndexOf("public static bool RequiresDoublePrecisionSupport => true;"),
            source);
        Assert.IsTrue(source.Contains("public static bool RequiresDoublePrecisionSupport => false;"), source);
    }

    [TestMethod]
    public void ReportsDoublePrecisionSupportForNestedElementTypes()
    {
        const string NestedSource = """
            using ComputeWeave;

            namespace Ukiyoe;

            public struct Inner
            {
                public double Value;
            }

            public struct Nested
            {
                public int Count;

                public Inner Inner;
            }

            [ComputePipelineHost("device", 1)]
            public sealed partial class NestedHost
            {
                private readonly GraphicsDevice device;

                [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
                private readonly ComputeResourceSlot<ReadWriteBuffer<Nested>> values = new();

                [ComputePipeline]
                private void Run(in ComputeContext context)
                {
                }
            }
            """;

        string source = RunAndGetSource([NestedSource], "HostNestedDoubleTests", "Ukiyoe.NestedHost");

        Assert.IsTrue(source.Contains("public static bool RequiresDoublePrecisionSupport => true;"), source);
        Assert.IsFalse(source.Contains("public static bool RequiresDoublePrecisionSupport => false;"), source);
    }

    [TestMethod]
    public void MatchesTheAccessibilityOfALessAccessibleResourceGroup()
    {
        const string InternalGroupSource = """
            using ComputeWeave;

            namespace Ukiyoe;

            [ComputeResourceGroup]
            internal sealed partial class InternalResources
            {
                [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
                internal ReadWriteBuffer<int> Values { get; }
            }
            """;

        const string InternalGroupHostSource = """
            using ComputeWeave;

            namespace Ukiyoe;

            [ComputePipelineHost("device", 1)]
            public sealed partial class InternalGroupHost
            {
                private readonly GraphicsDevice device;

                [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
                private readonly ComputeResourceGroupSlot<InternalResources> resources = new();

                [ComputePipeline]
                private void Run(in ComputeContext context)
                {
                }
            }
            """;

        string source = RunAndGetSource(
            [InternalGroupSource, InternalGroupHostSource],
            "HostAccessibilityTests",
            "Ukiyoe.InternalGroupHost");

        Assert.IsTrue(
            source.Contains("internal bool TryEnsureResources(in global::Ukiyoe.InternalResources.Plan plan, out bool changed)"),
            source);
    }

    [TestMethod]
    public void EmitsTheInvocationWrapperOfAnInteropPipeline()
    {
        const string InteropHostSource = """
            using ComputeWeave;

            namespace Ukiyoe;

            [ComputePipelineHost("device", 1)]
            public sealed partial class InteropHost
            {
                private readonly GraphicsDevice device;

                [ComputePipeline]
                [ComputeInterop]
                private void Blit(
                    in ComputeContext context,
                    [ComputeResource(ComputeResourceAccess.ReadWrite, Sharing = ComputeResourceSharing.External)] ReadWriteTexture2D<Bgra32, Float4> target)
                {
                }
            }
            """;

        string source = RunAndGetSource([InteropHostSource], "HostInteropInvocationTests", "Ukiyoe.InteropHost");

        Assert.IsTrue(
            source.Contains(
                "public global::ComputeWeave.ComputeSubmission Blit(" +
                "global::ComputeWeave.ComputeResourceBinding<" +
                "global::ComputeWeave.ReadWriteTexture2D<global::ComputeWeave.Bgra32, global::ComputeWeave.Float4>> @target)"),
            source);
        Assert.IsTrue(source.Contains("return this.computeHostRuntime.Submit(new BlitInvocation(this, @target));"), source);
        Assert.IsTrue(source.Contains("private struct BlitInvocation : global::ComputeWeave.IComputePipelineInvocation"), source);
        Assert.IsTrue(
            source.Contains(
                "private global::ComputeWeave.ReadWriteTexture2D<global::ComputeWeave.Bgra32, global::ComputeWeave.Float4> " +
                "@targetBoundResource;"),
            source);
        Assert.IsTrue(source.Contains("if (!binder.TryPin(in this.@target, out this.@targetBoundResource))"), source);
        Assert.IsTrue(source.Contains("this.host.Blit(in context, this.@targetBoundResource);"), source);
    }

    [TestMethod]
    public void EmitsTheRegistrationFactoryForAHostWithoutOwnedSlots()
    {
        const string EmptyHostSource = """
            using ComputeWeave;

            namespace Ukiyoe;

            [ComputePipelineHost("device", 1)]
            public sealed partial class EmptyHost
            {
                private readonly GraphicsDevice device;

                [ComputePipeline]
                private void Run(in ComputeContext context)
                {
                }
            }
            """;

        string source = RunAndGetSource([EmptyHostSource], "HostEmptyTests", "Ukiyoe.EmptyHost");

        Assert.IsTrue(
            source.Contains(
                "this.computeHostRuntime = global::ComputeWeave.ComputeHostRuntime.Create(device, CanonicalDescriptor, maximumPendingSubmissions, []);"),
            source);
        Assert.IsFalse(source.Contains("TryEnsure"), source);
    }
}
