using System.Text;
using ComputeSharp.SourceGenerators.Helpers;
using ComputeSharp.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.SourceGenerators.Canonicalization;

[TestClass]
public class CanonicalTypeNameBuilderTests
{
    private const string Source = """
        using System.Collections.Generic;
        using ComputeSharp;

        namespace Ukiyoe;

        public class Outer<TOuter>
        {
            public class Inner<TInner>
            {
            }
        }

        public class Fields
        {
            public int Value;
            public int Alias;
            public dynamic Dynamic;
            public ReadWriteBuffer<int> IntBuffer;
            public ReadWriteBuffer<float> FloatBuffer;
            public Outer<int>.Inner<string> OuterIntInnerString;
            public Outer<string>.Inner<int> OuterStringInnerInt;
            public int[] SingleDimensional;
            public int[,] TwoDimensional;
            public int[,,] ThreeDimensional;
            public (int X, string Y) NamedTuple;
            public (int A, string B) RenamedTuple;
            public string NonNullableString;
            public string? NullableString;
            public Dictionary<string, ReadWriteBuffer<int>> Nested;
        }
        """;

    private static string CanonicalName(string fieldName)
    {
        ITypeSymbol typeSymbol = SymbolHelper.GetFieldType(Source, "Ukiyoe.Fields", fieldName, "CanonicalTypeNameBuilderTests");

        return CanonicalTypeNameBuilder.GetCanonicalTypeName(typeSymbol);
    }

    [TestMethod]
    public void ExpandsAliasToMetadataName()
    {
        Assert.AreEqual("System.Int32", CanonicalName("Value"));
        Assert.AreEqual("System.Int32", CanonicalName("Alias"));
    }

    [TestMethod]
    public void EncodesDynamicAsObject()
    {
        Assert.AreEqual("System.Object", CanonicalName("Dynamic"));
    }

    [TestMethod]
    public void DistinguishesConstructedGenericArguments()
    {
        Assert.AreEqual("ComputeSharp.ReadWriteBuffer`1[System.Int32]", CanonicalName("IntBuffer"));
        Assert.AreEqual("ComputeSharp.ReadWriteBuffer`1[System.Single]", CanonicalName("FloatBuffer"));
        Assert.AreNotEqual(CanonicalName("IntBuffer"), CanonicalName("FloatBuffer"));
    }

    [TestMethod]
    public void EncodesNestedConstructedGenericPerSegment()
    {
        Assert.AreEqual("Ukiyoe.Outer`1[System.Int32]+Inner`1[System.String]", CanonicalName("OuterIntInnerString"));
        Assert.AreEqual("Ukiyoe.Outer`1[System.String]+Inner`1[System.Int32]", CanonicalName("OuterStringInnerInt"));
        Assert.AreNotEqual(CanonicalName("OuterIntInnerString"), CanonicalName("OuterStringInnerInt"));
    }

    [TestMethod]
    public void DistinguishesArrayRanks()
    {
        Assert.AreEqual("System.Int32[]", CanonicalName("SingleDimensional"));
        Assert.AreEqual("System.Int32[,]", CanonicalName("TwoDimensional"));
        Assert.AreEqual("System.Int32[,,]", CanonicalName("ThreeDimensional"));
    }

    [TestMethod]
    public void IgnoresTupleElementNames()
    {
        Assert.AreEqual("System.ValueTuple`2[System.Int32,System.String]", CanonicalName("NamedTuple"));
        Assert.AreEqual(CanonicalName("NamedTuple"), CanonicalName("RenamedTuple"));
    }

    [TestMethod]
    public void IgnoresNullableAnnotations()
    {
        Assert.AreEqual("System.String", CanonicalName("NonNullableString"));
        Assert.AreEqual(CanonicalName("NonNullableString"), CanonicalName("NullableString"));
    }

    [TestMethod]
    public void EncodesRecursiveConstructedGeneric()
    {
        Assert.AreEqual(
            "System.Collections.Generic.Dictionary`2[System.String,ComputeSharp.ReadWriteBuffer`1[System.Int32]]",
            CanonicalName("Nested"));
    }

    [TestMethod]
    public void ProducesNormalizedNamesWithoutReservedCharacters()
    {
        string[] fieldNames =
        [
            "Value",
            "Dynamic",
            "IntBuffer",
            "OuterIntInnerString",
            "ThreeDimensional",
            "NamedTuple",
            "Nested"
        ];

        foreach (string fieldName in fieldNames)
        {
            string canonicalName = CanonicalName(fieldName);

            Assert.IsFalse(CanonicalTypeNameBuilder.ContainsReservedCharacter(canonicalName), canonicalName);
            Assert.IsTrue(canonicalName.IsNormalized(NormalizationForm.FormC), canonicalName);
        }
    }
}
