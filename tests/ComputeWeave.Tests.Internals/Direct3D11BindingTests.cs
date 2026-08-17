using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

/// <summary>
/// Verifies the native bindings the Direct3D11 provider calls through.
/// </summary>
/// <remarks>
/// The expected values are written here as literals, taken from section 11.3 and 11.4 of the interop host
/// support specification. Reading them back out of the implementation would assert nothing.
/// </remarks>
[TestClass]
public class Direct3D11BindingTests
{
    /// <summary>
    /// The vtable slot each binding must call through.
    /// </summary>
    private static readonly (string Type, string Method, int Slot)[] Slots =
    [
        ("ID3D11Device1", "OpenSharedResource1", 48),
        ("ID3D11Device5", "OpenSharedFence", 67),
        ("ID3D11DeviceContext4", "Flush", 111),
        ("ID3D11DeviceContext4", "Signal", 147),
        ("ID3D11DeviceContext4", "Wait", 148),
        ("ID2D1DeviceContext", "CreateBitmapFromDxgiSurface", 62),
        ("IDXGIDevice", "GetAdapter", 7),
        ("IDXGIAdapter", "GetDesc", 8)
    ];

    /// <summary>
    /// The interface identifier each binding must carry.
    /// </summary>
    private static readonly (string Type, string Iid)[] Iids =
    [
        ("ID3D11Device1", "a04bfb29-08ef-43d6-a49c-a9bdbdcbe686"),
        ("ID3D11Device5", "8ffde202-a0e7-45df-9e01-e837801b5ea0"),
        ("ID3D11DeviceContext4", "917600da-f58c-4c33-98d8-3e15b390fa24"),
        ("ID3D11Fence", "affde9d1-1df7-4bb7-8a34-0f46251dab80"),
        ("ID3D11Texture2D", "6f15aaf2-d208-4e89-9ab4-489535d34f9c"),
        ("ID2D1DeviceContext", "e8f7fe7a-191c-466d-ad95-975678bda998"),
        ("ID2D1Bitmap1", "a898a84c-3873-4588-b08b-ebbf978df041"),
        ("IDXGIDevice", "54ec77fa-1377-44e6-8c32-88fd5f44c84c"),
        ("IDXGISurface", "cafcb56c-6ac3-4889-bf47-9e23bbd260ec")
    ];

    /// <summary>
    /// The binding types Phase 2 added.
    /// </summary>
    private static readonly string[] AddedTypes =
    [
        "ID3D11Device1",
        "ID3D11Device5",
        "ID3D11DeviceContext4",
        "ID3D11Fence",
        "ID3D11Texture2D",
        "ID2D1DeviceContext",
        "ID2D1Bitmap1",
        "IDXGIDevice",
        "IDXGISurface",
        "D2D1_BITMAP_PROPERTIES1",
        "D2D1_BITMAP_OPTIONS",
        "D2D1_PIXEL_FORMAT",
        "D2D1_ALPHA_MODE"
    ];

    /// <summary>
    /// The assembly the bindings live in.
    /// </summary>
    private static Assembly BindingAssembly => typeof(GraphicsDevice).Assembly;

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
    /// Reads every integer constant an IL stream loads.
    /// </summary>
    /// <param name="il">The IL stream to read.</param>
    /// <returns>The loaded constants, in order.</returns>
    /// <remarks>
    /// The short forms loading zero through eight are single byte opcodes. Reading only the <c>ldc.i4.s</c> and
    /// <c>ldc.i4</c> forms would silently miss every slot below nine.
    /// </remarks>
    private static List<int> ReadInt32Constants(byte[] il)
    {
        List<int> constants = [];

        for (int i = 0; i < il.Length;)
        {
            byte opcode = il[i];

            if (opcode is >= 0x16 and <= 0x1E)
            {
                constants.Add(opcode - 0x16);
                i++;
            }
            else if (opcode == 0x1F)
            {
                constants.Add((sbyte)il[i + 1]);
                i += 2;
            }
            else if (opcode == 0x20)
            {
                constants.Add(BitConverter.ToInt32(il, i + 1));
                i += 5;
            }
            else
            {
                i++;
            }
        }

        return constants;
    }

    [TestMethod]
    public void EachBindingCallsThroughItsDocumentedSlot()
    {
        foreach ((string typeName, string methodName, int slot) in Slots)
        {
            Type type = GetBindingType(typeName);
            MethodInfo? method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);

            Assert.IsNotNull(method, $"{typeName}.{methodName} is missing.");

            MethodBody? body = method.GetMethodBody();

            Assert.IsNotNull(body, $"{typeName}.{methodName} has no body.");

            byte[]? il = body.GetILAsByteArray();

            Assert.IsNotNull(il, $"{typeName}.{methodName} has no IL.");

            List<int> constants = ReadInt32Constants(il);

            Assert.IsTrue(
                constants.Contains(slot),
                $"{typeName}.{methodName} does not call through slot {slot}. Constants: {string.Join(", ", constants)}.");
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

            Guid actual = ReadGuid(getter);

            Assert.AreEqual(Guid.Parse(expected), actual, $"{typeName} carries the wrong interface identifier.");
        }
    }

    [TestMethod]
    public void EveryAddedBindingStaysInternal()
    {
        foreach (string name in AddedTypes)
        {
            Type type = GetBindingType(name);

            Assert.IsFalse(type.IsPublic, $"The binding '{name}' is public.");
            Assert.IsFalse(type.IsNestedPublic, $"The binding '{name}' is publicly nested.");
        }
    }

    /// <summary>
    /// Invokes the interface identifier getter of a binding and reads the value back.
    /// </summary>
    /// <param name="getter">The getter to invoke.</param>
    /// <returns>The interface identifier the binding carries.</returns>
    private static unsafe Guid ReadGuid(MethodInfo getter)
    {
        object? result = getter.Invoke(null, null);

        Assert.IsNotNull(result);

        return *(Guid*)Pointer.Unbox(result);
    }
}
