using System;
using System.Linq;
using System.Reflection;
using ComputeWeave.D2D1.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.D2D1.Tests;

/// <summary>
/// Verifies the native bindings the Direct2D authoring path calls through.
/// </summary>
/// <remarks>
/// <para>
/// A vtable slot number that is wrong compiles cleanly and calls a different function. Nothing in the
/// type system objects and the symptom appears far from the cause. The numbers written here were
/// checked against the Windows SDK headers over two SDK versions, by flattening each interface's
/// inheritance and reading its declarations in order; the procedure and its result are recorded in the
/// Direct2D authoring specification. Reading them back out of the bindings would assert nothing, so
/// they are literals, and a later edit to a binding has to disagree with them out loud.
/// </para>
/// <para>
/// Covering them this way matters more here than a behavioural test can express. Of the 73 slots, 47
/// have no caller in the shipped assembly at all, and 7 more are called but reached by no test, so
/// moving any of those 54 is invisible to the suite. Reading the numbers out of the IL covers all 73
/// alike, and does not need a device.
/// </para>
/// </remarks>
[TestClass]
public class D2D1BindingTests
{
    /// <summary>
    /// The vtable slot each binding must call through.
    /// </summary>
    private static readonly (string Type, string Method, int Slot)[] Slots =
    [
        ("ID2D1Device1", "QueryInterface", 0),
        ("ID2D1Device1", "AddRef", 1),
        ("ID2D1Device1", "Release", 2),
        ("ID2D1Device1", "GetFactory", 3),
        ("ID2D1DeviceContext", "GetFactory", 3),
        ("ID2D1DeviceContext", "GetDpi", 52),
        ("ID2D1DeviceContext", "CreateEffect", 63),
        ("ID2D1DeviceContext", "GetTarget", 75),
        ("ID2D1DrawInfo", "AddRef", 1),
        ("ID2D1DrawInfo", "Release", 2),
        ("ID2D1DrawInfo", "SetInputDescription", 3),
        ("ID2D1DrawInfo", "SetOutputBuffer", 4),
        ("ID2D1DrawInfo", "SetPixelShaderConstantBuffer", 7),
        ("ID2D1DrawInfo", "SetResourceTexture", 8),
        ("ID2D1DrawInfo", "SetPixelShader", 10),
        ("ID2D1DrawTransform", "QueryInterface", 0),
        ("ID2D1DrawTransform", "AddRef", 1),
        ("ID2D1DrawTransform", "Release", 2),
        ("ID2D1DrawTransform", "GetInputCount", 3),
        ("ID2D1DrawTransform", "MapOutputRectToInputRects", 4),
        ("ID2D1DrawTransform", "MapInputRectsToOutputRect", 5),
        ("ID2D1DrawTransform", "MapInvalidRect", 6),
        ("ID2D1DrawTransform", "SetDrawInfo", 7),
        ("ID2D1Effect", "SetValue", 9),
        ("ID2D1Effect", "GetValue", 11),
        ("ID2D1Effect", "SetInput", 14),
        ("ID2D1Effect", "GetInput", 16),
        ("ID2D1EffectContext", "QueryInterface", 0),
        ("ID2D1EffectContext", "AddRef", 1),
        ("ID2D1EffectContext", "Release", 2),
        ("ID2D1EffectContext", "CreateEffect", 4),
        ("ID2D1EffectContext", "GetMaximumSupportedFeatureLevel", 5),
        ("ID2D1EffectContext", "LoadPixelShader", 11),
        ("ID2D1EffectContext", "IsShaderLoaded", 14),
        ("ID2D1EffectContext", "CreateResourceTexture", 15),
        ("ID2D1EffectContext", "CheckFeatureSupport", 22),
        ("ID2D1EffectImpl", "QueryInterface", 0),
        ("ID2D1EffectImpl", "AddRef", 1),
        ("ID2D1EffectImpl", "Release", 2),
        ("ID2D1EffectImpl", "Initialize", 3),
        ("ID2D1EffectImpl", "PrepareForRender", 4),
        ("ID2D1EffectImpl", "SetGraph", 5),
        ("ID2D1Factory1", "RegisterEffectFromString", 23),
        ("ID2D1Image", "QueryInterface", 0),
        ("ID2D1Image", "AddRef", 1),
        ("ID2D1Image", "Release", 2),
        ("ID2D1Image", "GetFactory", 3),
        ("ID2D1Multithread", "QueryInterface", 0),
        ("ID2D1Multithread", "AddRef", 1),
        ("ID2D1Multithread", "Release", 2),
        ("ID2D1Multithread", "GetMultithreadProtected", 3),
        ("ID2D1Multithread", "Enter", 4),
        ("ID2D1Multithread", "Leave", 5),
        ("ID2D1ResourceTexture", "QueryInterface", 0),
        ("ID2D1ResourceTexture", "AddRef", 1),
        ("ID2D1ResourceTexture", "Release", 2),
        ("ID2D1ResourceTexture", "Update", 3),
        ("ID2D1TransformGraph", "SetSingleTransformNode", 4),
        ("ID3D11ShaderReflection", "QueryInterface", 0),
        ("ID3D11ShaderReflection", "AddRef", 1),
        ("ID3D11ShaderReflection", "Release", 2),
        ("ID3D11ShaderReflection", "GetDesc", 3),
        ("ID3D11ShaderReflection", "GetMovInstructionCount", 12),
        ("ID3D11ShaderReflection", "GetMovcInstructionCount", 13),
        ("ID3D11ShaderReflection", "GetConversionInstructionCount", 14),
        ("ID3D11ShaderReflection", "GetBitwiseInstructionCount", 15),
        ("ID3D11ShaderReflection", "GetGSInputPrimitive", 16),
        ("ID3D11ShaderReflection", "GetNumInterfaceSlots", 18),
        ("ID3D11ShaderReflection", "GetMinFeatureLevel", 19),
        ("ID3D11ShaderReflection", "GetThreadGroupSize", 20),
        ("ID3D11ShaderReflection", "GetRequiresFlags", 21),
        ("ID3DInclude", "Open", 0),
        ("ID3DInclude", "Close", 1)
    ];

