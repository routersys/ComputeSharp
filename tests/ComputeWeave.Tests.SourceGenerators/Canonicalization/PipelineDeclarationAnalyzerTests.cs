using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Canonicalization;

/// <summary>
/// The analyzers that check how a pipeline host and its members are declared, none of which had a test.
/// </summary>
/// <remarks>
/// Each of these reports an error, so an author meets them before anything is generated. The danger runs one
/// way: an analyzer that started reporting where it should not would break the build of this repository, while
/// one that stopped reporting breaks nothing here, no host in this repository being declared the wrong way.
/// Every method below builds the declaration the analyzer exists to refuse, and the last one shows that all of
/// them stay quiet on a host that is declared correctly.
/// </remarks>
[TestClass]
public class PipelineDeclarationAnalyzerTests
{
    /// <summary>
    /// A host declared the way the ones in this repository are, for the negative case.
    /// </summary>
    private const string WellFormedSource = """
        using ComputeWeave;

        namespace Ukiyoe;

        [ComputePipelineHost("device", 1)]
        public sealed partial class Host
        {
            private readonly GraphicsDevice device;

            [ComputePipeline]
            private void Run(in ComputeContext context)
            {
            }

            [ComputeInterop]
            private void Share(in ComputeContext context, [ComputeResource(ComputeResourceAccess.ReadWrite, Sharing = ComputeResourceSharing.External)] ReadWriteBuffer<int> buffer)
            {
            }
        }
        """;

    [TestMethod]
    public void CopyingTheComputeContextIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Ukiyoe;

            [ComputePipelineHost("device", 1)]
            public sealed partial class Host
            {
                private readonly GraphicsDevice device;

                [ComputePipeline]
                private void Run(in ComputeContext context)
                {
                    ComputeContext copy = context;
                }
            }
            """;

        AnalyzerHelper.AssertDiagnostics(
            new InvalidComputeContextCopyAnalyzer(),
            [Source],
            "PipelineContextCopyAnalyzerTests",
            "CMPW0051");
    }

    [TestMethod]
    public void AHostThatIsNotSealedIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Ukiyoe;

            [ComputePipelineHost("device", 1)]
            public partial class Host
            {
                private readonly GraphicsDevice device;
            }
            """;

