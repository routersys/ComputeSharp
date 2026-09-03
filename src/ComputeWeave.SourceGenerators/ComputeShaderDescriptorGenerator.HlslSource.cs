using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using ComputeWeave.SourceGeneration.Extensions;
using ComputeWeave.SourceGeneration.Helpers;
using ComputeWeave.SourceGeneration.Mappings;
using ComputeWeave.SourceGeneration.Models;
using ComputeWeave.SourceGeneration.SyntaxProcessors;
using ComputeWeave.SourceGeneration.SyntaxRewriters;
using ComputeWeave.SourceGenerators.SyntaxRewriters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static ComputeWeave.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeWeave.SourceGenerators;

/// <inheritdoc/>
partial class ComputeShaderDescriptorGenerator
{
    /// <summary>
    /// A helper with all logic to generate the <c>HlslSource</c> property.
    /// </summary>
    internal static partial class HlslSource
    {
        /// <summary>
        /// Gathers all necessary information on a transpiled HLSL source for a given shader type.
        /// </summary>
        /// <param name="diagnostics">The collection of produced <see cref="DiagnosticInfo"/> instances.</param>
        /// <param name="semanticModelProvider">The <see cref="SemanticModelProvider"/> instance currently in use.</param>
        /// <param name="structDeclarationSymbol">The <see cref="INamedTypeSymbol"/> for the shader type.</param>
        /// <param name="shaderInterfaceType">The shader interface type implemented by the shader type.</param>
        /// <param name="isPixelShaderLike">Whether <paramref name="structDeclarationSymbol"/> is a "pixel shader like" type.</param>
        /// <param name="threadsX">The thread ids value for the X axis.</param>
        /// <param name="threadsY">The thread ids value for the Y axis.</param>
        /// <param name="threadsZ">The thread ids value for the Z axis.</param>
        /// <param name="token">The <see cref="CancellationToken"/> used to cancel the operation, if needed.</param>
        /// <param name="isImplicitTextureUsed">Indicates whether the current shader uses an implicit texture.</param>
        /// <param name="isSamplerUsed">Whether or not the static sampler is used.</param>
        /// <param name="synchronizesTheWholeThreadGroup">Whether the shader waits for every thread of its thread group.</param>
        /// <param name="hlslSource">The resulting HLSL source for the current shader.</param>
        public static void GetInfo(
            ImmutableArrayBuilder<DiagnosticInfo> diagnostics,
            SemanticModelProvider semanticModelProvider,
            INamedTypeSymbol structDeclarationSymbol,
            INamedTypeSymbol shaderInterfaceType,
            bool isPixelShaderLike,
            int threadsX,
            int threadsY,
            int threadsZ,
            CancellationToken token,
            out bool isImplicitTextureUsed,
            out bool isSamplerUsed,
            out bool synchronizesTheWholeThreadGroup,
            out string hlslSource)
        {
            // Detect any invalid properties
            HlslDefinitionsSyntaxProcessor.DetectAndReportInvalidPropertyDeclarations(diagnostics, structDeclarationSymbol);

            token.ThrowIfCancellationRequested();

            // We need to sets to track all discovered custom types and static methods
            HashSet<INamedTypeSymbol> discoveredTypes = new(SymbolEqualityComparer.Default);
            Dictionary<IMethodSymbol, MethodDeclarationSyntax> staticMethods = new(SymbolEqualityComparer.Default);
            Dictionary<IMethodSymbol, MethodDeclarationSyntax> instanceMethods = new(SymbolEqualityComparer.Default);
            Dictionary<IMethodSymbol, (MethodDeclarationSyntax, MethodDeclarationSyntax)> constructors = new(SymbolEqualityComparer.Default);
            Dictionary<IFieldSymbol, string> constantDefinitions = new(SymbolEqualityComparer.Default);
            Dictionary<IFieldSymbol, HlslStaticField> staticFieldDefinitions = new(SymbolEqualityComparer.Default);

            // Setup the semantic model and basic properties
            bool isComputeShader = !isPixelShaderLike;
            string? implicitTextureType = HlslKnownTypes.GetMappedNameForPixelShaderType(shaderInterfaceType);

            token.ThrowIfCancellationRequested();

            GetInstanceFields(
                diagnostics,
                structDeclarationSymbol,
                discoveredTypes,
                isComputeShader,
                out ImmutableArray<HlslResourceField> resourceFields,
                out ImmutableArray<HlslValueField> valueFields);

            token.ThrowIfCancellationRequested();

            ImmutableArray<HlslSharedBuffer> sharedBuffers = GetSharedBuffers(
                structDeclarationSymbol,
                discoveredTypes);

            token.ThrowIfCancellationRequested();

            // Every rewriter for this shader raises what it needs into this one instance, so a requirement
            // is read here whichever declaration it was found in and however that declaration was reached
            HlslShaderRequirements requirements = new();

            (string entryPoint, ImmutableArray<HlslMethod> processedMethods) = GetProcessedMethods(
                diagnostics,
                structDeclarationSymbol,
                shaderInterfaceType,
                semanticModelProvider,
                discoveredTypes,
                staticMethods,
                instanceMethods,
                constructors,
                constantDefinitions,
                staticFieldDefinitions,
                requirements,
                isComputeShader,
                token);

            token.ThrowIfCancellationRequested();

            Dictionary<IMethodSymbol, LocalFunctionStatementSyntax> initializerLocalFunctions = new(SymbolEqualityComparer.Default);

            ImmutableArray<HlslStaticField> staticFields = GetStaticFields(
                diagnostics,
                semanticModelProvider,
                structDeclarationSymbol,
                discoveredTypes,
                staticMethods,
                instanceMethods,
                constructors,
                constantDefinitions,
                staticFieldDefinitions,
                requirements,
                initializerLocalFunctions,
                token);

            token.ThrowIfCancellationRequested();

            // The static methods are gathered after the static fields, because an initializer can import one
            // of its own. Where they land among the methods does not matter: every forward declaration is
            // written before any of the definitions, and before the static fields that call them.
            processedMethods = AppendImportedMethods(processedMethods, staticMethods, initializerLocalFunctions);

            token.ThrowIfCancellationRequested();

            // Process the discovered types and constants
            HlslDefinitionsSyntaxProcessor.GetDeclaredTypes(
                diagnostics,
                structDeclarationSymbol,
                discoveredTypes,
                instanceMethods,
                constructors,
                out ImmutableArray<HlslUserType> typeDeclarations,
                out ImmutableArray<string> typeMethodDeclarations);

            token.ThrowIfCancellationRequested();

            ImmutableArray<HlslConstant> definedConstants = HlslDefinitionsSyntaxProcessor.GetDefinedConstants(constantDefinitions);

            token.ThrowIfCancellationRequested();

            // Check whether an implicit texture is used in the shader
            isImplicitTextureUsed = implicitTextureType is not null;

            // Read the requirements once every declaration has been rewritten, a static field initializer
            // being able to raise one just as the shader body and the declarations it reaches can
            isSamplerUsed = requirements.IsSamplerUsed;
            synchronizesTheWholeThreadGroup = requirements.SynchronizesTheWholeThreadGroup;

            // Get the HLSL source data with the intermediate info
            hlslSource = GetHlslSourceInfo(
                threadsX,
                threadsY,
                threadsZ,
                definedConstants,
                resourceFields,
                valueFields,
                staticFields,
                sharedBuffers,
                processedMethods,
                typeDeclarations,
                typeMethodDeclarations,
                isComputeShader,
                implicitTextureType,
                requirements.IsSamplerUsed,
                entryPoint);
        }

