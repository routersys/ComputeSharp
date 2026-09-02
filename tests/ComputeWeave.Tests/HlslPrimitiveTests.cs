using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests;

[TestClass]
public partial class HlslPrimitiveTests
{
    [TestMethod]
    public void Float3x2_ImplicitConversions()
    {
        Matrix3x2 m3x2 = new(1, 2, 3, 4, 5, 6);

        float3x2 f3x2 = m3x2;

        Assert.AreEqual(m3x2.M11, f3x2.M11);
        Assert.AreEqual(m3x2.M12, f3x2.M12);
        Assert.AreEqual(m3x2.M21, f3x2.M21);
        Assert.AreEqual(m3x2.M22, f3x2.M22);
        Assert.AreEqual(m3x2.M31, f3x2.M31);
        Assert.AreEqual(m3x2.M32, f3x2.M32);

        Matrix3x2 roundTrip3x2 = f3x2;

        Assert.AreEqual(m3x2, roundTrip3x2);
    }

    [TestMethod]
    public void Float4x4_ImplicitConversions()
    {
        Matrix4x4 m4x4 = new(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16);

        float4x4 f4x4 = m4x4;

        Assert.AreEqual(m4x4.M11, f4x4.M11);
        Assert.AreEqual(m4x4.M12, f4x4.M12);
        Assert.AreEqual(m4x4.M13, f4x4.M13);
        Assert.AreEqual(m4x4.M14, f4x4.M14);
        Assert.AreEqual(m4x4.M21, f4x4.M21);
        Assert.AreEqual(m4x4.M22, f4x4.M22);
        Assert.AreEqual(m4x4.M23, f4x4.M23);
        Assert.AreEqual(m4x4.M24, f4x4.M24);
        Assert.AreEqual(m4x4.M31, f4x4.M31);
        Assert.AreEqual(m4x4.M32, f4x4.M32);
        Assert.AreEqual(m4x4.M33, f4x4.M33);
        Assert.AreEqual(m4x4.M34, f4x4.M34);
        Assert.AreEqual(m4x4.M41, f4x4.M41);
        Assert.AreEqual(m4x4.M42, f4x4.M42);
        Assert.AreEqual(m4x4.M43, f4x4.M43);
        Assert.AreEqual(m4x4.M44, f4x4.M44);

        Matrix4x4 roundTrip4x4 = f4x4;

        Assert.AreEqual(m4x4, roundTrip4x4);
    }

    // Which kinds take part in the multiplication operators is named in the templates, apart from the list they are generated from
    [TestMethod]
    public void MulOperatorsAreTheSameForEveryKindTheIntrinsicDeclares()
    {
        (string Name, string Scalar)[] kinds =
        [
            ("Bool", "Boolean"),
            ("Double", "Double"),
            ("Float", "Single"),
            ("Int", "Int32"),
            ("UInt", "UInt32")
        ];

        List<string> declared = [];

        // Reading the kinds out of the intrinsic keeps the overload list the one source they come from
        foreach ((string name, string scalar) in kinds)
        {
            bool accepted = typeof(Hlsl)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(static method => method.Name == nameof(Hlsl.Mul))
                .SelectMany(static method => method.GetParameters())
                .Any(parameter => parameter.ParameterType.Name == scalar || IsShapeOfKind(parameter.ParameterType.Name, name));

            if (accepted)
            {
                declared.Add(name);
            }
        }

        Assert.IsTrue(declared.Count >= 2, "Fewer than two kinds take part, so comparing them says nothing.");

        List<string[]> surfaces = [];
        List<int> mixed = [];

        foreach (string kind in declared)
        {
            string scalar = kinds.Single(entry => entry.Name == kind).Scalar;
            List<string> signatures = [];
            int shapesThatDiffer = 0;

            foreach (Type type in typeof(Hlsl).Assembly.GetExportedTypes())
            {
                if (!IsShapeOfKind(type.Name, kind))
                {
                    continue;
                }

                foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (method.Name != "op_Multiply")
                    {
                        continue;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    string returned = EraseKind(method.ReturnType.Name, kind, scalar);
                    string first = EraseKind(parameters[0].ParameterType.Name, kind, scalar);
                    string second = EraseKind(parameters[1].ParameterType.Name, kind, scalar);

                    // The operators that reach the intrinsic are the ones whose two operands hold shapes that differ
                    if (first != second && first != "k" && second != "k")
                    {
                        shapesThatDiffer++;
                    }

                    signatures.Add($"{returned} {first} {second}");
                }
            }

            signatures.Sort(StringComparer.Ordinal);
            surfaces.Add([.. signatures]);
            mixed.Add(shapesThatDiffer);
        }

        int reference = 0;

        // The richest kind is the one to compare against, so a kind that is missing operators is the one named
        for (int i = 1; i < surfaces.Count; i++)
        {
            if (surfaces[i].Length > surfaces[reference].Length)
            {
                reference = i;
            }
        }

        Assert.IsTrue(mixed[reference] > 0, "No operator multiplies two shapes that differ, so the comparison below would hold over nothing.");

        for (int i = 0; i < surfaces.Count; i++)
        {
            if (i == reference)
            {
                continue;
            }

            CollectionAssert.AreEqual(
                surfaces[reference],
                surfaces[i],
                $"{declared[i]} does not declare the same multiplication operators as {declared[reference]}.");
        }
    }

    // Whether a type name is one of the vector or matrix shapes of a kind, such as Int3 or Int2x4
    private static bool IsShapeOfKind(string name, string kind)
    {
        if (!name.StartsWith(kind, StringComparison.Ordinal))
        {
            return false;
        }

        string shape = name[kind.Length..];

        return shape.Length switch
        {
            1 => char.IsAsciiDigit(shape[0]),
            3 => char.IsAsciiDigit(shape[0]) && shape[1] == 'x' && char.IsAsciiDigit(shape[2]),
            _ => false
        };
    }

    // The kind is erased from a type name so that the operators of two kinds can be compared as sets
    private static string EraseKind(string name, string kind, string scalar)
    {
        if (name == scalar)
        {
            return "k";
        }

        return IsShapeOfKind(name, kind) ? "K" + name[kind.Length..] : name;
    }
}