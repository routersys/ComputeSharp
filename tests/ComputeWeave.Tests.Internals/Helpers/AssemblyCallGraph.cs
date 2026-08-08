using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using ComputeWeave.Resources.Lifetime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

/// <summary>
/// The call graph of <c>ComputeWeave.dll</c>, keyed by the declaring type and name of each method.
/// </summary>
/// <remarks>
/// <para>
/// Nodes carry no signature, so overloads and generic instantiations of one name collapse into a single node.
/// That widens every edge set rather than narrowing it, so a target this graph reports as unreachable is
/// unreachable in the assembly as well.
/// </para>
/// <para>
/// An interface call resolves to the interface member rather than to the implementation behind it, so a path
/// that runs through an interface ends at that member. A caller reached only through an interface is therefore
/// invisible here, which is what makes the explicit implementation of a member a boundary the graph respects.
/// </para>
/// </remarks>
internal sealed class AssemblyCallGraph
{
    private readonly Dictionary<string, SortedSet<string>> callees = [];

    private readonly Dictionary<string, SortedSet<string>> callers = [];

    private AssemblyCallGraph()
    {
    }

    /// <summary>
    /// Reads the call graph out of the compiled assembly.
    /// </summary>
    /// <returns>The call graph.</returns>
    public static AssemblyCallGraph Read()
    {
        AssemblyCallGraph graph = new();
        Dictionary<short, OpCode> table = BuildOpCodeTable();

        using FileStream stream = File.OpenRead(typeof(ResourceGenerationRecord).Assembly.Location);
        using PEReader peReader = new(stream);

        MetadataReader reader = peReader.GetMetadataReader();

        foreach (MethodDefinitionHandle handle in reader.MethodDefinitions)
        {
            MethodDefinition method = reader.GetMethodDefinition(handle);

            if (method.RelativeVirtualAddress == 0)
            {
                continue;
            }

            TypeDefinition owner = reader.GetTypeDefinition(method.GetDeclaringType());
            string caller = $"{reader.GetString(owner.Name)}.{reader.GetString(method.Name)}";

            byte[]? il = peReader.GetMethodBody(method.RelativeVirtualAddress).GetILBytes();

            if (il is null)
            {
                continue;
            }

            graph.ReadBody(reader, table, caller, il);
        }

        return graph;
    }

    /// <summary>
    /// Gets the methods a method calls.
    /// </summary>
    /// <param name="method">The qualified name of the method.</param>
    /// <returns>The methods it calls.</returns>
    public IReadOnlyCollection<string> GetCallees(string method)
    {
        return this.callees.TryGetValue(method, out SortedSet<string>? targets) ? targets : [];
    }

    /// <summary>
    /// Gets the methods that call a method.
    /// </summary>
    /// <param name="method">The qualified name of the method.</param>
    /// <returns>The methods that call it.</returns>
    public IReadOnlyCollection<string> GetCallers(string method)
    {
        return this.callers.TryGetValue(method, out SortedSet<string>? sources) ? sources : [];
    }

    /// <summary>
    /// Gets whether a target is reachable from a root, and the path that reaches it.
    /// </summary>
    /// <param name="root">The qualified name of the method to start from.</param>
    /// <param name="target">The qualified name of the method to look for.</param>
    /// <param name="path">The path from the root to the target, if there is one.</param>
    /// <returns>Whether the target is reachable.</returns>
    public bool TryGetPath(string root, string target, out string path)
    {
        Dictionary<string, string?> visitedFrom = new() { [root] = null };
        Queue<string> pending = new();

        pending.Enqueue(root);

        while (pending.Count != 0)
        {
            string current = pending.Dequeue();

            foreach (string callee in GetCallees(current))
            {
                if (!visitedFrom.TryAdd(callee, current))
                {
                    continue;
                }

                if (callee == target)
                {
                    path = BuildPath(visitedFrom, callee);

                    return true;
                }

                pending.Enqueue(callee);
            }
        }

        path = string.Empty;

        return false;
    }

    private static string BuildPath(Dictionary<string, string?> visitedFrom, string target)
    {
        List<string> steps = [];

        for (string? step = target; step is not null; step = visitedFrom[step])
        {
            steps.Add(step);
        }

        steps.Reverse();

        return string.Join(" -> ", steps);
    }