        /// <summary>
        /// Gets a sequence of captured fields and their mapped names.
        /// </summary>
        /// <param name="diagnostics">The collection of produced <see cref="DiagnosticInfo"/> instances.</param>
        /// <param name="structDeclarationSymbol">The input <see cref="INamedTypeSymbol"/> instance to process.</param>
        /// <param name="types">The collection of currently discovered types.</param>
        /// <param name="isComputeShader">Indicates whether or not <paramref name="structDeclarationSymbol"/> represents a compute shader.</param>
        /// <param name="resourceFields">The resulting sequence of resource instance fields for the current shader.</param>
        /// <param name="valueFields">The resulting sequence of value instance fields for the current shader.</param>
        private static void GetInstanceFields(
            ImmutableArrayBuilder<DiagnosticInfo> diagnostics,
            INamedTypeSymbol structDeclarationSymbol,
            ICollection<INamedTypeSymbol> types,
            bool isComputeShader,
            out ImmutableArray<HlslResourceField> resourceFields,
            out ImmutableArray<HlslValueField> valueFields)
        {
            using ImmutableArrayBuilder<HlslResourceField> resources = new();
            using ImmutableArrayBuilder<HlslValueField> values = new();

            bool hlslResourceFound = false;

            foreach (ISymbol memberSymbol in structDeclarationSymbol.GetMembers())
            {
                // Skip constants and static fields, we only care about instance ones here
                if (memberSymbol is not IFieldSymbol { IsConst: false, IsStatic: false } fieldSymbol)
                {
                    continue;
                }

                // Try to get the actual field name
                if (!ConstantBufferSyntaxProcessor.TryGetFieldAccessorName(fieldSymbol, out string? fieldName, out _))
                {
                    continue;
                }

                // Captured fields must be named type symbols
                if (fieldSymbol.Type is not INamedTypeSymbol typeSymbol)
                {
                    diagnostics.Add(InvalidShaderField, fieldSymbol, structDeclarationSymbol, fieldName, fieldSymbol.Type);

                    continue;
                }

                string metadataName = typeSymbol.GetFullyQualifiedMetadataName();
                string typeName = HlslKnownTypes.GetMappedName(typeSymbol);

                _ = HlslKnownKeywords.TryGetMappedName(fieldName, out string? mapping);

                // Allowed fields must be either resources, unmanaged values or delegates
                if (HlslKnownTypes.IsTypedResourceType(metadataName))
                {
                    hlslResourceFound = true;

                    // Track the type of items in the current buffer
                    if (HlslKnownTypes.IsStructuredBufferType(metadataName))
                    {
                        types.Add((INamedTypeSymbol)typeSymbol.TypeArguments[0]);
                    }

                    // Check whether the resource is a globallycoherent writeable buffer
                    if (HlslKnownTypes.IsReadWriteBufferType(metadataName) &&
                        fieldSymbol.TryGetAttributeWithFullyQualifiedMetadataName("ComputeWeave.GloballyCoherentAttribute", out _))
                    {
                        typeName = "globallycoherent " + typeName;
                    }

                    // Add the current mapping for the name (if the name used a reserved keyword)
                    resources.Add((metadataName, mapping ?? fieldName, typeName));
                }
                else if (!typeSymbol.IsUnmanagedType &&
                         typeSymbol.TypeKind != TypeKind.Delegate)
                {
                    // Shaders can only capture valid HLSL resource types or unmanaged types
                    diagnostics.Add(InvalidShaderField, fieldSymbol, structDeclarationSymbol, fieldName, typeSymbol);

                    continue;
                }
                else if (typeSymbol.IsUnmanagedType)
                {
                    // Track the type if it's a custom struct
                    if (!HlslKnownTypes.IsKnownHlslType(metadataName))
                    {
                        types.Add(typeSymbol);
                    }

                    values.Add((mapping ?? fieldName, typeName));
                }
            }

            // If the shader is a compute one (so no implicit output texture), it has to contain at least one resource
            if (!hlslResourceFound && isComputeShader)
            {
                diagnostics.Add(MissingShaderResources, structDeclarationSymbol, structDeclarationSymbol);
            }

            resourceFields = resources.ToImmutable();
            valueFields = values.ToImmutable();
        }

