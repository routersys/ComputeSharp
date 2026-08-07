using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using ComputeWeave.Resources.Lifetime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

[TestClass]
public class ResourceGenerationReferenceCallSiteTests
{
    private enum Exclusion
    {
        SlotGate,
        HazardGate,
        ReferenceTrackerLease,
        UnpublishedGeneration,
        UnsynchronizedPoisonPath
    }

    private static readonly string[] AcquisitionPrimitives =
    [
        "TryAcquireCpuReference",
        "TryAcquireExternalReference",
        "TryAcquireNativeReference",
        "TryAcquirePersistentLease",
        "TryAcquireRecordingReference"
    ];

    private static readonly string[] ActiveExitPrimitives =
    [
        "TryMarkFaulted",
        "TryMarkTerminalRetained",
        "TryRequestRetire"
    ];

    private static readonly (string Caller, string Target, Exclusion Exclusion)[] ApprovedCallSites =
    [
        ("ComputeSubmissionExecutor.FaultExternalGenerations", "TryMarkFaulted", Exclusion.UnsynchronizedPoisonPath),
        ("GraphicsDevice.AcquireNativeResource", "TryAcquireNativeReference", Exclusion.HazardGate),
        ("GraphicsDevice.BeginCpuAccess", "TryAcquireCpuReference", Exclusion.HazardGate),
        ("PreparedGenerationRollback.RollbackUnpublished", "TryRequestRetire", Exclusion.UnpublishedGeneration),
        ("ResourceGenerationOwner.ReleaseUnpublished", "TryRequestRetire", Exclusion.UnpublishedGeneration),
        ("ResourceGenerationPinTracker.TryPin", "TryAcquireRecordingReference", Exclusion.ReferenceTrackerLease),
        ("SlotControlRecord.MarkTerminalRetained", "TryMarkTerminalRetained", Exclusion.SlotGate),
        ("SlotControlRecord.RetireAndReleaseOwnership", "TryRequestRetire", Exclusion.SlotGate),
        ("SlotControlRecord.TryAcquirePersistentLease", "TryAcquireExternalReference", Exclusion.SlotGate),
        ("SlotControlRecord.TryAcquirePersistentLease", "TryAcquirePersistentLease", Exclusion.SlotGate),
        ("SlotControlRecord.TryPin", "TryAcquireRecordingReference", Exclusion.SlotGate),
        ("SlotControlRecord.TryPinActiveExternal", "TryAcquireExternalReference", Exclusion.SlotGate)
    ];

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

    private static string? TryGetTargetName(MetadataReader reader, int token)
    {
        EntityHandle handle = MetadataTokens.EntityHandle(token);

        if (handle.Kind is not HandleKind.MethodDefinition)
        {
            return null;
        }

        MethodDefinition definition = reader.GetMethodDefinition((MethodDefinitionHandle)handle);
        TypeDefinition owner = reader.GetTypeDefinition(definition.GetDeclaringType());

        if (reader.GetString(owner.Name) is not nameof(ResourceGenerationRecord))
        {
            return null;
        }

        return reader.GetString(definition.Name);
    }

    private static SortedSet<string> ScanCallSites()
    {
        SortedSet<string> callSites = [];
        Dictionary<short, OpCode> table = BuildOpCodeTable();
        HashSet<string> watched = [.. AcquisitionPrimitives, .. ActiveExitPrimitives];

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

            byte[]? il = peReader.GetMethodBody(method.RelativeVirtualAddress).GetILBytes();

            if (il is null)
            {
                continue;
            }

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
                    Assert.Fail($"The instruction stream of {reader.GetString(method.Name)} carries an unknown opcode.");
                }

                if (opCode.OperandType is OperandType.InlineSwitch)
                {
                    offset += 4 + (4 * BitConverter.ToInt32(il, offset));

                    continue;
                }

                if (opCode.OperandType is OperandType.InlineMethod &&
                    TryGetTargetName(reader, BitConverter.ToInt32(il, offset)) is string target &&
                    watched.Contains(target))
                {
                    TypeDefinition caller = reader.GetTypeDefinition(method.GetDeclaringType());

                    _ = callSites.Add($"{reader.GetString(caller.Name)}.{reader.GetString(method.Name)} -> {target}");
                }

                offset += GetOperandByteLength(opCode.OperandType);
            }
        }

        return callSites;
    }

    [TestMethod]
    public void KeepsEveryGenerationReferenceCallSiteApproved()
    {
        SortedSet<string> observed = ScanCallSites();
        SortedSet<string> approved = [.. ApprovedCallSites.Select(static entry => $"{entry.Caller} -> {entry.Target}")];

        string added = string.Join(", ", observed.Except(approved));
        string removed = string.Join(", ", approved.Except(observed));

        Assert.AreEqual(string.Empty, added, "An unapproved call site mutates a generation reference.");
        Assert.AreEqual(string.Empty, removed, "An approved call site no longer exists.");
    }

    [TestMethod]
    public void TracksEveryAcquisitionPrimitiveOfTheRecord()
    {
        SortedSet<string> declared =
        [
            .. typeof(ResourceGenerationRecord)
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(static method => method.Name)
                .Where(static name => name.StartsWith("TryAcquire", StringComparison.Ordinal))
        ];

        Assert.AreEqual(
            string.Empty,
            string.Join(", ", declared.Except(AcquisitionPrimitives)),
            "An untracked acquisition primitive was added to the record.");

        Assert.AreEqual(
            string.Empty,
            string.Join(", ", AcquisitionPrimitives.Except(declared)),
            "A tracked acquisition primitive no longer exists.");
    }

    [TestMethod]
    public void NamesAnAdmissibleExclusionForEveryApprovedCallSite()
    {
        foreach ((string caller, string target, Exclusion exclusion) in ApprovedCallSites)
        {
            Assert.IsTrue(Enum.IsDefined(exclusion), $"{caller} -> {target}");
            Assert.IsTrue(
                Array.IndexOf(AcquisitionPrimitives, target) >= 0 || Array.IndexOf(ActiveExitPrimitives, target) >= 0,
                $"{caller} -> {target}");
        }

        Assert.AreEqual(
            ApprovedCallSites.Length,
            ApprovedCallSites.Select(static entry => $"{entry.Caller} -> {entry.Target}").Distinct().Count());
    }
}
