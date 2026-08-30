using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ComputeWeave.Interop;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

/// <summary>
/// Verifies the runtime diagnostic identifiers the specification declares.
/// </summary>
/// <remarks>
/// The expected values are written here as literals, taken from section 19.2 of the normative specification.
/// Reading them back out of the implementation would assert nothing.
/// </remarks>
[TestClass]
public class ComputeDiagnosticTests
{
    /// <summary>
    /// The outcome of a domain operation and the diagnostic that rejects it.
    /// </summary>
    private static readonly (DomainOperationStatus Status, string Id)[] DomainRejections =
    [
        (DomainOperationStatus.DomainUnavailable, "CMPW3003"),
        (DomainOperationStatus.PermitBusy, "CMPW3004"),
        (DomainOperationStatus.TokenExhausted, "CMPW3006"),
        (DomainOperationStatus.SchedulerBusy, "CMPW3007")
    ];

    [TestMethod]
    public void MapsEveryDomainRejectionToItsDocumentedIdentifier()
    {
        foreach ((DomainOperationStatus status, string id) in DomainRejections)
        {
            Assert.AreEqual(id, ComputeDiagnosticIds.FromDomainOperationStatus(status), $"{status} maps to the wrong diagnostic.");
        }
    }

    [TestMethod]
    public void CoversEveryRejectingOutcome()
    {
        // Acquired は拒否ではない。それ以外はすべて識別子を持たなければならない。
        DomainOperationStatus[] rejecting = Enum.GetValues<DomainOperationStatus>()
            .Where(static value => value is not DomainOperationStatus.Acquired)
            .ToArray();

        Assert.AreEqual(DomainRejections.Length, rejecting.Length, "A rejecting outcome has no documented diagnostic.");

        foreach (DomainOperationStatus status in rejecting)
        {
            Assert.IsTrue(
                DomainRejections.Any(pair => pair.Status == status),
                $"{status} is not covered by the diagnostic table.");
        }
    }

    [TestMethod]
    public void CarriesTheIdentifierOnTheException()
    {
        ComputeDiagnosticException exception = new("CMPW3007", "message");

        Assert.AreEqual("CMPW3007", exception.DiagnosticId);
        Assert.AreEqual("message", exception.Message);

        // 既存の利用者が書いている catch を壊さないこと。
        Assert.IsInstanceOfType<InvalidOperationException>(exception);
        Assert.IsInstanceOfType<IComputeDiagnostic>(exception);
    }

    [TestMethod]
    public void ReportsTheDeviceMismatchIdentifier()
    {
        Type type = typeof(GraphicsDeviceMismatchException);

        Assert.IsTrue(typeof(IComputeDiagnostic).IsAssignableFrom(type), "The device mismatch does not carry a diagnostic.");

        object instance = FormatterServicesCreate(type);

        Assert.AreEqual("CMPW1001", ((IComputeDiagnostic)instance).DiagnosticId);
    }

    [TestMethod]
    public void DeclaresEveryIdentifierWithTheProductPrefix()
    {
        FieldInfo[] fields = typeof(ComputeDiagnosticIds)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static field => field.FieldType == typeof(string))
            .ToArray();

        Assert.IsTrue(fields.Length >= 25, "The diagnostic table is incomplete.");

        foreach (FieldInfo field in fields)
        {
            string id = (string)field.GetValue(null)!;

            Assert.IsTrue(id.StartsWith("CMPW", StringComparison.Ordinal), $"{field.Name} does not use the product prefix.");
            Assert.AreEqual(8, id.Length, $"{field.Name} is not four digits.");
            Assert.IsTrue(id[4..].All(char.IsAsciiDigit), $"{field.Name} is not four digits.");
        }

        string[] ids = fields.Select(static field => (string)field.GetValue(null)!).ToArray();

