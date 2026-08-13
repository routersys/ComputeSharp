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

    private static void AssertGeneratedMemberConflict(string member, string testName)
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidGeneratedPlanSignatureAnalyzer(),
            [ResourceSet($$"""
                {{Attribute}}
                    private readonly SharedTextureSlot<Bgra32, Float4, ExternalView> source;

                    public void {{member}}()
                    {
                    }
                """)],
            testName,
            "CMPW0104");
    }

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

    [TestMethod]
    public void AcceptsSharedTextureSlotsWithDistinctCanonicalNames()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidGeneratedPlanSignatureAnalyzer(),
            [ResourceSet($$"""
                {{Attribute}}
                    private readonly SharedTextureSlot<Bgra32, Float4, ExternalView> source;

                {{Attribute}}
                    private readonly SharedTextureSlot<Bgra32, Float4, ExternalView> output;
                """)],
            "AcceptsDistinctSharedTextureCanonicalNames");
    }

    [TestMethod]
    public void RejectsASharedTextureSlotWithoutACanonicalName()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidGeneratedPlanSignatureAnalyzer(),
            [ResourceSet($$"""
                {{Attribute}}
                    private readonly SharedTextureSlot<Bgra32, Float4, ExternalView> _;
                """)],
            "RejectsEmptySharedTextureCanonicalName",
            "CMPW0104");
    }

    [TestMethod]
    public void RejectsSharedTextureSlotsSharingACanonicalName()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidGeneratedPlanSignatureAnalyzer(),
            [ResourceSet($$"""
                {{Attribute}}
                    private readonly SharedTextureSlot<Bgra32, Float4, ExternalView> source;

                {{Attribute}}
                    private readonly SharedTextureSlot<Bgra32, Float4, ExternalView> _source;
                """)],
            "RejectsSharedTextureCanonicalName",
            "CMPW0104",
            "CMPW0104");
    }

    [TestMethod]
    public void RejectsASharedTextureSlotConflictingWithTryEnsure()
    {
        AssertGeneratedMemberConflict("TryEnsureSource", "RejectsDeclaredTryEnsure");
    }

    [TestMethod]
    public void RejectsASharedTextureSlotConflictingWithTryGetAllocatedSize()
    {
        AssertGeneratedMemberConflict("TryGetSourceAllocatedSize", "RejectsDeclaredTryGetAllocatedSize");
    }

    [TestMethod]
    public void RejectsASharedTextureSlotConflictingWithGetComputeBinding()
    {
        AssertGeneratedMemberConflict("GetSourceComputeBinding", "RejectsDeclaredGetComputeBinding");
    }

    [TestMethod]
    public void RejectsASharedTextureSlotConflictingWithBeginExternalOperation()
    {
        AssertGeneratedMemberConflict("BeginSourceExternalOperation", "RejectsDeclaredBeginExternalOperation");
    }

    [TestMethod]
    public void RejectsASharedTextureSlotConflictingWithAcquireExternalViewLease()
    {
        AssertGeneratedMemberConflict("AcquireSourceExternalViewLease", "RejectsDeclaredAcquireExternalViewLease");
    }
}
