using ComputeSharp.SourceGenerators;
using ComputeSharp.Tests.SourceGenerators.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.SourceGenerators.Canonicalization;

[TestClass]
public class RawExternalViewEscapeAnalyzerTests
{
    private static string Container(string members)
    {
        return $$"""
            using System;
            using ComputeSharp;

            namespace Ukiyoe;

            public sealed class FakeView : IDisposable
            {
                public void Dispose()
                {
                }
            }

            public sealed class Container
            {
                private FakeView? stored;

            {{members}}
            }
            """;
    }

    [TestMethod]
    public void AcceptsARawViewBoundToALocal()
    {
        AnalyzerHelper.AssertDiagnostics(
            new RawExternalViewEscapeAnalyzer(),
            [Container("""
                    public void Use(ExternalTextureLease<FakeView> lease)
                    {
                        FakeView view = lease.DangerousGetView();

                        view.Dispose();
                    }
                """)],
            "AcceptsLocalBinding");
    }

    [TestMethod]
    public void DetectsARawViewStoredInAField()
    {
        AnalyzerHelper.AssertDiagnostics(
            new RawExternalViewEscapeAnalyzer(),
            [Container("""
                    public void Use(ExternalTextureLease<FakeView> lease)
                    {
                        this.stored = lease.DangerousGetView();
                    }
                """)],
            "DetectsFieldStore",
            "CMPS0096");
    }

    [TestMethod]
    public void DetectsARawViewBeingReturned()
    {
        AnalyzerHelper.AssertDiagnostics(
            new RawExternalViewEscapeAnalyzer(),
            [Container("""
                    public FakeView Use(ExternalTextureLease<FakeView> lease)
                    {
                        return lease.DangerousGetView();
                    }
                """)],
            "DetectsReturn",
            "CMPS0096");
    }

    [TestMethod]
    public void AcceptsARawViewBeingDiscarded()
    {
        AnalyzerHelper.AssertDiagnostics(
            new RawExternalViewEscapeAnalyzer(),
            [Container("""
                    public void Use(ExternalTextureLease<FakeView> lease)
                    {
                        _ = lease.DangerousGetView();
                    }
                """)],
            "AcceptsDiscard");
    }
}
