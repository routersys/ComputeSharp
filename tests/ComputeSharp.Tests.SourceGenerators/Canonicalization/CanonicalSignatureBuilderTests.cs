using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using ComputeSharp.SourceGenerators.Helpers;
using ComputeSharp.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.SourceGenerators.Canonicalization;

[TestClass]
public class CanonicalSignatureBuilderTests
{
    private const string Source = """
        #nullable enable

        using ComputeSharp;

        namespace Ukiyoe;

        public class Outer<TOuter>
        {
            public class Inner<TInner>
            {
            }
        }

        public class Host
        {
            public class Nested
            {
                public void ContextOnly(in ComputeContext context)
                {
                }

                public void Overloaded(in ComputeContext context, int value)
                {
                }

                public void Overloaded(in ComputeContext context, float value)
                {
                }

                public void RefKinds(in ComputeContext context, int none, ref int byRef, out int byOut, in int byIn)
                {
                    byOut = 0;
                }

                public void TenParameters(
                    in ComputeContext context,
                    int a,
                    int b,
                    int c,
                    int d,
                    int e,
                    int f,
                    int g,
                    int h,
                    int i)
                {
                }

                public void ConstructedGeneric(in ComputeContext context, ReadWriteBuffer<int> buffer)
                {
                }

                public void NestedConstructedGeneric(in ComputeContext context, Outer<int>.Inner<string> value)
                {
                }

                public void SingleDimensionalArray(in ComputeContext context, int[] values)
                {
                }

                public void TwoDimensionalArray(in ComputeContext context, int[,] values)
                {
                }

                public void NonNullableReference(in ComputeContext context, string value)
                {
                }

                public void NullableReference(in ComputeContext context, string? value)
                {
                }

                public void NamedTuple(in ComputeContext context, (int X, string Y) value)
                {
                }

                public void RenamedTuple(in ComputeContext context, (int A, string B) value)
                {
                }
            }
        }
        """;

    private const string DecomposedSource = """
        using ComputeSharp;

        namespace Ukiyoe;

        public class DecomposedTypeName
        {
            public void Run(in ComputeContext context)
            {
            }
        }
        """;

    private const string HostName = "Ukiyoe.Host+Nested";

    private const string Context = "03:ComputeSharp.ComputeContext";

    private static string Signature(string methodName)
    {
        ImmutableArray<IMethodSymbol> methodSymbols = SymbolHelper.GetMethods(Source, HostName, methodName, "CanonicalSignatureBuilderTests");

        Assert.AreEqual(1, methodSymbols.Length);
        Assert.IsTrue(CanonicalSignatureBuilder.TryGetCanonicalSignature(methodSymbols[0], out string signature));

        return signature;
    }

    private static HashSet<string> Signatures(string methodName)
    {
        HashSet<string> signatures = [];

        foreach (IMethodSymbol methodSymbol in SymbolHelper.GetMethods(Source, HostName, methodName, "CanonicalSignatureBuilderTests"))
        {
            Assert.IsTrue(CanonicalSignatureBuilder.TryGetCanonicalSignature(methodSymbol, out string signature));
            Assert.IsTrue(signatures.Add(signature), signature);
        }

        return signatures;
    }

    [TestMethod]
    public void EncodesContextOnlyMethod()
    {
        Assert.AreEqual($"{HostName}|ContextOnly|00000000|System.Void|00000001|{Context}", Signature("ContextOnly"));
    }

    [TestMethod]
    public void EncodesEveryKnownRefKind()
    {
        Assert.AreEqual(
            $"{HostName}|RefKinds|00000000|System.Void|00000005|{Context}|00:System.Int32|01:System.Int32|02:System.Int32|03:System.Int32",
            Signature("RefKinds"));
    }

    [TestMethod]
    public void EncodesParameterCountAboveNine()
    {
        Assert.AreEqual(
            $"{HostName}|TenParameters|00000000|System.Void|0000000A|{Context}" +
            "|00:System.Int32|00:System.Int32|00:System.Int32|00:System.Int32|00:System.Int32" +
            "|00:System.Int32|00:System.Int32|00:System.Int32|00:System.Int32",
            Signature("TenParameters"));
    }

    [TestMethod]
    public void DistinguishesOverloadsByParameterType()
    {
        HashSet<string> signatures = Signatures("Overloaded");

        Assert.AreEqual(2, signatures.Count);
        Assert.IsTrue(signatures.Contains($"{HostName}|Overloaded|00000000|System.Void|00000002|{Context}|00:System.Int32"));
        Assert.IsTrue(signatures.Contains($"{HostName}|Overloaded|00000000|System.Void|00000002|{Context}|00:System.Single"));
    }

