using ComputeSharp.SourceGenerators;
using ComputeSharp.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.SourceGenerators.Canonicalization;

[TestClass]
public class InteropResourceSetEmissionTests
{
    private const string ResourceSetSource = """
        using System;
        using ComputeSharp;

        namespace Ukiyoe;

        internal sealed class UkiyoeExternalTextureView : IDisposable
        {
            public void Dispose()
            {
            }
        }

        [ComputeInteropResourceSet]
        public sealed partial class UkiyoeInteropResources
        {
            [ComputeSharedTexture(
                ComputeResourceResizePolicy.Exact,
                ComputeResourceAccess.ReadWrite,
                ExternalResourceAccess.Write,
                ExternalTextureUsage.RenderTarget,
                ComputeAlphaMode.Premultiplied,
                ComputeSharedTextureInitialOwner.External,
                ComputeResourceRecovery.RecreateFromHost)]
            private readonly SharedTextureSlot<Bgra32, Float4, UkiyoeExternalTextureView> _source;

            [ComputeSharedTexture(
                ComputeResourceResizePolicy.GrowOnly,
                ComputeResourceAccess.ReadWrite,
                ExternalResourceAccess.Read,
                ExternalTextureUsage.Sampled,
                ComputeAlphaMode.Premultiplied,
                ComputeSharedTextureInitialOwner.Compute,
                ComputeResourceRecovery.Recompute)]
            private readonly SharedTextureSlot<Bgra32, Float4, UkiyoeExternalTextureView> _output;
        }
        """;

    private const string SlotTypeName =
        "global::ComputeSharp.SharedTextureSlot<global::ComputeSharp.Bgra32, global::ComputeSharp.Float4, " +
        "global::Ukiyoe.UkiyoeExternalTextureView>";

    private static string RunAndGetSource(string assemblyName)
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation([ResourceSetSource], assemblyName);
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new PipelineDescriptorGenerator());

        return GeneratorHelper.GetGeneratedSource(GeneratorHelper.Run(driver, compilation, out _), "Ukiyoe.UkiyoeInteropResources");
    }

    [TestMethod]
    public void EmitsTheRegistrationFactoryInSlotOrdinalOrder()
    {
        string source = RunAndGetSource("ResourceSetFactoryTests");

        Assert.IsTrue(
            source.Contains("private readonly global::ComputeSharp.ComputeInteropResourceSetRuntime computeInteropResourceSetRuntime;"),
            source);
        Assert.IsTrue(
            source.Contains(
                "private UkiyoeInteropResources(global::ComputeSharp.GraphicsDevice device, " +
                "global::ComputeSharp.ComputeInteropDomain domain)"),
            source);
        Assert.IsTrue(source.Contains($"this.@_output = new {SlotTypeName}();"), source);
        Assert.IsTrue(source.Contains($"this.@_source = new {SlotTypeName}();"), source);
        Assert.IsTrue(
            source.IndexOf($"this.@_output = new {SlotTypeName}();") <
            source.IndexOf($"this.@_source = new {SlotTypeName}();"),
            source);
        Assert.IsTrue(
            source.Contains(
                "this.computeInteropResourceSetRuntime = global::ComputeSharp.ComputeInteropResourceSetRuntime.Create(" +
                "device, domain, CanonicalDescriptor, [this.@_output, this.@_source]);"),
            source);
        Assert.IsTrue(
            source.Contains(
                "public static UkiyoeInteropResources Create(global::ComputeSharp.GraphicsDevice device, " +
                "global::ComputeSharp.ComputeInteropDomain domain)"),
            source);
        Assert.IsTrue(source.Contains("return new UkiyoeInteropResources(device, domain);"), source);
    }

    [TestMethod]
    public void EmitsTheDisposalMembers()
    {
        string source = RunAndGetSource("ResourceSetDisposalTests");

        Assert.IsTrue(source.Contains("partial class UkiyoeInteropResources : global::System.IDisposable"), source);
        Assert.IsTrue(source.Contains("this.computeInteropResourceSetRuntime.Dispose();"), source);
        Assert.IsTrue(source.Contains("this.@_output.Dispose();"), source);
        Assert.IsTrue(source.Contains("this.@_source.Dispose();"), source);
        Assert.IsTrue(source.Contains("this.computeInteropResourceSetRuntime.WaitForDisposal();"), source);
    }

    [TestMethod]
    public void EmitsTypedPlanMethodsDelegatingToEverySharedSlot()
    {
        string source = RunAndGetSource("ResourceSetPlanMethodTests");

        Assert.IsTrue(source.Contains("public bool TryEnsureSource(int width, int height, out bool changed)"), source);
        Assert.IsTrue(source.Contains("return this.@_source.TryEnsure(width, height, out changed);"), source);
        Assert.IsTrue(source.Contains("public bool TryEnsureOutput(int width, int height, out bool changed)"), source);
        Assert.IsTrue(source.Contains("return this.@_output.TryEnsure(width, height, out changed);"), source);
    }

    [TestMethod]
    public void EmitsComputeBindingAccessorsForEverySharedSlot()
    {
        string source = RunAndGetSource("ResourceSetBindingTests");

        Assert.IsTrue(
            source.Contains(
                "public global::ComputeSharp.ComputeResourceBinding<global::ComputeSharp.ReadWriteTexture2D<" +
                "global::ComputeSharp.Bgra32, global::ComputeSharp.Float4>> GetSourceComputeBinding()"),
            source);
        Assert.IsTrue(source.Contains("return this.@_source.GetComputeBinding();"), source);
        Assert.IsTrue(source.Contains("GetOutputComputeBinding()"), source);
        Assert.IsTrue(source.Contains("return this.@_output.GetComputeBinding();"), source);
    }

    [TestMethod]
    public void EmitsTheExternalViewMembersWithTheAccessibilityOfTheViewType()
    {
        string source = RunAndGetSource("ResourceSetExternalViewTests");

        Assert.IsTrue(
            source.Contains(
                "internal global::ComputeSharp.BorrowedExternalTextureView<global::Ukiyoe.UkiyoeExternalTextureView> " +
                "BeginSourceExternalOperation()"),
            source);
        Assert.IsTrue(source.Contains("return this.@_source.BeginExternalOperation();"), source);
        Assert.IsTrue(
            source.Contains(
                "internal global::ComputeSharp.ExternalTextureLease<global::Ukiyoe.UkiyoeExternalTextureView> " +
                "AcquireOutputExternalViewLease()"),
            source);
        Assert.IsTrue(source.Contains("return this.@_output.AcquireExternalViewLease();"), source);
    }
}