        /// <summary>
        /// Appends the imported static methods, and the local functions lifted out of them, to the processed ones.
        /// </summary>
        /// <param name="processedMethods">The methods gathered while rewriting the shader.</param>
        /// <param name="staticMethods">The collection of discovered static methods.</param>
        /// <param name="localFunctions">The local functions lifted out of methods imported into an initializer.</param>
        /// <returns>The full sequence of methods to write.</returns>
        private static ImmutableArray<HlslMethod> AppendImportedMethods(
            ImmutableArray<HlslMethod> processedMethods,
            IReadOnlyDictionary<IMethodSymbol, MethodDeclarationSyntax> staticMethods,
            IReadOnlyDictionary<IMethodSymbol, LocalFunctionStatementSyntax> localFunctions)
        {
            using ImmutableArrayBuilder<HlslMethod> builder = new();

            builder.AddRange(processedMethods.AsSpan());

            foreach (LocalFunctionStatementSyntax localFunction in localFunctions.Values)
            {
                // Parameter default values stay on the forward declaration only, as HLSL rejects repeating them
                builder.Add((
                    localFunction.AsDefinition().NormalizeWhitespace(eol: "\n").ToFullString(),
                    localFunction.WithoutParameterDefaults().NormalizeWhitespace(eol: "\n").ToFullString()));
            }

            foreach (MethodDeclarationSyntax staticMethod in staticMethods.Values)
            {
                builder.Add((
                    staticMethod.AsDefinition().NormalizeWhitespace(eol: "\n").ToFullString(),
                    staticMethod.WithoutParameterDefaults().NormalizeWhitespace(eol: "\n").ToFullString()));
            }

            return builder.ToImmutable();
        }