    private void ReadBody(MetadataReader reader, Dictionary<short, OpCode> table, string caller, byte[] il)
    {
        int offset = 0;

        while (offset < il.Length)
        {
            short value = il[offset];

            if (value == 0xFE)
            {
                value = (short)(0xFE00 | il[offset + 1]);
                offset += 2;
            }
            else
            {
                offset++;
            }

            if (!table.TryGetValue(value, out OpCode opCode))
            {
                Assert.Fail($"The instruction stream of {caller} carries an unknown opcode.");
            }

            if (opCode.OperandType is OperandType.InlineSwitch)
            {
                offset += 4 + (4 * BitConverter.ToInt32(il, offset));

                continue;
            }

            if (opCode.OperandType is OperandType.InlineMethod &&
                TryGetQualifiedName(reader, BitConverter.ToInt32(il, offset)) is string target)
            {
                Add(this.callees, caller, target);
                Add(this.callers, target, caller);
            }

            offset += GetOperandByteLength(opCode.OperandType);
        }
    }

    private static void Add(Dictionary<string, SortedSet<string>> map, string key, string value)
    {
        if (!map.TryGetValue(key, out SortedSet<string>? entries))
        {
            entries = [];
            map[key] = entries;
        }

        _ = entries.Add(value);
    }

    private static Dictionary<short, OpCode> BuildOpCodeTable()
    {
        Dictionary<short, OpCode> table = [];

        foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is OpCode opCode)
            {
                table[opCode.Value] = opCode;
            }
        }

        return table;
    }

    private static int GetOperandByteLength(OperandType operandType)
    {
        return operandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            _ => 4
        };
    }

    private static string? TryGetQualifiedName(MetadataReader reader, int token)
    {
        EntityHandle handle = MetadataTokens.EntityHandle(token);

        if (handle.Kind is HandleKind.MethodDefinition)
        {
            MethodDefinition definition = reader.GetMethodDefinition((MethodDefinitionHandle)handle);
            TypeDefinition owner = reader.GetTypeDefinition(definition.GetDeclaringType());

            return $"{reader.GetString(owner.Name)}.{reader.GetString(definition.Name)}";
        }

        if (handle.Kind is HandleKind.MemberReference)
        {
            MemberReference reference = reader.GetMemberReference((MemberReferenceHandle)handle);

            string? owner = reference.Parent.Kind switch
            {
                HandleKind.TypeReference => reader.GetString(reader.GetTypeReference((TypeReferenceHandle)reference.Parent).Name),
                HandleKind.TypeDefinition => reader.GetString(reader.GetTypeDefinition((TypeDefinitionHandle)reference.Parent).Name),
                HandleKind.TypeSpecification => reader
                    .GetTypeSpecification((TypeSpecificationHandle)reference.Parent)
                    .DecodeSignature(TypeNameProvider.Instance, null),
                _ => null
            };

            return owner is null ? null : $"{owner}.{reader.GetString(reference.Name)}";
        }

        if (handle.Kind is HandleKind.MethodSpecification)
        {
            MethodSpecification specification = reader.GetMethodSpecification((MethodSpecificationHandle)handle);

            return TryGetQualifiedName(reader, MetadataTokens.GetToken(specification.Method));
        }

        return null;
    }

    /// <summary>
    /// Reads the name of the type a signature refers to.
    /// </summary>
    /// <remarks>
    /// A call a generic type makes to one of its own members carries a type specification rather than a type
    /// definition, so the name of the generic type has to be decoded out of the instantiation to see the edge.
    /// </remarks>
    private sealed class TypeNameProvider : ISignatureTypeProvider<string, object?>
    {
        public static readonly TypeNameProvider Instance = new();

        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
        {
            return genericType;
        }

        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            return reader.GetString(reader.GetTypeDefinition(handle).Name);
        }

        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            return reader.GetString(reader.GetTypeReference(handle).Name);
        }

        public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
        {
            return reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
        }

        public string GetSZArrayType(string elementType)
        {
            return elementType;
        }

        public string GetArrayType(string elementType, ArrayShape shape)
        {
            return elementType;
        }

        public string GetByReferenceType(string elementType)
        {
            return elementType;
        }

        public string GetPointerType(string elementType)
        {
            return elementType;
        }

        public string GetPinnedType(string elementType)
        {
            return elementType;
        }

        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired)
        {
            return unmodifiedType;
        }

        public string GetPrimitiveType(PrimitiveTypeCode typeCode)
        {
            return typeCode.ToString();
        }

        public string GetFunctionPointerType(MethodSignature<string> signature)
        {
            return "*";
        }

        public string GetGenericMethodParameter(object? genericContext, int index)
        {
            return "!!";
        }

        public string GetGenericTypeParameter(object? genericContext, int index)
        {
            return "!";
        }
    }
}