    [TestMethod]
    public void EncodesConstructedGenericParameter()
    {
        Assert.AreEqual(
            $"{HostName}|ConstructedGeneric|00000000|System.Void|00000002|{Context}|00:ComputeSharp.ReadWriteBuffer`1[System.Int32]",
            Signature("ConstructedGeneric"));
    }

    [TestMethod]
    public void EncodesNestedConstructedGenericParameter()
    {
        Assert.AreEqual(
            $"{HostName}|NestedConstructedGeneric|00000000|System.Void|00000002|{Context}|00:Ukiyoe.Outer`1[System.Int32]+Inner`1[System.String]",
            Signature("NestedConstructedGeneric"));
    }

    [TestMethod]
    public void DistinguishesArrayRankInParameters()
    {
        Assert.AreEqual(
            $"{HostName}|SingleDimensionalArray|00000000|System.Void|00000002|{Context}|00:System.Int32[]",
            Signature("SingleDimensionalArray"));

        Assert.AreEqual(
            $"{HostName}|TwoDimensionalArray|00000000|System.Void|00000002|{Context}|00:System.Int32[,]",
            Signature("TwoDimensionalArray"));
    }

    [TestMethod]
    public void IgnoresNullableAnnotationInParameters()
    {
        Assert.AreEqual(
            $"{HostName}|NonNullableReference|00000000|System.Void|00000002|{Context}|00:System.String",
            Signature("NonNullableReference"));

        Assert.AreEqual(
            $"{HostName}|NullableReference|00000000|System.Void|00000002|{Context}|00:System.String",
            Signature("NullableReference"));
    }

    [TestMethod]
    public void IgnoresTupleElementNamesInParameters()
    {
        Assert.AreEqual(
            $"{HostName}|NamedTuple|00000000|System.Void|00000002|{Context}|00:System.ValueTuple`2[System.Int32,System.String]",
            Signature("NamedTuple"));

        Assert.AreEqual(
            $"{HostName}|RenamedTuple|00000000|System.Void|00000002|{Context}|00:System.ValueTuple`2[System.Int32,System.String]",
            Signature("RenamedTuple"));
    }

    [TestMethod]
    public void ProducesNormalizedSignature()
    {
        Assert.IsTrue(Signature("ConstructedGeneric").IsNormalized(NormalizationForm.FormC));
    }

    [TestMethod]
    public void NormalizesDecomposedIdentifiers()
    {
        const string DecomposedName = "Décomposed";

        string source = DecomposedSource.Replace("DecomposedTypeName", DecomposedName, StringComparison.Ordinal);

        Assert.IsFalse(DecomposedName.IsNormalized(NormalizationForm.FormC));

        ImmutableArray<IMethodSymbol> methodSymbols = SymbolHelper.GetMethods(
            source,
            $"Ukiyoe.{DecomposedName}",
            "Run",
            "CanonicalSignatureBuilderNormalizationTests");

        Assert.IsTrue(CanonicalSignatureBuilder.TryGetCanonicalSignature(methodSymbols[0], out string signature));
        Assert.IsTrue(signature.IsNormalized(NormalizationForm.FormC));
        Assert.AreEqual(
            $"Ukiyoe.{DecomposedName.Normalize(NormalizationForm.FormC)}|Run|00000000|System.Void|00000001|{Context}",
            signature);
    }

    [TestMethod]
    public void DetectsReservedCharactersInComponents()
    {
        Assert.IsFalse(CanonicalTypeNameBuilder.ContainsReservedCharacter("Ukiyoe.Host+Nested"));
        Assert.IsTrue(CanonicalTypeNameBuilder.ContainsReservedCharacter("Ukiyoe|Host"));
        Assert.IsTrue(CanonicalTypeNameBuilder.ContainsReservedCharacter("Ukiyoe:Host"));
    }

    [TestMethod]
    public void OrdersPipelinesByOrdinalSignatureComparison()
    {
        ImmutableArray<IMethodSymbol> methodSymbols = SymbolHelper.GetMethods(Source, HostName, "Overloaded", "CanonicalSignatureBuilderTests");
        List<string> forward = [];
        List<string> reversed = [];

        foreach (IMethodSymbol methodSymbol in methodSymbols)
        {
            Assert.IsTrue(CanonicalSignatureBuilder.TryGetCanonicalSignature(methodSymbol, out string signature));

            forward.Add(signature);
        }

        reversed.AddRange(forward);
        reversed.Reverse();

        forward.Sort(StringComparer.Ordinal);
        reversed.Sort(StringComparer.Ordinal);

        CollectionAssert.AreEqual(forward, reversed);
        Assert.IsTrue(string.CompareOrdinal(forward[0], forward[1]) < 0);
    }
}