        /// <summary>
        /// Gets a sequence of shader static fields and their mapped names.
        /// </summary>
        /// <param name="diagnostics">The collection of produced <see cref="DiagnosticInfo"/> instances.</param>
        /// <param name="semanticModel">The <see cref="SemanticModelProvider"/> instance for the type to process.</param>
        /// <param name="structDeclarationSymbol">The type symbol for the shader type.</param>
        /// <param name="discoveredTypes">The collection of currently discovered types.</param>
        /// <param name="staticMethods">The collection of discovered static methods.</param>
        /// <param name="instanceMethods">The collection of discovered instance methods for custom struct types.</param>
        /// <param name="constructors">The collection of discovered constructors for custom struct types.</param>
        /// <param name="constantDefinitions">The collection of discovered constant definitions.</param>
        /// <param name="staticFieldDefinitions">The collection of discovered static field definitions.</param>
        /// <param name="requirements">The requirements gathered for the shader being rewritten.</param>
        /// <param name="localFunctions">The collection to receive the local functions lifted out of imported methods.</param>
        /// <param name="token">The <see cref="CancellationToken"/> used to cancel the operation, if needed.</param>
        /// <returns>A sequence of static constant fields in <paramref name="structDeclarationSymbol"/>.</returns>
        private static ImmutableArray<HlslStaticField> GetStaticFields(
            ImmutableArrayBuilder<DiagnosticInfo> diagnostics,
            SemanticModelProvider semanticModel,
            INamedTypeSymbol structDeclarationSymbol,
            ICollection<INamedTypeSymbol> discoveredTypes,
            IDictionary<IMethodSymbol, MethodDeclarationSyntax> staticMethods,
            IDictionary<IMethodSymbol, MethodDeclarationSyntax> instanceMethods,
            IDictionary<IMethodSymbol, (MethodDeclarationSyntax, MethodDeclarationSyntax)> constructors,
            IDictionary<IFieldSymbol, string> constantDefinitions,
            IDictionary<IFieldSymbol, HlslStaticField> staticFieldDefinitions,
            HlslShaderRequirements requirements,
            IDictionary<IMethodSymbol, LocalFunctionStatementSyntax> localFunctions,
            CancellationToken token)
        {
            using ImmutableArrayBuilder<HlslStaticField> builder = new();

            foreach (ISymbol memberSymbol in structDeclarationSymbol.GetMembers())
            {
                // Pre-filter fields that cannot be valid, to reduce the calls to check the [GroupShared] attribute
                if (memberSymbol is not IFieldSymbol { IsImplicitlyDeclared: false, IsStatic: true, IsConst: false, } fieldSymbol)
                {
                    continue;
                }

                // Ignore [GroupShared] fields, they'll be gathered in a separate rewriting step
                if (fieldSymbol.TryGetAttributeWithFullyQualifiedMetadataName("ComputeWeave.GroupSharedAttribute", out _))
                {
                    continue;
                }

                if (HlslDefinitionsSyntaxProcessor.TryGetStaticField(
                    structDeclarationSymbol,
                    fieldSymbol,
                    semanticModel,
                    discoveredTypes,
                    staticMethods,
                    instanceMethods,
                    constructors,
                    constantDefinitions,
                    staticFieldDefinitions,
                    requirements,
                    diagnostics,
                    token,
                    out string? name,
                    out string? typeDeclaration,
                    out string? assignmentExpression,
                    out StaticFieldRewriter? staticFieldRewriter))
                {
                    builder.Add((name, typeDeclaration, assignmentExpression));

                    // An initializer may import a method that declares a local function, which HLSL cannot
                    // nest, so the ones lifted out of it are carried up to be written like any other
                    foreach (KeyValuePair<IMethodSymbol, LocalFunctionStatementSyntax> localFunction in staticFieldRewriter.LocalFunctions)
                    {
                        localFunctions[localFunction.Key] = localFunction.Value;
                    }
                }
            }

            // Also gather the external static fields. The collection holds the fields of the shader
            // as well, the processor claiming an entry for each one so that an initializer reading the
            // field it writes can be seen, and those are already in the builder from the loop above
            foreach (KeyValuePair<IFieldSymbol, HlslStaticField> externalField in staticFieldDefinitions)
            {
                if (!SymbolEqualityComparer.Default.Equals(externalField.Key.ContainingType, structDeclarationSymbol))
                {
                    builder.Add(externalField.Value);
                }
            }

            return builder.ToImmutable();
        }