        AnalyzerHelper.AssertDiagnostics(
            new InvalidComputePipelineContainerTypeDeclarationAnalyzer(),
            [Source],
            "PipelineHostNotSealedAnalyzerTests",
            "CMPW0066");
    }

    [TestMethod]
    public void AResourceSetThatIsNotSealedIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Ukiyoe;

            [ComputeInteropResourceSet]
            public partial class ResourceSet
            {
            }
            """;

        AnalyzerHelper.AssertDiagnostics(
            new InvalidComputePipelineContainerTypeDeclarationAnalyzer(),
            [Source],
            "PipelineResourceSetNotSealedAnalyzerTests",
            "CMPW0074");
    }

    [TestMethod]
    public void AResourceGroupThatIsNotSealedIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Ukiyoe;

            [ComputeResourceGroup]
            public partial class Group
            {
            }
            """;

        AnalyzerHelper.AssertDiagnostics(
            new InvalidComputePipelineContainerTypeDeclarationAnalyzer(),
            [Source],
            "PipelineResourceGroupNotSealedAnalyzerTests",
            "CMPW0100");
    }

    [TestMethod]
    public void AGenericHostIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Ukiyoe;

            [ComputePipelineHost("device", 1)]
            public sealed partial class Host<T>
            {
                private readonly GraphicsDevice device;
            }
            """;

        AnalyzerHelper.AssertDiagnostics(
            new InvalidComputePipelineContainerTypeDeclarationAnalyzer(),
            [Source],
            "PipelineGenericHostAnalyzerTests",
            "CMPW0106");
    }

    [TestMethod]
    public void AHostThatAllowsNoInvocationIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Ukiyoe;

            [ComputePipelineHost("device", 0)]
            public sealed partial class Host
            {
                private readonly GraphicsDevice device;
            }
            """;

        AnalyzerHelper.AssertDiagnostics(
            new InvalidComputePipelineContainerTypeDeclarationAnalyzer(),
            [Source],
            "PipelineNoInvocationAnalyzerTests",
            "CMPW0068");
    }

    [TestMethod]
    public void AHostNamingADeviceFieldItDoesNotHaveIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Ukiyoe;

            [ComputePipelineHost("missing", 1)]
            public sealed partial class Host
            {
                private readonly GraphicsDevice device;
            }
            """;

        AnalyzerHelper.AssertDiagnostics(
            new InvalidComputePipelineHostDeviceFieldAnalyzer(),
            [Source],
            "PipelineDeviceFieldAnalyzerTests",
            "CMPW0067");
    }

    [TestMethod]
    public void APipelineMethodThatIsStaticIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Ukiyoe;

            [ComputePipelineHost("device", 1)]
            public sealed partial class Host
            {
                private readonly GraphicsDevice device;

                [ComputePipeline]
                private static void Run(in ComputeContext context)
                {
                }
            }
            """;

        AnalyzerHelper.AssertDiagnostics(
            new InvalidComputePipelineMethodDeclarationAnalyzer(),
            [Source],
            "PipelineStaticMethodAnalyzerTests",
            "CMPW0091");
    }

    [TestMethod]
    public void APipelineMethodTakingTheContextByValueIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Ukiyoe;

            [ComputePipelineHost("device", 1)]
            public sealed partial class Host
            {
                private readonly GraphicsDevice device;

                [ComputePipeline]
                private void Run(ComputeContext context)
                {
                }
            }
            """;

        AnalyzerHelper.AssertDiagnostics(
            new InvalidComputePipelineMethodDeclarationAnalyzer(),
            [Source],
            "PipelineContextByValueAnalyzerTests",
            "CMPW0069");
    }

    [TestMethod]
    public void AResourceParameterWithoutItsAttributeIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Ukiyoe;

            [ComputePipelineHost("device", 1)]
            public sealed partial class Host
            {
                private readonly GraphicsDevice device;

                [ComputePipeline]
                private void Run(in ComputeContext context, ReadWriteBuffer<int> buffer)
                {
                }
            }
            """;

        AnalyzerHelper.AssertDiagnostics(
            new MissingComputeResourceAttributeAnalyzer(),
            [Source],
            "PipelineResourceAttributeAnalyzerTests",
            "CMPW0070");
    }

    [TestMethod]
    public void AnInteropMethodWithoutAnExternalResourceIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Ukiyoe;

            [ComputePipelineHost("device", 1)]
            public sealed partial class Host
            {
                private readonly GraphicsDevice device;

                [ComputeInterop]
                private void Share(in ComputeContext context)
                {
                }
            }
            """;

        AnalyzerHelper.AssertDiagnostics(
            new MissingExternalComputeResourceInInteropMethodAnalyzer(),
            [Source],
            "PipelineInteropResourceAnalyzerTests",
            "CMPW0072");
    }

    [TestMethod]
    public void AHostDeclaringAMemberTheGeneratorWritesIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Ukiyoe;

            [ComputePipelineHost("device", 1)]
            public sealed partial class Host
            {
                private readonly GraphicsDevice device;

                public Host()
                {
                }
            }
            """;

        AnalyzerHelper.AssertDiagnostics(
            new InvalidGeneratedLifecycleMemberDeclarationAnalyzer(),
            [Source],
            "PipelineLifecycleMemberAnalyzerTests",
            "CMPW0095");
    }

    [TestMethod]
    public void EveryAnalyzerIsQuietOnAWellFormedHost()
    {
        // Without this, each test above would pass for an analyzer that reported on everything it saw
        DiagnosticAnalyzer[] analyzers =
        [
            new InvalidComputeContextCopyAnalyzer(),
            new InvalidComputePipelineContainerTypeDeclarationAnalyzer(),
            new InvalidComputePipelineHostDeviceFieldAnalyzer(),
            new InvalidComputePipelineMethodDeclarationAnalyzer(),
            new InvalidGeneratedLifecycleMemberDeclarationAnalyzer(),
            new MissingComputeResourceAttributeAnalyzer(),
            new MissingExternalComputeResourceInInteropMethodAnalyzer()
        ];

        foreach (DiagnosticAnalyzer analyzer in analyzers)
        {
            AnalyzerHelper.AssertDiagnostics(analyzer, [WellFormedSource], "PipelineWellFormed" + analyzer.GetType().Name);
        }
    }
}
