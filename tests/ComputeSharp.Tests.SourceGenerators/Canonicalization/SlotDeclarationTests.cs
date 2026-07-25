using ComputeSharp.SourceGenerators.Helpers;
using ComputeSharp.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.SourceGenerators.Canonicalization;

[TestClass]
public class SlotDeclarationTests
{
    private const string Source = """
        using System;
        using ComputeSharp;

        namespace Ukiyoe;

        public sealed class Grid
        {
            public ReadWriteBuffer<int> Index { get; }
        }

        public sealed class ExternalView : IDisposable
        {
            public void Dispose()
            {
            }
        }

        public sealed class Host
        {
            private readonly ComputeResourceSlot<ReadWriteBuffer<int>> index = new();
            private readonly ComputeResourceGroupSlot<Grid> grid = new();
            private readonly ReadWriteBuffer<float> borrowed = null!;
        }

        public sealed class ResourceSet
        {
            private readonly SharedTextureSlot<Bgra32, Float4, ExternalView> source = new();
        }
        """;

    private static string FieldTypeName(string typeMetadataName, string fieldName)
    {
        ITypeSymbol typeSymbol = SymbolHelper.GetFieldType(Source, typeMetadataName, fieldName, "SlotDeclarationTests");

        return CanonicalTypeNameBuilder.GetCanonicalTypeName(typeSymbol);
    }

    [TestMethod]
    public void DeclaresOwnedResourceSlot()
    {
        Assert.AreEqual(
            "ComputeSharp.ComputeResourceSlot`1[ComputeSharp.ReadWriteBuffer`1[System.Int32]]",
            FieldTypeName("Ukiyoe.Host", "index"));
    }

    [TestMethod]
    public void DeclaresOwnedResourceGroupSlot()
    {
        Assert.AreEqual(
            "ComputeSharp.ComputeResourceGroupSlot`1[Ukiyoe.Grid]",
            FieldTypeName("Ukiyoe.Host", "grid"));
    }

    [TestMethod]
    public void DeclaresBorrowedResourceField()
    {
        Assert.AreEqual(
            "ComputeSharp.ReadWriteBuffer`1[System.Single]",
            FieldTypeName("Ukiyoe.Host", "borrowed"));
    }

    [TestMethod]
    public void DeclaresSharedTextureSlot()
    {
        Assert.AreEqual(
            "ComputeSharp.SharedTextureSlot`3[ComputeSharp.Bgra32,ComputeSharp.Float4,Ukiyoe.ExternalView]",
            FieldTypeName("Ukiyoe.ResourceSet", "source"));
    }
}
