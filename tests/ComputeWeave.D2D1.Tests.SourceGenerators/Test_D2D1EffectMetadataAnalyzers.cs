using System.Threading.Tasks;
using ComputeWeave.D2D1.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.D2D1.Tests.SourceGenerators;

/// <summary>
/// The effect metadata an author writes on a shader type, and what each attribute refuses.
/// </summary>
/// <remarks>
/// The metadata reaches a consumer as the registration blob of the effect, so a value the parser cannot use
/// is caught here rather than at registration. One analyzer serves four of the attributes and keys the
/// identifier off the attribute type, so a row is needed for each of the four: a mapping that lost an entry
/// would leave the other three reporting.
/// </remarks>
[TestClass]
public class Test_D2D1EffectMetadataAnalyzers
{
    [TestMethod]
    public async Task InvalidD2DEffectIdAttributeValue_NotAGuid()
    {
        const string source = """
            using ComputeWeave.D2D1;

            [{|CMPWD2D0059:D2DEffectId("not a guid")|}]
            internal partial struct MyType
            {
            }
            """;

        await CSharpAnalyzerTest<InvalidEffectIdValueAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task InvalidD2DEffectIdAttributeValue_ValidGuid_DoesNotWarn()
    {
        const string source = """
            using ComputeWeave.D2D1;

            [D2DEffectId("6A2E4F9C-3D1B-4E8A-9C7F-25B0A1D6E3F4")]
            internal partial struct MyType
            {
            }
            """;

        await CSharpAnalyzerTest<InvalidEffectIdValueAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    [DataRow("D2DEffectDisplayName", "CMPWD2D0060")]
    [DataRow("D2DEffectDescription", "CMPWD2D0061")]
    [DataRow("D2DEffectCategory", "CMPWD2D0062")]
    [DataRow("D2DEffectAuthor", "CMPWD2D0063")]
    public async Task InvalidD2DEffectMetadataAttributeValue_BlankValue(string attribute, string diagnosticId)
    {
        string source = $$"""
            using ComputeWeave.D2D1;

            [{|{{diagnosticId}}:{{attribute}}("   ")|}]
            internal partial struct MyType
            {
            }
            """;

        await CSharpAnalyzerTest<InvalidEffectMetadataValueAnalyzer>.VerifyAnalyzerAsync(source);
    }

    /// <summary>
    /// The control. A value the parser can use has to leave all four attributes silent.
    /// </summary>
    [TestMethod]
    [DataRow("D2DEffectDisplayName")]
    [DataRow("D2DEffectDescription")]
    [DataRow("D2DEffectCategory")]
    [DataRow("D2DEffectAuthor")]
    public async Task ValidD2DEffectMetadataAttributeValue_DoesNotWarn(string attribute)
    {
        string source = $$"""
            using ComputeWeave.D2D1;

            [{{attribute}}("Name")]
            internal partial struct MyType
            {
            }
            """;

        await CSharpAnalyzerTest<InvalidEffectMetadataValueAnalyzer>.VerifyAnalyzerAsync(source);
    }
}
