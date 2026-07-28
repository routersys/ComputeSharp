using ComputeSharp.SourceGenerators;
using ComputeSharp.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.SourceGenerators.Canonicalization;

[TestClass]
public class PipelineHostEmissionTests
{
    private const string GroupSource = """
        using ComputeSharp;

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
        using ComputeSharp;

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
        using ComputeSharp;

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

        Assert.IsTrue(source.Contains("private readonly global::ComputeSharp.ComputeHostRuntime computeHostRuntime;"), source);
        Assert.IsTrue(source.Contains("private Host(global::ComputeSharp.GraphicsDevice device, int maximumPendingSubmissions)"), source);
        Assert.IsTrue(source.Contains("this.@device = device;"), source);
        Assert.IsTrue(
            source.Contains(
                "this.computeHostRuntime = global::ComputeSharp.ComputeHostRuntime.Create(device, CanonicalDescriptor, " +
                "maximumPendingSubmissions, [this.@grid, this.@index, this.@silhouette]);"),
            source);
        Assert.IsTrue(source.Contains("public static Host Create(global::ComputeSharp.GraphicsDevice device, int maximumPendingSubmissions)"), source);
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
            source.Contains("public global::ComputeSharp.ComputeSubmission Run(global::ComputeSharp.ReadWriteBuffer<int> @source)"),
            source);
        Assert.IsTrue(source.Contains("return this.computeHostRuntime.Submit(new RunInvocation(this, @source));"), source);
        Assert.IsTrue(source.Contains("private readonly struct RunInvocation : global::ComputeSharp.IComputePipelineInvocation"), source);
        Assert.IsTrue(source.Contains("public static int PipelineOrdinal => 0;"), source);
        Assert.IsTrue(source.Contains("if (!binder.TryPin(this.@source))"), source);
        Assert.IsTrue(
            source.Contains(
                "global::ComputeSharp.ComputeResourceBinding<global::ComputeSharp.ReadWriteBuffer<double>> binding1 = " +
                "this.host.computeHostRuntime.GetBinding<global::ComputeSharp.ReadWriteBuffer<double>>(0, 0);"),
            source);
        Assert.IsTrue(source.Contains("if (!binder.TryPin(0, in binding1))"), source);
        Assert.IsTrue(source.Contains("this.host.Run(in context, this.@source);"), source);
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
                "public global::ComputeSharp.ComputeResourceBinding<global::ComputeSharp.ReadWriteBuffer<int>> GetIndexComputeBinding()"),
            source);
        Assert.IsTrue(
            source.Contains("return this.computeHostRuntime.GetBinding<global::ComputeSharp.ReadWriteBuffer<int>>(1, 0);"),
            source);
        Assert.IsTrue(
            source.Contains(
                "public global::ComputeSharp.ComputeResourceBinding<global::ComputeSharp.ReadWriteTexture2D<float>> GetSilhouetteComputeBinding()"),
            source);
        Assert.IsFalse(source.Contains("GetGridComputeBinding"), source);
    }

    [TestMethod]
    public void EmitsMaterializerDeclarationsInSlotResourceIndexOrder()
    {
        string source = RunAndGetSource([GroupSource, HostSource], "HostMaterializerTests", "Ukiyoe.Host");

        Assert.IsTrue(source.Contains("private readonly struct GridMaterializer : global::ComputeSharp.IComputeGenerationMaterializer"), source);
        Assert.IsTrue(source.Contains("public GridMaterializer(int colorInLength, int maskWidth, int maskHeight)"), source);
        Assert.IsTrue(source.Contains("context.DeclareBuffer<double>(this.colorInLength);"), source);
        Assert.IsTrue(
            source.Contains(
                "context.DeclareTexture2D<global::ComputeSharp.Bgra32, global::ComputeSharp.Float4>(this.maskWidth, this.maskHeight);"),
            source);
        Assert.IsTrue(
            source.IndexOf("context.DeclareBuffer<double>(this.colorInLength);") <
            source.IndexOf("context.DeclareTexture2D<global::ComputeSharp.Bgra32, global::ComputeSharp.Float4>(this.maskWidth, this.maskHeight);"),
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
            using ComputeSharp;

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
            using ComputeSharp;

            namespace Ukiyoe;

            [ComputeResourceGroup]
            internal sealed partial class InternalResources
            {
                [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
                internal ReadWriteBuffer<int> Values { get; }
            }
            """;

        const string InternalGroupHostSource = """
            using ComputeSharp;

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
            using ComputeSharp;

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
                "public global::ComputeSharp.ComputeSubmission Blit(" +
                "global::ComputeSharp.ReadWriteTexture2D<global::ComputeSharp.Bgra32, global::ComputeSharp.Float4> @target)"),
            source);
        Assert.IsTrue(source.Contains("return this.computeHostRuntime.Submit(new BlitInvocation(this, @target));"), source);
        Assert.IsTrue(source.Contains("if (!binder.TryPin(this.@target))"), source);
        Assert.IsTrue(source.Contains("this.host.Blit(in context, this.@target);"), source);
    }

    [TestMethod]
    public void EmitsTheRegistrationFactoryForAHostWithoutOwnedSlots()
    {
        const string EmptyHostSource = """
            using ComputeSharp;

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
                "this.computeHostRuntime = global::ComputeSharp.ComputeHostRuntime.Create(device, CanonicalDescriptor, maximumPendingSubmissions, []);"),
            source);
        Assert.IsFalse(source.Contains("TryEnsure"), source);
    }
}