        /// <summary>
        /// Gets a sequence of captured members and their mapped names.
        /// </summary>
        /// <param name="structDeclarationSymbol">The input <see cref="INamedTypeSymbol"/> instance to process.</param>
        /// <param name="types">The collection of currently discovered types.</param>
        /// <returns>A sequence of captured members in <paramref name="structDeclarationSymbol"/>.</returns>
        private static ImmutableArray<HlslSharedBuffer> GetSharedBuffers(
            INamedTypeSymbol structDeclarationSymbol,
            ICollection<INamedTypeSymbol> types)
        {
            using ImmutableArrayBuilder<HlslSharedBuffer> builder = new();

            foreach (ISymbol memberSymbol in structDeclarationSymbol.GetMembers())
            {
                // We're only looking for static fields of a valid type for group shared buffers
                if (memberSymbol is not IFieldSymbol { IsStatic: true, Type: IArrayTypeSymbol { ElementType: INamedTypeSymbol { IsUnmanagedType: true } } typeSymbol } fieldSymbol)
                {
                    continue;
                }

                if (!fieldSymbol.TryGetAttributeWithFullyQualifiedMetadataName("ComputeWeave.GroupSharedAttribute", out AttributeData? attribute))
                {
                    continue;
                }

                int? bufferSize = (int?)attribute.ConstructorArguments.FirstOrDefault().Value;

                string typeName = HlslKnownTypes.GetMappedElementName(typeSymbol);

                _ = HlslKnownKeywords.TryGetMappedName(fieldSymbol.Name, out string? mapping);

                builder.Add((mapping ?? fieldSymbol.Name, typeName, bufferSize));

                types.Add((INamedTypeSymbol)typeSymbol.ElementType);
            }

            return builder.ToImmutable();
        }

