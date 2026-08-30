using System.Linq;
using ComputeWeave.D2D1.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.D2D1.Tests.SourceGenerators;

/// <summary>
/// The suppression that keeps a resource texture field of a pixel shader from being reported as unassigned.
/// </summary>
/// <remarks>
/// <para>
/// A resource texture field is never assigned in source, and the generated descriptor is what binds it, so
/// the compiler warning that says nothing writes to it is wrong for this one shape. The suppression is what
/// makes an author's shader compile, and its identifier is what a reader sees when asking why the warning
/// went away.
/// </para>
/// <para>
/// That the suppression happens is held by the solution build rather than by a test. A shader type in the
/// Direct2D test project declares such a field, and this repository turns warnings into errors, so a
/// suppression that stopped working fails the build. A test of its own is not possible here: the testing
/// package cannot read the suppression information of the Roslyn version this repository uses, and asking
/// it to do so throws while initializing its own type.
/// </para>
/// </remarks>
[TestClass]
public class Test_D2D1ResourceTextureUninitializedFieldSuppressor
{
    [TestMethod]
    public void DeclaresTheShippedSuppressionIdentifier()
    {
        SuppressionDescriptor descriptor = new D2D1ResourceTextureUninitializedFieldDiagnosticSuppressor()
            .SupportedSuppressions
            .Single();

        Assert.AreEqual("CMPWD2DSPR0001", descriptor.Id);
        Assert.AreEqual("CS0649", descriptor.SuppressedDiagnosticId);
    }
}