    /// <summary>
    /// The interface identifier each binding must carry.
    /// </summary>
    private static readonly (string Type, string Iid)[] Iids =
    [
        ("ID2D1Device1", "d21768e1-23a4-4823-a14b-7c3eba85d658"),
        ("ID2D1DrawTransform", "36bfdcb6-9739-435d-a30d-a653beff6a6f"),
        ("ID2D1EffectImpl", "a248fd3f-3e6c-4e63-9f03-7f68ecc91db9"),
        ("ID2D1Factory1", "bb12d362-daee-4b9a-aa1d-14ba401cfa1f"),
        ("ID2D1Image", "65019f75-8da2-497c-b32c-dfa34e48ede6"),
        ("ID2D1Multithread", "31e6e7bc-e0ff-4d46-8c64-a0a8c41c15d3"),
        ("ID3D11ShaderReflection", "8d536ca1-0cca-4956-a837-786963755584")
    ];

    /// <summary>
    /// The assembly the bindings live in.
    /// </summary>
    private static Assembly BindingAssembly => typeof(D2D1ShaderCompiler).Assembly;

    /// <summary>
    /// Checks that each binding indexes the vtable at the slot recorded above.
    /// </summary>
    [TestMethod]
    public void EachBindingCallsThroughItsDocumentedSlot()
    {
        foreach ((string typeName, string methodName, int slot) in Slots)
        {
            Type type = GetBindingType(typeName);
            MethodInfo? method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(method, $"{typeName}.{methodName} is missing.");

            Assert.AreEqual(
                slot,
                ReadVtableIndex(type, method),
                $"{typeName}.{methodName} does not call through slot {slot}.");
        }
    }

    [TestMethod]
    public void EachBindingCarriesItsDocumentedInterfaceIdentifier()
    {
        foreach ((string typeName, string expected) in Iids)
        {
            Type type = GetBindingType(typeName);
            Type comObject = BindingAssembly.GetType("ComputeWeave.Win32.IComObject")!;

            Assert.IsTrue(comObject.IsAssignableFrom(type), $"{typeName} does not implement IComObject.");

            InterfaceMapping mapping = type.GetInterfaceMap(comObject);
            MethodInfo getter = mapping.TargetMethods.Single(static m => m.Name.EndsWith("get_IID", StringComparison.Ordinal));

            Assert.AreEqual(Guid.Parse(expected), ReadGuid(getter), $"{typeName} carries the wrong interface identifier.");
        }
    }

