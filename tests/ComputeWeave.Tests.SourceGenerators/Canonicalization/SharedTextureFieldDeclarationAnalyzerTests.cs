using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Canonicalization;

[TestClass]
public class SharedTextureFieldDeclarationAnalyzerTests
{
    private static string ResourceSet(string field)
    {
        return $$"""
            using System;
            using ComputeWeave;

            namespace Ukiyoe;

            public sealed class ExternalView : IDisposable
            {
                public void Dispose()
                {
                }
            }

            [ComputeInteropResourceSet]
            public sealed partial class ResourceSet
            {
            {{field}}
            }
            """;
    }

    private const string Attribute = """
                [ComputeSharedTexture(
                    ComputeResourceResizePolicy.Exact,
                    ComputeResourceAccess.ReadWrite,
                    ExternalResourceAccess.Write,
                    ExternalTextureUsage.RenderTarget,
                    ComputeAlphaMode.Premultiplied,
                    ComputeSharedTextureInitialOwner.External,
                    ComputeResourceRecovery.RecreateFromHost)]
        """;

    [TestMethod]
    public void AcceptsAPrivateReadOnlySlotFieldWithoutAnInitializer()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidComputeSharedTextureFieldDeclarationAnalyzer(),
            [ResourceSet($$"""
                {{Attribute}}
                    private readonly SharedTextureSlot<Bgra32, Float4, ExternalView> source;
                """)],
            "SharedTextureFieldDeclarationAnalyzerTests");
    }

    [TestMethod]
    public void RejectsASlotFieldWithAnInitializer()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidComputeSharedTextureFieldDeclarationAnalyzer(),
            [ResourceSet($$"""
                {{Attribute}}
                    private readonly SharedTextureSlot<Bgra32, Float4, ExternalView> source = new();
                """)],
            "SharedTextureFieldDeclarationAnalyzerTests",
            "CMPW0109");
    }

    [TestMethod]
    public void RejectsASlotFieldThatIsNotPrivate()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidComputeSharedTextureFieldDeclarationAnalyzer(),
            [ResourceSet($$"""
                {{Attribute}}
                    internal readonly SharedTextureSlot<Bgra32, Float4, ExternalView> source;
                """)],
            "SharedTextureFieldDeclarationAnalyzerTests",
            "CMPW0109");
    }

    [TestMethod]
    public void RejectsASlotFieldThatIsNotReadOnly()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidComputeSharedTextureFieldDeclarationAnalyzer(),
            [ResourceSet($$"""
                {{Attribute}}
                    private SharedTextureSlot<Bgra32, Float4, ExternalView> source;
                """)],
            "SharedTextureFieldDeclarationAnalyzerTests",
            "CMPW0109");
    }

    [TestMethod]
    public void RejectsASlotFieldThatIsStatic()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidComputeSharedTextureFieldDeclarationAnalyzer(),
            [ResourceSet($$"""
                {{Attribute}}
                    private static readonly SharedTextureSlot<Bgra32, Float4, ExternalView> source;
                """)],
            "SharedTextureFieldDeclarationAnalyzerTests",
            "CMPW0109");
    }

    [TestMethod]
    public void RejectsAFieldThatIsNotASharedTextureSlot()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidComputeSharedTextureFieldDeclarationAnalyzer(),
            [ResourceSet($$"""
                {{Attribute}}
                    private readonly ReadWriteTexture2D<Bgra32, Float4> source;
                """)],
            "SharedTextureFieldDeclarationAnalyzerTests",
            "CMPW0109");
    }
}