        /// <summary>
        /// Gets a sequence of processed methods declared within a given type.
        /// </summary>
        /// <param name="diagnostics">The collection of produced <see cref="DiagnosticInfo"/> instances.</param>
        /// <param name="structDeclarationSymbol">The type symbol for the shader type.</param>
        /// <param name="shaderInterfaceType">The shader interface type implemented by the shader type.</param>
        /// <param name="semanticModel">The <see cref="SemanticModelProvider"/> instance for the type to process.</param>
        /// <param name="discoveredTypes">The collection of currently discovered types.</param>
        /// <param name="staticMethods">The set of discovered and processed static methods.</param>
        /// <param name="instanceMethods">The collection of discovered instance methods for custom struct types.</param>
        /// <param name="constructors">The collection of discovered constructors for custom struct types.</param>
        /// <param name="constantDefinitions">The collection of discovered constant definitions.</param>
        /// <param name="staticFieldDefinitions">The collection of discovered static field definitions.</param>
        /// <param name="requirements">The requirements gathered for the shader being rewritten.</param>
        /// <param name="isComputeShader">Indicates whether or not <paramref name="structDeclarationSymbol"/> represents a compute shader.</param>
        /// <param name="token">The <see cref="CancellationToken"/> used to cancel the operation, if needed.</param>
        /// <returns>A sequence of processed methods in <paramref name="structDeclarationSymbol"/>, and the entry point.</returns>
        private static (string EntryPoint, ImmutableArray<HlslMethod> Methods) GetProcessedMethods(
            ImmutableArrayBuilder<DiagnosticInfo> diagnostics,
            INamedTypeSymbol structDeclarationSymbol,
            INamedTypeSymbol shaderInterfaceType,
            SemanticModelProvider semanticModel,
            ICollection<INamedTypeSymbol> discoveredTypes,
            IDictionary<IMethodSymbol, MethodDeclarationSyntax> staticMethods,
            IDictionary<IMethodSymbol, MethodDeclarationSyntax> instanceMethods,
            IDictionary<IMethodSymbol, (MethodDeclarationSyntax, MethodDeclarationSyntax)> constructors,
            IDictionary<IFieldSymbol, string> constantDefinitions,
            IDictionary<IFieldSymbol, HlslStaticField> staticFieldDefinitions,
            HlslShaderRequirements requirements,
            bool isComputeShader,
            CancellationToken token)
        {
            using ImmutableArrayBuilder<HlslMethod> methods = new();

            IMethodSymbol entryPointInterfaceMethod = shaderInterfaceType.GetMethod("Execute")!;
            string? entryPoint = null;

            foreach (ISymbol memberSymbol in structDeclarationSymbol.GetMembers())
            {
                // Find all declared methods in the type
                if (memberSymbol is not IMethodSymbol { IsImplicitlyDeclared: false, } methodSymbol)
                {
                    continue;
                }

                // Ensure that we have accessible source information
                if (!methodSymbol.TryGetSyntaxNode(token, out MethodDeclarationSyntax? methodDeclaration))
                {
                    continue;
                }

                // Check whether the current method is the entry point (ie. it's implementing 'Execute'). We use
                // 'FindImplementationForInterfaceMember' to handle explicit interface implementations as well.
                bool isShaderEntryPoint = SymbolEqualityComparer.Default.Equals(
                    structDeclarationSymbol.FindImplementationForInterfaceMember(entryPointInterfaceMethod),
                    methodSymbol);

                // Except for the entry point, ignore explicit interface implementations
                if (!isShaderEntryPoint && !methodSymbol.ExplicitInterfaceImplementations.IsDefaultOrEmpty)
                {
                    continue;
                }

                token.ThrowIfCancellationRequested();

                // Create the source rewriter for the current method
                ShaderSourceRewriter shaderSourceRewriter = new(
                    structDeclarationSymbol,
                    semanticModel,
                    discoveredTypes,
                    staticMethods,
                    instanceMethods,
                    constructors,
                    constantDefinitions,
                    staticFieldDefinitions,
                    requirements,
                    diagnostics,
                    token,
                    isShaderEntryPoint);

                // Rewrite the method syntax tree
                MethodDeclarationSyntax? processedMethod = shaderSourceRewriter.Visit(methodDeclaration)!.WithoutTrivia();

                token.ThrowIfCancellationRequested();

                // Emit the extracted local functions first
                foreach (KeyValuePair<IMethodSymbol, LocalFunctionStatementSyntax> localFunction in shaderSourceRewriter.LocalFunctions)
                {
                    // Parameter default values stay on the forward declaration only, as HLSL rejects repeating them
                    methods.Add((
                        localFunction.Value.AsDefinition().NormalizeWhitespace(eol: "\n").ToFullString(),
                        localFunction.Value.WithoutParameterDefaults().NormalizeWhitespace(eol: "\n").ToFullString()));
                }

                // If the method is the shader entry point, do additional processing
                if (isShaderEntryPoint)
                {
                    processedMethod = isComputeShader switch
                    {
                        true => new ExecuteMethodRewriter.Compute(shaderSourceRewriter).Visit(processedMethod)!,
                        false => new ExecuteMethodRewriter.Pixel(shaderSourceRewriter).Visit(processedMethod)!
                    };

                    entryPoint = processedMethod.NormalizeWhitespace(eol: "\n").ToFullString();
                }
                else
                {
                    methods.Add((
                        processedMethod.AsDefinition().NormalizeWhitespace(eol: "\n").ToFullString(),
                        processedMethod.WithoutParameterDefaults().NormalizeWhitespace(eol: "\n").ToFullString()));
                }
            }

            token.ThrowIfCancellationRequested();

            return (entryPoint!, methods.ToImmutable());
        }