    /// <summary>
    /// Checks that no binding method has been added without a row above.
    /// </summary>
    /// <remarks>
    /// A method added to a binding would otherwise be covered by nothing, the table being a list this
    /// test reads rather than a set it derives. Comparing the counts per type turns that into a failure.
    /// </remarks>
    [TestMethod]
    public void EveryBindingMethodIsListed()
    {
        foreach (IGrouping<string, (string Type, string Method, int Slot)> group in Slots.GroupBy(static s => s.Type))
        {
            Type type = GetBindingType(group.Key);
            MethodInfo[] declared = type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            Assert.AreEqual(
                group.Count(),
                declared.Length,
                $"{group.Key} declares {declared.Length} methods but {group.Count()} are listed: {string.Join(", ", declared.Select(static m => m.Name))}.");
        }
    }

    /// <summary>
    /// Gets a binding type by its name.
    /// </summary>
    /// <param name="name">The name of the type to look up.</param>
    /// <returns>The matching type.</returns>
    private static Type GetBindingType(string name)
    {
        Type? type = BindingAssembly.GetType($"ComputeWeave.Win32.{name}", throwOnError: false);

        Assert.IsNotNull(type, $"The binding '{name}' is missing.");

        return type;
    }

    /// <summary>
    /// Reads the interface identifier a compiled getter returns.
    /// </summary>
    /// <param name="getter">The getter to invoke.</param>
    /// <returns>The identifier it returns.</returns>
    private static unsafe Guid ReadGuid(MethodInfo getter)
    {
        object? result = getter.Invoke(null, null);

        Assert.IsNotNull(result);

        return *(Guid*)Pointer.Unbox(result);
    }

    /// <summary>
    /// Reads the vtable index a binding method indexes with.
    /// </summary>
    /// <param name="type">The binding the method belongs to.</param>
    /// <param name="method">The method to read.</param>
    /// <returns>The index, or -1 when the method never loads the vtable.</returns>
    /// <remarks>
    /// <para>
    /// The index is read where it is, immediately after the load of the <c>lpVtbl</c> field, rather than
    /// by collecting every constant the method loads. Collecting them needs a walker that knows the
    /// length of every opcode; one that does not, and steps a byte at a time instead, reads operand bytes
    /// as if they were instructions. Measured: <c>sizeof</c> is the two bytes <c>FE 1C</c>, and a byte
    /// stepping reader takes the second of them for <c>ldc.i4.6</c>.
    /// </para>
    /// <para>
    /// The compiler writes the offset three different ways, because it folds what it can. Indexing by
    /// zero needs no arithmetic at all, so the dereference follows the field load directly. Indexing by
    /// one is the size of a pointer, with no multiply. Every larger index loads the number, then
    /// multiplies. All three are read here, and all 73 of them agree with what the Windows SDK says the
    /// slot is, which is what says this reader is reading the right thing.
    /// </para>
    /// </remarks>
    private static int ReadVtableIndex(Type type, MethodInfo method)
    {
        FieldInfo? field = type.GetField("lpVtbl", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(field, $"{type.Name} has no lpVtbl field.");

        byte[]? il = method.GetMethodBody()?.GetILAsByteArray();

        Assert.IsNotNull(il, $"{type.Name}.{method.Name} has no IL.");

        for (int i = 0; i + 5 <= il.Length; i++)
        {
            // ldfld, then the token of the field being loaded.
            if (il[i] != 0x7B || BitConverter.ToInt32(il, i + 1) != field.MetadataToken)
            {
                continue;
            }

            int at = i + 5;

            if (at >= il.Length)
            {
                break;
            }

            return il[at] switch
            {
                0x4D => 0,                                  // ldind.i, so no offset was added
                0xFE => 1,                                  // sizeof on its own, the multiply folded away
                >= 0x16 and <= 0x1E => il[at] - 0x16,       // ldc.i4.0 through ldc.i4.8
                0x1F => (sbyte)il[at + 1],                  // ldc.i4.s
                0x20 => BitConverter.ToInt32(il, at + 1),   // ldc.i4
                _ => -1
            };
        }

        return -1;
    }
}
