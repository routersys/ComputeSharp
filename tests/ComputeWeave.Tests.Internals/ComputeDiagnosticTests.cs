using System;
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
}