        /// <summary>
        /// Produces the series of statements to build the current HLSL source.
        /// </summary>
        /// <param name="threadsX">The thread ids value for the X axis.</param>
        /// <param name="threadsY">The thread ids value for the Y axis.</param>
        /// <param name="threadsZ">The thread ids value for the Z axis.</param>
        /// <param name="definedConstants"><inheritdoc cref="HlslSourceSyntaxProcessor.WriteTopDeclarations" path="/param[@name='definedConstants']/node()"/></param>
        /// <param name="resourceFields">The sequence of resource instance fields for the current shader.</param>
        /// <param name="valueFields"><inheritdoc cref="HlslSourceSyntaxProcessor.WriteCapturedFields" path="/param[@name='valueFields']/node()"/></param>
        /// <param name="staticFields"><inheritdoc cref="HlslSourceSyntaxProcessor.WriteTopDeclarations" path="/param[@name='staticFields']/node()"/></param>
        /// <param name="sharedBuffers">The sequence of shared buffers declared by the shader.</param>
        /// <param name="processedMethods"><inheritdoc cref="HlslSourceSyntaxProcessor.WriteTopDeclarations" path="/param[@name='processedMethods']/node()"/></param>
        /// <param name="typeDeclarations"><inheritdoc cref="HlslSourceSyntaxProcessor.WriteTopDeclarations" path="/param[@name='typeDeclarations']/node()"/></param>
        /// <param name="typeMethodDeclarations"><inheritdoc cref="HlslSourceSyntaxProcessor.WriteMethodDeclarations" path="/param[@name='typeMethodDeclarations']/node()"/></param>
        /// <param name="isComputeShader">Whether or not the current shader type is a compute shader.</param>
        /// <param name="implicitTextureType">The type of the implicit target texture, if present.</param>
        /// <param name="isSamplerUsed">Whether the static sampler is used by the shader.</param>
        /// <param name="executeMethod">The body of the entry point of the shader.</param>
        /// <returns>The HLSL source for the current shader.</returns>
        private static string GetHlslSourceInfo(
            int threadsX,
            int threadsY,
            int threadsZ,
            ImmutableArray<HlslConstant> definedConstants,
            ImmutableArray<HlslResourceField> resourceFields,
            ImmutableArray<HlslValueField> valueFields,
            ImmutableArray<HlslStaticField> staticFields,
            ImmutableArray<HlslSharedBuffer> sharedBuffers,
            ImmutableArray<HlslMethod> processedMethods,
            ImmutableArray<HlslUserType> typeDeclarations,
            ImmutableArray<string> typeMethodDeclarations,
            bool isComputeShader,
            string? implicitTextureType,
            bool isSamplerUsed,
            string executeMethod)
        {
            using IndentedTextWriter writer = new();

            // Group size constants
            writer.WriteLine($"#define __GroupSize__get_X {threadsX}");
            writer.WriteLine($"#define __GroupSize__get_Y {threadsY}");
            writer.WriteLine($"#define __GroupSize__get_Z {threadsZ}");

            HlslSourceSyntaxProcessor.WriteTopDeclarations(
                writer,
                definedConstants,
                staticFields,
                processedMethods,
                typeDeclarations,
                includeTypeForwardDeclarations: true);

            writer.WriteLine("cbuffer _ : register(b0)");

            // Captured variables
            using (writer.WriteBlock())
            {
                writer.WriteLine("int __x;");
                writer.WriteLine("int __y;");
                writer.WriteLineIf(isComputeShader, "int __z;");

                HlslSourceSyntaxProcessor.WriteCapturedFields(writer, valueFields);
            }

            int constantBuffersCount = 1;
            int readOnlyBuffersCount = 0;
            int readWriteBuffersCount = 0;

            // Optional implicit texture field
            writer.WriteLine(skipIfPresent: true);
            writer.WriteLineIf(!isComputeShader, $"{implicitTextureType} __outputTexture : register(u{readWriteBuffersCount++});");

            // Optional sampler field
            writer.WriteLine(skipIfPresent: true);
            writer.WriteLineIf(isSamplerUsed, "SamplerState __sampler : register(s);");

            // Resources
            foreach (HlslResourceField resourceField in resourceFields)
            {
                writer.WriteLine(skipIfPresent: true);

                if (HlslKnownTypes.IsConstantBufferType(resourceField.MetadataName))
                {
                    writer.WriteLine($"cbuffer _{resourceField.Name} : register(b{constantBuffersCount++})");

                    using (writer.WriteBlock())
                    {
                        writer.WriteLine($"{resourceField.Type} {resourceField.Name}[2];");
                    }
                }
                else if (HlslKnownTypes.IsReadOnlyTypedResourceType(resourceField.MetadataName))
                {
                    writer.WriteLine($"{resourceField.Type} {resourceField.Name} : register(t{readOnlyBuffersCount++});");
                }
                else if (HlslKnownTypes.IsReadWriteTypedResourceType(resourceField.MetadataName))
                {
                    writer.WriteLine($"{resourceField.Type} {resourceField.Name} : register(u{readWriteBuffersCount++});");
                }
            }

            // Shared buffers
            foreach (HlslSharedBuffer buffer in sharedBuffers)
            {
                object count = (object?)buffer.Count ?? "__GroupSize__get_X * __GroupSize__get_Y * __GroupSize__get_Z";

                writer.WriteLine(skipIfPresent: true);
                writer.WriteLine($"groupshared {buffer.Type} {buffer.Name} [{count}];");
            }

            HlslSourceSyntaxProcessor.WriteMethodDeclarations(writer, processedMethods, typeMethodDeclarations);

            // Entry point
            writer.WriteLine("[NumThreads(__GroupSize__get_X, __GroupSize__get_Y, __GroupSize__get_Z)]");
            writer.WriteLine(executeMethod);

            return writer.ToString();
        }
    }
}