using System.IO;
using ComputeSharp.Graphics.Pipelines;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public class PipelineCanonicalSignatureValidatorTests
{
    private const string Host = "Ukiyoe.Host+Nested";

    private const string Method = "Run";

    private const string Context = "03:ComputeSharp.ComputeContext";

    private static void Validate(string canonicalSignature)
    {
        PipelineCanonicalSignatureValidator.Validate(canonicalSignature, Host, Method);
    }

    private static void AssertRejected(string canonicalSignature)
    {
        _ = Assert.ThrowsException<InvalidDataException>(() => Validate(canonicalSignature));
    }

    [TestMethod]
    public void AcceptsContextOnlySignature()
    {
        Validate($"{Host}|{Method}|00000000|System.Void|00000001|{Context}");
    }

    [TestMethod]
    public void AcceptsEveryKnownRefKind()
    {
        Validate($"{Host}|{Method}|00000000|System.Void|00000005|{Context}|00:System.Int32|01:System.Int32|02:System.Int32|03:System.Int32");
    }

    [TestMethod]
    public void AcceptsConstructedGenericParameterType()
    {
        Validate($"{Host}|{Method}|00000000|System.Void|00000002|{Context}|00:ComputeSharp.ReadWriteBuffer`1[System.Int32]");
    }

    [TestMethod]
    public void AcceptsParameterCountAboveNine()
    {
        Validate(
            $"{Host}|{Method}|00000000|System.Void|0000000A|{Context}" +
            "|00:System.Int32|00:System.Int32|00:System.Int32|00:System.Int32" +
            "|00:System.Int32|00:System.Int32|00:System.Int32|00:System.Int32|00:System.Int32");
    }

    [TestMethod]
    public void RejectsMismatchedContainingType()
    {
        AssertRejected($"Ukiyoe.Other|{Method}|00000000|System.Void|00000001|{Context}");
    }

    [TestMethod]
    public void RejectsMismatchedMethodName()
    {
        AssertRejected($"{Host}|Other|00000000|System.Void|00000001|{Context}");
    }

    [TestMethod]
    public void RejectsNonZeroGenericArity()
    {
        AssertRejected($"{Host}|{Method}|00000001|System.Void|00000001|{Context}");
    }

    [TestMethod]
    public void RejectsNonVoidReturnType()
    {
        AssertRejected($"{Host}|{Method}|00000000|System.Int32|00000001|{Context}");
    }

    [TestMethod]
    public void RejectsZeroParameterCount()
    {
        AssertRejected($"{Host}|{Method}|00000000|System.Void|00000000");
    }

    [TestMethod]
    public void RejectsFirstParameterWithoutContext()
    {
        AssertRejected($"{Host}|{Method}|00000000|System.Void|00000001|03:System.Int32");
        AssertRejected($"{Host}|{Method}|00000000|System.Void|00000001|00:ComputeSharp.ComputeContext");
    }

    [TestMethod]
    public void RejectsUnknownRefKind()
    {
        AssertRejected($"{Host}|{Method}|00000000|System.Void|00000002|{Context}|04:System.Int32");
    }

    [TestMethod]
    public void RejectsLowercaseHexadecimal()
    {
        AssertRejected($"{Host}|{Method}|00000000|System.Void|0000000a|{Context}|00:System.Int32");
        AssertRejected($"{Host}|{Method}|0000000a|System.Void|00000001|{Context}");
    }

    [TestMethod]
    public void RejectsMismatchedParameterSegmentCount()
    {
        AssertRejected($"{Host}|{Method}|00000000|System.Void|00000002|{Context}");
        AssertRejected($"{Host}|{Method}|00000000|System.Void|00000001|{Context}|00:System.Int32");
    }

    [TestMethod]
    public void RejectsMalformedParameterSegment()
    {
        AssertRejected($"{Host}|{Method}|00000000|System.Void|00000001|03ComputeSharp.ComputeContext");
        AssertRejected($"{Host}|{Method}|00000000|System.Void|00000002|{Context}|0:System.Int32");
        AssertRejected($"{Host}|{Method}|00000000|System.Void|00000002|{Context}|00:");
        AssertRejected($"{Host}|{Method}|00000000|System.Void|00000002|{Context}|00:System:Int32");
    }

    [TestMethod]
    public void RejectsMalformedComponentLayout()
    {
        AssertRejected("");
        AssertRejected($"{Host}|{Method}|00000000|System.Void|00000001|{Context}|");
        AssertRejected($"{Host}||00000000|System.Void|00000001|{Context}");
        AssertRejected($"{Host}|{Method}|000000000|System.Void|00000001|{Context}");
    }

    [TestMethod]
    public void RejectsReservedCharacterInLeadingComponents()
    {
        PipelineCanonicalSignatureValidator.Validate(
            $"{Host}|{Method}|00000000|System.Void|00000001|{Context}",
            Host,
            Method);

        _ = Assert.ThrowsException<InvalidDataException>(
            () => PipelineCanonicalSignatureValidator.Validate(
                $"Ukiyoe:Host|{Method}|00000000|System.Void|00000001|{Context}",
                "Ukiyoe:Host",
                Method));
    }
}
