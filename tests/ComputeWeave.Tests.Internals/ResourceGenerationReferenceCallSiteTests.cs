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

    private const string RecordType = nameof(ResourceGenerationRecord);

    private const string SlotGateType = "SlotGate";

    private const string SpinLockEntry = "SpinLock.Enter";

    private const string HazardGateEntry = "Lock.EnterScope";

    private static readonly string[] LeaseEntries =
    [
        "ID3D12ReadOnlyResource.ValidateAndGetID3D12Resource",
        "ReferenceTracker.GetLease"
    ];

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

            string owner = reference.Parent.Kind switch
            {
                HandleKind.TypeReference => reader.GetString(reader.GetTypeReference((TypeReferenceHandle)reference.Parent).Name),
                HandleKind.TypeDefinition => reader.GetString(reader.GetTypeDefinition((TypeDefinitionHandle)reference.Parent).Name),
                _ => null!
            };

            return owner is null ? null : $"{owner}.{reader.GetString(reference.Name)}";
        }

        return null;
    }

    private static HashSet<string> BuildWatchedTargets()
    {
        HashSet<string> watched = [SpinLockEntry, HazardGateEntry, .. LeaseEntries];

        foreach (string primitive in AcquisitionPrimitives.Concat(ActiveExitPrimitives))
        {
            _ = watched.Add($"{RecordType}.{primitive}");
        }

        return watched;
    }

    private static (Dictionary<string, SortedSet<string>> Calls, SortedSet<string> PublicSlotGateMembers) Scan()
    {
        Dictionary<string, SortedSet<string>> calls = [];
        SortedSet<string> publicSlotGateMembers = [];
        Dictionary<short, OpCode> table = BuildOpCodeTable();
        HashSet<string> watched = BuildWatchedTargets();

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

            if (reader.GetString(owner.Name) is SlotGateType &&
                !method.Attributes.HasFlag(MethodAttributes.Static) &&
                (method.Attributes & MethodAttributes.MemberAccessMask) is MethodAttributes.Public)
            {
                _ = publicSlotGateMembers.Add(caller);
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
                    Assert.Fail($"The instruction stream of {caller} carries an unknown opcode.");
                }

                if (opCode.OperandType is OperandType.InlineSwitch)
                {
                    offset += 4 + (4 * BitConverter.ToInt32(il, offset));

                    continue;
                }

                if (opCode.OperandType is OperandType.InlineMethod &&
                    TryGetQualifiedName(reader, BitConverter.ToInt32(il, offset)) is string target &&
                    watched.Contains(target))
                {
                    if (!calls.TryGetValue(caller, out SortedSet<string>? targets))
                    {
                        targets = [];
                        calls[caller] = targets;
                    }

                    _ = targets.Add(target);
                }

                offset += GetOperandByteLength(opCode.OperandType);
            }
        }

        return (calls, publicSlotGateMembers);
    }

    private static SortedSet<string> CollectCallSites(Dictionary<string, SortedSet<string>> calls)
    {
        SortedSet<string> callSites = [];

        foreach ((string caller, SortedSet<string> targets) in calls)
        {
            foreach (string target in targets.Where(static target => target.StartsWith($"{RecordType}.", StringComparison.Ordinal)))
            {
                _ = callSites.Add($"{caller} -> {target[(RecordType.Length + 1)..]}");
            }
        }

        return callSites;
    }

    [TestMethod]
    public void KeepsEveryGenerationReferenceCallSiteApproved()
    {
        SortedSet<string> observed = CollectCallSites(Scan().Calls);
        SortedSet<string> approved = [.. ApprovedCallSites.Select(static entry => $"{entry.Caller} -> {entry.Target}")];

        Assert.AreEqual(
            string.Empty,
            string.Join(", ", observed.Except(approved)),
            "An unapproved call site mutates a generation reference.");

        Assert.AreEqual(
            string.Empty,
            string.Join(", ", approved.Except(observed)),
            "An approved call site no longer exists.");
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

    [TestMethod]
    public void EntersTheDeclaredExclusionAtEveryGatedCallSite()
    {
        Dictionary<string, SortedSet<string>> calls = Scan().Calls;

        foreach ((string caller, string target, Exclusion exclusion) in ApprovedCallSites)
        {
            if (exclusion is not (Exclusion.HazardGate or Exclusion.ReferenceTrackerLease))
            {
                continue;
            }

            SortedSet<string> targets = calls[caller];

            bool isEntered = exclusion is Exclusion.HazardGate
                ? targets.Contains(HazardGateEntry)
                : LeaseEntries.Any(targets.Contains);

            Assert.IsTrue(isEntered, $"{caller} -> {target} declares {exclusion} but its body never enters it.");
        }
    }

    [TestMethod]
    public void EntersTheExclusionOnEveryPublicSlotGateMember()
    {
        (Dictionary<string, SortedSet<string>> calls, SortedSet<string> members) = Scan();

        Assert.IsTrue(members.Count > 0);

        string ungated = string.Join(
            ", ",
            members.Where(member => !calls.TryGetValue(member, out SortedSet<string>? targets) || !targets.Contains(SpinLockEntry)));

        Assert.AreEqual(string.Empty, ungated, "A public slot gate member reaches the slot control record without entering the exclusion.");
    }
}