        Assert.AreEqual(ids.Length, ids.Distinct().Count(), "A diagnostic identifier is declared twice.");
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RefusesToLeaseFromAProviderThatCannotOrderPersistentViews(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        FakeInteropProvider provider = new(graphicsDevice, scheduler)
        {
            Capabilities =
                ExternalInteropCapabilities.SharedFence |
                ExternalInteropCapabilities.SharedTexture2D |
                ExternalInteropCapabilities.SingleImmediateContextOrdering
        };

        using ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(provider);

        SharedTextureSlot<Bgra32, Float4, FakeExternalView> slot = new();

        // 共有スロットを宣言しただけでは拒まない。Persistent Lease を取った時点で拒む。
        ComputeInteropResourceSetRuntime resources = ComputeInteropResourceSetRuntime.Create(
            graphicsDevice,
            domain,
            InteropResourceSetRegistrationTests.ResourceSetDescriptor(1, ComputeSharedTextureInitialOwner.Compute),
            [slot]);

        try
        {
            Assert.IsTrue(slot.TryEnsure(4, 4, out _));

            NotSupportedException exception = Assert.ThrowsExactly<NotSupportedException>(() => _ = slot.AcquireExternalViewLease());

            Assert.IsTrue(
                exception.Message.Contains(nameof(ExternalInteropCapabilities.PersistentExternalViewOrdering), StringComparison.Ordinal),
                "The rejection does not name the missing capability.");
        }
        finally
        {
            resources.Dispose();
            resources.WaitForDisposal();
        }
    }

    /// <summary>
    /// Creates an instance of a type whose constructors are private.
    /// </summary>
    /// <param name="type">The type to create.</param>
    /// <returns>The created instance.</returns>
    private static object FormatterServicesCreate(Type type)
    {
        ConstructorInfo constructor = type
            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(static candidate => candidate.GetParameters() is [{ ParameterType.Name: nameof(String) }]);

        return constructor.Invoke(["message"]);
    }

    /// <summary>
    /// Every identifier the runtime can produce is named by a test.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The identifiers are constants, so a use inlines the value into the method that carries it and an
    /// identifier no code names appears in no string of the assembly. The produced set is therefore readable
    /// from the compiled form, and what is produced is what a consumer can catch on.
    /// </para>
    /// <para>
    /// The identifiers that appear nowhere are the ones section 19.2 of the specification records as having
    /// no reachable path, each with its reason. They need no test, and this assertion does not ask for one.
    /// Surfacing one of them without writing a test makes this fail.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void EveryIdentifierTheRuntimeProducesIsNamedByATest()
    {
        HashSet<string> produced = StringsOf(typeof(ComputeDiagnosticIds).Assembly);
        HashSet<string> named = StringsOf(typeof(ComputeDiagnosticTests).Assembly);

        string[] missing = [.. Identifiers().Where(id => produced.Contains(id) && !named.Contains(id)).Order()];

        Assert.AreEqual(0, missing.Length, $"Produced but no test names: {string.Join(", ", missing)}");
    }

    /// <summary>
    /// The identifier of every runtime diagnostic the table declares.
    /// </summary>
    /// <returns>The declared identifiers.</returns>
    private static IEnumerable<string> Identifiers()
    {
        foreach (FieldInfo field in typeof(ComputeDiagnosticIds).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.FieldType == typeof(string))
            {
                yield return (string)field.GetValue(null)!;
            }
        }
    }

    /// <summary>
    /// Every string an assembly's instructions can load.
    /// </summary>
    /// <param name="assembly">The assembly to read.</param>
    /// <returns>The strings the assembly's instructions can load.</returns>
    /// <remarks>
    /// The scan tries every byte offset, so it can resolve a value that is not an operand. Such a value is
    /// still one the assembly holds, because a token only resolves to a string that is already there, so a
    /// constant no code names cannot appear this way.
    /// </remarks>
    private static HashSet<string> StringsOf(Assembly assembly)
    {
        const BindingFlags Members =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        HashSet<string> strings = [];

        foreach (Type type in assembly.GetTypes())
        {
            foreach (MethodBase method in type.GetMethods(Members).Cast<MethodBase>().Concat(type.GetConstructors(Members)))
            {
                Collect(method, strings);
            }
        }

        return strings;
    }

    /// <summary>
    /// Adds every string a method body can load.
    /// </summary>
    /// <param name="method">The method to scan.</param>
    /// <param name="strings">The set to add to.</param>
    private static void Collect(MethodBase method, HashSet<string> strings)
    {
        byte[]? body = method.GetMethodBody()?.GetILAsByteArray();

        if (body is null)
        {
            return;
        }

        for (int i = 0; i < body.Length - 4; i++)
        {
            if (body[i] != 0x72)
            {
                continue;
            }

            try
            {
                _ = strings.Add(method.Module.ResolveString(BitConverter.ToInt32(body, i + 1)));
            }
            catch (ArgumentException)
            {
            }
        }
    }
}
