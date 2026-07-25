using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.SourceGenerators.Helpers;

internal static class SymbolHelper
{
    public static IMethodSymbol GetMethod(string source, string typeMetadataName, string methodName, string assemblyName)
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation(source, assemblyName);
        INamedTypeSymbol typeSymbol = GetType(compilation, typeMetadataName);

        foreach (ISymbol memberSymbol in typeSymbol.GetMembers(methodName))
        {
            if (memberSymbol is IMethodSymbol methodSymbol)
            {
                return methodSymbol;
            }
        }

        Assert.Fail($"The method '{methodName}' was not found on '{typeMetadataName}'.");

        return null!;
    }

    public static ImmutableArray<IMethodSymbol> GetMethods(string source, string typeMetadataName, string methodName, string assemblyName)
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation(source, assemblyName);
        INamedTypeSymbol typeSymbol = GetType(compilation, typeMetadataName);
        ImmutableArray<IMethodSymbol>.Builder builder = ImmutableArray.CreateBuilder<IMethodSymbol>();

        foreach (ISymbol memberSymbol in typeSymbol.GetMembers(methodName))
        {
            if (memberSymbol is IMethodSymbol methodSymbol)
            {
                builder.Add(methodSymbol);
            }
        }

        Assert.AreNotEqual(0, builder.Count, $"The method '{methodName}' was not found on '{typeMetadataName}'.");

        return builder.ToImmutable();
    }

    public static ITypeSymbol GetFieldType(string source, string typeMetadataName, string fieldName, string assemblyName)
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation(source, assemblyName);
        INamedTypeSymbol typeSymbol = GetType(compilation, typeMetadataName);

        foreach (ISymbol memberSymbol in typeSymbol.GetMembers(fieldName))
        {
            if (memberSymbol is IFieldSymbol fieldSymbol)
            {
                return fieldSymbol.Type;
            }
        }

        Assert.Fail($"The field '{fieldName}' was not found on '{typeMetadataName}'.");

        return null!;
    }

    public static ITypeSymbol GetParameterType(string source, string typeMetadataName, string methodName, int parameterIndex, string assemblyName)
    {
        return GetMethod(source, typeMetadataName, methodName, assemblyName).Parameters[parameterIndex].Type;
    }

    private static INamedTypeSymbol GetType(CSharpCompilation compilation, string typeMetadataName)
    {
        INamedTypeSymbol? typeSymbol = compilation.GetTypeByMetadataName(typeMetadataName);

        Assert.IsNotNull(typeSymbol, $"The type '{typeMetadataName}' was not found.");

        return typeSymbol;
    }
}
