using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

/// <summary>
/// Fixes which callers may execute an external drain.
/// </summary>
/// <remarks>
/// Section 15.4 of the pipeline interop specification forbids running a final drain directly from a dispose or
/// from the release of the last persistent lease, and INV-INT-009 reserves that execution to the maintenance
/// coordinator. Section 4 of the external drain maintenance specification carries those into the two paths of
/// the slot: a requester wakes the coordinator, and only the coordinator and the registry teardown execute.
/// </remarks>
[TestClass]
public class ExternalDrainExecutorTests
{
    private const string SlotType = "SharedTextureSlot`3";

    private const string Executor = $"{SlotType}.RunMaintenance";

    private const string ExecutorEntry = $"{SlotType}.ComputeWeave.Resources.Lifetime.IComputeSharedSlot.RunMaintenance";

    private static readonly string[] RequesterEntries =
    [
        $"{SlotType}.TryEnsure",
        $"{SlotType}.Dispose",
        $"{SlotType}.WaitForDisposal",
        $"{SlotType}.TryReplaceGeneration"
    ];

    private static readonly string[] DrainPhases =
    [
        $"{SlotType}.TryIssueFinalDrain",
        $"{SlotType}.TryCompleteFinalDrain",
        $"{SlotType}.TryReleaseExternalObjects",
        $"{SlotType}.TryRunExternalMaintenancePass"
    ];

    [TestMethod]
    public void ReachesTheDrainPhasesOnlyThroughTheExecutor()
    {
        AssemblyCallGraph graph = AssemblyCallGraph.Read();

        Assert.AreNotEqual(0, graph.GetCallees(Executor).Count, "the executor was not found in the assembly");

        foreach (string requester in RequesterEntries)
        {
            Assert.AreNotEqual(0, graph.GetCallees(requester).Count, $"{requester} was not found in the assembly");

            foreach (string phase in DrainPhases)
            {
                Assert.IsFalse(
                    graph.TryGetPath(requester, phase, out string path),
                    $"a requester reaches an external drain phase: {path}");
            }
        }
    }

    [TestMethod]
    public void EntersTheExecutorOnlyThroughTheSharedSlotInterface()
    {
        AssemblyCallGraph graph = AssemblyCallGraph.Read();

        IReadOnlyCollection<string> callers = graph.GetCallers(Executor);

        Assert.AreEqual(
            ExecutorEntry,
            string.Join(", ", callers.OrderBy(static caller => caller, System.StringComparer.Ordinal)),
            "the executor gained a caller outside the shared slot interface");
    }

    [TestMethod]
    public void RunsTheDrainPhasesOnlyFromAMaintenancePass()
    {
        AssemblyCallGraph graph = AssemblyCallGraph.Read();

        foreach (string phase in DrainPhases.Where(static phase => !phase.EndsWith("TryRunExternalMaintenancePass", System.StringComparison.Ordinal)))
        {
            Assert.AreEqual(
                $"{SlotType}.TryRunExternalMaintenancePass",
                string.Join(", ", graph.GetCallers(phase).OrderBy(static caller => caller, System.StringComparer.Ordinal)),
                $"{phase} gained a caller outside the maintenance pass");
        }
    }
}
