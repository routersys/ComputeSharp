using System.Collections.Immutable;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.SourceGeneration.Helpers;
using ComputeSharp.SourceGenerators.Helpers;
using ComputeSharp.SourceGenerators.Models;
using ComputeSharp.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.SourceGenerators.Canonicalization;

[TestClass]
public class ResourcePlanGrammarTests
{
    private const string Source = """
        using ComputeSharp;

        namespace Ukiyoe;

        public sealed class Members
        {
            public ReadWriteBuffer<int> _index = null!;
            public ReadWriteTexture2D<Bgra32, Float4> Output = null!;
            public ReadOnlyBuffer<float> @weights = null!;
            public ReadWriteTexture3D<int> Volume = null!;
            public object NotAResource = null!;
        }
        """;

    private static ITypeSymbol FieldType(string fieldName)
    {
        return SymbolHelper.GetFieldType(Source, "Ukiyoe.Members", fieldName, "ResourcePlanGrammarTests");
    }

    private static ImmutableArray<ResourcePlanFieldContractInfo> PlanFields(string fieldName, uint slotResourceIndex = 0)
    {
        using ImmutableArrayBuilder<ResourcePlanFieldContractInfo> builder = new();

        Assert.IsTrue(ResourcePlanGrammar.TryAppendPlanFields(FieldType(fieldName), fieldName, slotResourceIndex, in builder));

        return builder.ToImmutable();
    }

    [TestMethod]
    public void DerivesBufferPlanKind()
    {
        Assert.IsTrue(ResourcePlanGrammar.TryGetPlanKind(FieldType("_index"), out ResourcePlanKind planKind));
        Assert.AreEqual(ResourcePlanKind.Buffer, planKind);
    }

    [TestMethod]
    public void DerivesTexture2DPlanKind()
    {
        Assert.IsTrue(ResourcePlanGrammar.TryGetPlanKind(FieldType("Output"), out ResourcePlanKind planKind));
        Assert.AreEqual(ResourcePlanKind.Texture2D, planKind);
    }

    [TestMethod]
    public void RejectsUnsupportedPlanKinds()
    {
        Assert.IsFalse(ResourcePlanGrammar.TryGetPlanKind(FieldType("Volume"), out _));
        Assert.IsFalse(ResourcePlanGrammar.TryGetPlanKind(FieldType("NotAResource"), out _));
    }

    [TestMethod]
    public void DerivesSingleLengthFieldForBuffer()
    {
        ImmutableArray<ResourcePlanFieldContractInfo> fields = PlanFields("_index");

        Assert.AreEqual(1, fields.Length);
        Assert.AreEqual(ResourcePlanDimensionKind.Length, fields[0].DimensionKind);
        Assert.AreEqual("_index", fields[0].MemberMetadataName);
        Assert.AreEqual("indexLength", fields[0].PlanParameterName);
        Assert.AreEqual("ComputeSharp.ReadWriteBuffer`1[System.Int32]", fields[0].ResourceTypeMetadataName);
        Assert.AreEqual(0u, fields[0].SlotResourceIndex);
    }

    [TestMethod]
    public void DerivesWidthAndHeightFieldsForTexture2D()
    {
        ImmutableArray<ResourcePlanFieldContractInfo> fields = PlanFields("Output", 2);

        Assert.AreEqual(2, fields.Length);
        Assert.AreEqual(ResourcePlanDimensionKind.Width, fields[0].DimensionKind);
        Assert.AreEqual("outputWidth", fields[0].PlanParameterName);
        Assert.AreEqual(ResourcePlanDimensionKind.Height, fields[1].DimensionKind);
        Assert.AreEqual("outputHeight", fields[1].PlanParameterName);
        Assert.AreEqual(2u, fields[0].SlotResourceIndex);
        Assert.AreEqual(2u, fields[1].SlotResourceIndex);
    }

    [TestMethod]
    public void StripsVerbatimPrefixFromPlanParameterName()
    {
        ImmutableArray<ResourcePlanFieldContractInfo> fields = PlanFields("weights");

        Assert.AreEqual("weightsLength", fields[0].PlanParameterName);
    }

    [TestMethod]
    public void CreatesCanonicalNamesFromSourceNames()
    {
        Assert.IsTrue(GeneratedIdentifier.TryCreateCanonicalName("_index", out string index));
        Assert.AreEqual("Index", index);

        Assert.IsTrue(GeneratedIdentifier.TryCreateCanonicalName("__grid", out string grid));
        Assert.AreEqual("Grid", grid);

        Assert.IsTrue(GeneratedIdentifier.TryCreateCanonicalName("@class", out string verbatim));
        Assert.AreEqual("Class", verbatim);

        Assert.IsTrue(GeneratedIdentifier.TryCreateCanonicalName("@_value", out string verbatimUnderscore));
        Assert.AreEqual("Value", verbatimUnderscore);

        Assert.IsTrue(GeneratedIdentifier.TryCreateCanonicalName("Output", out string output));
        Assert.AreEqual("Output", output);

        Assert.IsTrue(GeneratedIdentifier.TryCreateCanonicalName("colorIn", out string colorIn));
        Assert.AreEqual("ColorIn", colorIn);
    }

    [TestMethod]
    public void RejectsSourceNamesWithoutCanonicalName()
    {
        Assert.IsFalse(GeneratedIdentifier.TryCreateCanonicalName("_", out _));
        Assert.IsFalse(GeneratedIdentifier.TryCreateCanonicalName("___", out _));
        Assert.IsFalse(GeneratedIdentifier.TryCreateCanonicalName("@_", out _));
    }

    [TestMethod]
    public void CreatesLowerCamelPlanParameterNames()
    {
        Assert.AreEqual("colorInLength", GeneratedIdentifier.CreatePlanParameterName("ColorIn", ResourcePlanDimensionKind.Length));
        Assert.AreEqual("sourceWidth", GeneratedIdentifier.CreatePlanParameterName("Source", ResourcePlanDimensionKind.Width));
        Assert.AreEqual("sourceHeight", GeneratedIdentifier.CreatePlanParameterName("Source", ResourcePlanDimensionKind.Height));
    }
}
