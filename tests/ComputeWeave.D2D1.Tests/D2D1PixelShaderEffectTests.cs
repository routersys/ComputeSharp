using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using ComputeWeave.D2D1;
using ComputeWeave.D2D1.Interop;
using ComputeWeave.D2D1.Tests.Effects;
using ComputeWeave.D2D1.Tests.Extensions;
using ComputeWeave.D2D1.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

[assembly: D2DEnableRuntimeCompilation]

#pragma warning disable IDE0022, IDE0044

namespace ComputeWeave.D2D1.Tests;

[TestClass]
public partial class D2D1PixelShaderEffectTests
{
    private const string ResourceTextureManagerPrefix = "ResourceTextureManager";

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException), AllowDerivedTypes = false)]
    public unsafe void RegisterForD2D1Factory1_NullD2D1Factory1()
    {
        D2D1PixelShaderEffect.RegisterForD2D1Factory1<InvertEffect>(null, out _);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException), AllowDerivedTypes = false)]
    public unsafe void RegisterForD2D1Factory1_WithTransformMapperFactory_NullD2D1Factory1()
    {
        D2D1PixelShaderEffect.RegisterForD2D1Factory1<PixelateEffect>(null, out _);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException), AllowDerivedTypes = false)]
    public unsafe void CreateFromD2D1DeviceContext_NullD2D1DeviceContext()
    {
        D2D1PixelShaderEffect.CreateFromD2D1DeviceContext<InvertEffect>(null, (void**)1);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException), AllowDerivedTypes = false)]
    public unsafe void CreateFromD2D1DeviceContext_NullD2D1Effect()
    {
        D2D1PixelShaderEffect.CreateFromD2D1DeviceContext<InvertEffect>((void*)1, null);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException), AllowDerivedTypes = false)]
    public unsafe void SetConstantBufferForD2D1Effect_NullD2D1Effect()
    {
        D2D1PixelShaderEffect.SetConstantBufferForD2D1Effect<InvertEffect>(null, default);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException), AllowDerivedTypes = false)]
    public unsafe void SetResourceTextureManagerForD2D1Effect_NullD2D1Effect()
    {
        D2D1PixelShaderEffect.SetResourceTextureManagerForD2D1Effect(null, (void*)1, 0);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException), AllowDerivedTypes = false)]
    public unsafe void SetResourceTextureManagerForD2D1Effect_NullD2D1ResourceTextureManager()
    {
        D2D1PixelShaderEffect.SetResourceTextureManagerForD2D1Effect((void*)1, (void*)null, 0);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException), AllowDerivedTypes = false)]
    public unsafe void SetResourceTextureManagerForD2D1Effect_RCW_NullD2D1Effect()
    {
        D2D1ResourceTextureManager resourceTextureManager = new(
            extents: [64],
            bufferPrecision: D2D1BufferPrecision.UInt8Normalized,
            channelDepth: D2D1ChannelDepth.One,
            filter: D2D1Filter.MinLinearMagMipPoint,
            extendModes: [D2D1ExtendMode.Clamp]);

        D2D1PixelShaderEffect.SetResourceTextureManagerForD2D1Effect(null, resourceTextureManager, 0);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException), AllowDerivedTypes = false)]
    public unsafe void SetResourceTextureManagerForD2D1Effect_RCW_NullD2D1ResourceTextureManager()
    {
        D2D1PixelShaderEffect.SetResourceTextureManagerForD2D1Effect((void*)1, (D2D1ResourceTextureManager)null!, 0);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException), AllowDerivedTypes = false)]
    public unsafe void SetTransformMapperForD2D1Effect_RCW_NullD2D1Effect()
    {
        D2D1PixelShaderEffect.SetTransformMapperForD2D1Effect(null, (void*)1);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException), AllowDerivedTypes = false)]
    public unsafe void SetTransformMapperForD2D1Effect_RCW_NullD2D1ResourceTextureManager()
    {
        D2D1PixelShaderEffect.SetTransformMapperForD2D1Effect((void*)1, (D2D1DrawTransformMapper<NullConstantBufferShader>)null!);
    }

    [TestMethod]
    [ExpectedException(typeof(Win32Exception))]
    public unsafe void NullConstantBuffer_DrawImageFails()
    {
        using ComPtr<ID2D1Factory2> d2D1Factory2 = D2D1Helper.CreateD2D1Factory2();
        using ComPtr<ID2D1Device> d2D1Device = D2D1Helper.CreateD2D1Device(d2D1Factory2.Get());
        using ComPtr<ID2D1DeviceContext> d2D1DeviceContext = D2D1Helper.CreateD2D1DeviceContext(d2D1Device.Get());

        D2D1PixelShaderEffect.RegisterForD2D1Factory1<NullConstantBufferShader>(d2D1Factory2.Get(), out _);

        using ComPtr<ID2D1Effect> d2D1Effect = default;

        D2D1PixelShaderEffect.CreateFromD2D1DeviceContext<NullConstantBufferShader>(d2D1DeviceContext.Get(), (void**)d2D1Effect.GetAddressOf());

        using ComPtr<ID2D1Bitmap> d2D1BitmapTarget = D2D1Helper.CreateD2D1BitmapAndSetAsTarget(d2D1DeviceContext.Get(), 128, 128);

        D2D1Helper.DrawEffect(d2D1DeviceContext.Get(), d2D1Effect.Get());
    }

    [D2DInputCount(0)]
    [D2DRequiresScenePosition]
    [D2DGeneratedPixelShaderDescriptor]
    [AutoConstructor]
    internal readonly partial struct NullConstantBufferShader : ID2D1PixelShader
    {
        private readonly float dummy;

        public float4 Execute()
        {
            return this.dummy;
        }
    }

    [TestMethod]
    public unsafe void GetValueSize_ConstantBuffer()
    {
        using ComPtr<ID2D1Factory2> d2D1Factory2 = D2D1Helper.CreateD2D1Factory2();
        using ComPtr<ID2D1Device> d2D1Device = D2D1Helper.CreateD2D1Device(d2D1Factory2.Get());
        using ComPtr<ID2D1DeviceContext> d2D1DeviceContext = D2D1Helper.CreateD2D1DeviceContext(d2D1Device.Get());

        D2D1PixelShaderEffect.RegisterForD2D1Factory1<ConstantBufferSizeTestShader>(d2D1Factory2.Get(), out _);

        using ComPtr<ID2D1Effect> d2D1Effect = default;

        D2D1PixelShaderEffect.CreateFromD2D1DeviceContext<ConstantBufferSizeTestShader>(d2D1DeviceContext.Get(), (void**)d2D1Effect.GetAddressOf());

        uint size = d2D1Effect.Get()->GetValueSize(D2D1PixelShaderEffectProperty.ConstantBuffer);

        Assert.AreEqual(D2D1PixelShader.GetConstantBufferSize<ConstantBufferSizeTestShader>(), (int)size);
    }

    [D2DInputCount(0)]
    [D2DRequiresScenePosition]
    [D2DGeneratedPixelShaderDescriptor]
    [AutoConstructor]
    internal readonly partial struct ConstantBufferSizeTestShader : ID2D1PixelShader
    {
        private readonly float a;
        private readonly float b;
        private readonly float3 c;
        private readonly int d;
        private readonly int e;

        public float4 Execute()
        {
            return this.a + this.b + this.c.X + this.d + this.e;
        }
    }

    [TestMethod]
    public void ResourceTextureManagerProperties_MatchTheirOwnNumbering()
    {
        uint[] properties = ResourceTextureManagerPropertyIndices();

        // An empty read would leave the walk below asserting nothing at all
        Assert.IsTrue(properties.Length > 0);

        // Callers reach a manager by adding its number to the first property, so the two must agree
        for (int i = 0; i < properties.Length; i++)
        {
            Assert.AreEqual(D2D1PixelShaderEffectProperty.ResourceTextureManager0 + (uint)i, properties[i]);
        }
    }

    [TestMethod]
    public void ResourceTextureManagerProperties_MatchTheMaximumShader()
    {
        // The tests that walk every index rest on this shader declaring one texture per property
        Assert.AreEqual(
            ResourceTextureManagerPropertyIndices().Length,
            D2D1PixelShader.GetResourceTextureCount<ShaderWithMaximumResourceTextures>());
    }

    // Ordered by the number in the name, so a swapped pair is a gap rather than a sorted-away one
    private static uint[] ResourceTextureManagerPropertyIndices()
    {
        return [.. typeof(D2D1PixelShaderEffectProperty)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static field => field.IsLiteral && ResourceTextureManagerNumber(field.Name) >= 0)
            .OrderBy(static field => ResourceTextureManagerNumber(field.Name))
            .Select(static field => (uint)field.GetRawConstantValue()!)];
    }

    // A property that only shares the prefix is not one of the numbered ones, so it is left out
    private static int ResourceTextureManagerNumber(string name)
    {
        return name.StartsWith(ResourceTextureManagerPrefix, StringComparison.Ordinal) &&
            int.TryParse(name.AsSpan(ResourceTextureManagerPrefix.Length), CultureInfo.InvariantCulture, out int number)
            ? number
            : -1;
    }

    [TestMethod]
    public unsafe void SetResourceTextureManagerForD2D1Effect_IndexRange()
    {
        using ComPtr<ID2D1Factory2> d2D1Factory2 = D2D1Helper.CreateD2D1Factory2();
        using ComPtr<ID2D1Device> d2D1Device = D2D1Helper.CreateD2D1Device(d2D1Factory2.Get());
        using ComPtr<ID2D1DeviceContext> d2D1DeviceContext = D2D1Helper.CreateD2D1DeviceContext(d2D1Device.Get());

        D2D1PixelShaderEffect.RegisterForD2D1Factory1<ShaderWithMaximumResourceTextures>(d2D1Factory2.Get(), out _);

        using ComPtr<ID2D1Effect> d2D1Effect = default;

        D2D1PixelShaderEffect.CreateFromD2D1DeviceContext<ShaderWithMaximumResourceTextures>(d2D1DeviceContext.Get(), (void**)d2D1Effect.GetAddressOf());

        // The accepted range must end where the properties of the effect end
        Assert.AreEqual(
            (uint)(2 + D2D1PixelShader.GetResourceTextureCount<ShaderWithMaximumResourceTextures>()),
            d2D1Effect.Get()->GetPropertyCount());

        using ComPtr<IUnknown> resourceTextureManager = default;

        D2D1ResourceTextureManager.Create((void**)resourceTextureManager.GetAddressOf());

        // The last index backed by a property is accepted and reaches the effect
        D2D1PixelShaderEffect.SetResourceTextureManagerForD2D1Effect(d2D1Effect.Get(), resourceTextureManager.Get(), 15);

        // One past that index is refused by the argument check, before the effect is used
        _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => D2D1PixelShaderEffect.SetResourceTextureManagerForD2D1Effect(d2D1Effect.Get(), resourceTextureManager.Get(), 16));
    }

    [TestMethod]
    public unsafe void SetResourceTextureManagerForD2D1Effect_RCW_IndexRange()
    {
        using ComPtr<ID2D1Factory2> d2D1Factory2 = D2D1Helper.CreateD2D1Factory2();
        using ComPtr<ID2D1Device> d2D1Device = D2D1Helper.CreateD2D1Device(d2D1Factory2.Get());
        using ComPtr<ID2D1DeviceContext> d2D1DeviceContext = D2D1Helper.CreateD2D1DeviceContext(d2D1Device.Get());

        D2D1PixelShaderEffect.RegisterForD2D1Factory1<ShaderWithMaximumResourceTextures>(d2D1Factory2.Get(), out _);

        using ComPtr<ID2D1Effect> d2D1Effect = default;

        D2D1PixelShaderEffect.CreateFromD2D1DeviceContext<ShaderWithMaximumResourceTextures>(d2D1DeviceContext.Get(), (void**)d2D1Effect.GetAddressOf());

        D2D1ResourceTextureManager resourceTextureManager = new(
            extents: [64],
            bufferPrecision: D2D1BufferPrecision.UInt8Normalized,
            channelDepth: D2D1ChannelDepth.One,
            filter: D2D1Filter.MinMagMipPoint,
            extendModes: [D2D1ExtendMode.Clamp]);

        // The last index backed by a property is accepted and reaches the effect
        D2D1PixelShaderEffect.SetResourceTextureManagerForD2D1Effect(d2D1Effect.Get(), resourceTextureManager, 15);

        // One past that index is refused by the argument check, before the effect is used
        _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => D2D1PixelShaderEffect.SetResourceTextureManagerForD2D1Effect(d2D1Effect.Get(), resourceTextureManager, 16));
    }

    [TestMethod]
    public unsafe void SetResourceTextureManagerForD2D1Effect_EveryIndexReachesItsOwnSlot()
    {
        using ComPtr<ID2D1Factory2> d2D1Factory2 = D2D1Helper.CreateD2D1Factory2();
        using ComPtr<ID2D1Device> d2D1Device = D2D1Helper.CreateD2D1Device(d2D1Factory2.Get());
        using ComPtr<ID2D1DeviceContext> d2D1DeviceContext = D2D1Helper.CreateD2D1DeviceContext(d2D1Device.Get());

        D2D1PixelShaderEffect.RegisterForD2D1Factory1<ShaderWithMaximumResourceTextures>(d2D1Factory2.Get(), out _);

        using ComPtr<ID2D1Effect> d2D1Effect = default;

        D2D1PixelShaderEffect.CreateFromD2D1DeviceContext<ShaderWithMaximumResourceTextures>(d2D1DeviceContext.Get(), (void**)d2D1Effect.GetAddressOf());

        AssertEveryIndexReachesItsOwnResourceTextureManager(
            d2D1Effect.Get(),
            D2D1PixelShader.GetResourceTextureCount<ShaderWithMaximumResourceTextures>());
    }

    [TestMethod]
    public unsafe void GetRegistrationBlob_EveryIndexReachesItsOwnSlot()
    {
        using ComPtr<ID2D1Factory2> d2D1Factory2 = D2D1Helper.CreateD2D1Factory2();
        using ComPtr<ID2D1Device> d2D1Device = D2D1Helper.CreateD2D1Device(d2D1Factory2.Get());
        using ComPtr<ID2D1DeviceContext> d2D1DeviceContext = D2D1Helper.CreateD2D1DeviceContext(d2D1Device.Get());

        ReadOnlyMemory<byte> blob = D2D1PixelShaderEffect.GetRegistrationBlob<ShaderWithMaximumResourceTextures>(out Guid effectId);

        D2D1Helper.RegisterEffectFromRegistrationData((ID2D1Factory1*)d2D1Factory2.Get(), D2D1EffectRegistrationData.V1.Load(blob));

        using ComPtr<ID2D1Effect> d2D1Effect = default;

        d2D1DeviceContext.Get()->CreateEffect(&effectId, d2D1Effect.GetAddressOf()).Assert();

        AssertEveryIndexReachesItsOwnResourceTextureManager(
            d2D1Effect.Get(),
            D2D1PixelShader.GetResourceTextureCount<ShaderWithMaximumResourceTextures>());
    }

    // A property bound to another slot leaves one slot untouched, so distinct managers tell them apart
    private static unsafe void AssertEveryIndexReachesItsOwnResourceTextureManager(ID2D1Effect* d2D1Effect, int resourceTextureCount)
    {
        IUnknown** resourceTextureManagers = stackalloc IUnknown*[resourceTextureCount];

        try
        {
            for (int i = 0; i < resourceTextureCount; i++)
            {
                D2D1ResourceTextureManager.Create((void**)&resourceTextureManagers[i]);

                D2D1PixelShaderEffect.SetResourceTextureManagerForD2D1Effect(d2D1Effect, resourceTextureManagers[i], i);
            }

            for (int i = 0; i < resourceTextureCount; i++)
            {
                IUnknown* value = null;

                d2D1Effect->GetValue(
                    D2D1PixelShaderEffectProperty.ResourceTextureManager0 + (uint)i,
                    (byte*)&value,
                    (uint)sizeof(void*)).Assert();

                using ComPtr<IUnknown> retrieved = default;

                retrieved.Attach(value);

                Assert.IsTrue(retrieved.Get() == resourceTextureManagers[i]);
            }
        }
        finally
        {
            for (int i = 0; i < resourceTextureCount; i++)
            {
                if (resourceTextureManagers[i] is not null)
                {
                    _ = resourceTextureManagers[i]->Release();
                }
            }
        }
    }

    [TestMethod]
    public unsafe void GetValue_ConstantBuffer_RoundTrips()
    {
        using ComPtr<ID2D1Factory2> d2D1Factory2 = D2D1Helper.CreateD2D1Factory2();
        using ComPtr<ID2D1Device> d2D1Device = D2D1Helper.CreateD2D1Device(d2D1Factory2.Get());
        using ComPtr<ID2D1DeviceContext> d2D1DeviceContext = D2D1Helper.CreateD2D1DeviceContext(d2D1Device.Get());

        D2D1PixelShaderEffect.RegisterForD2D1Factory1<ConstantBufferSizeTestShader>(d2D1Factory2.Get(), out _);

        using ComPtr<ID2D1Effect> d2D1Effect = default;

        D2D1PixelShaderEffect.CreateFromD2D1DeviceContext<ConstantBufferSizeTestShader>(d2D1DeviceContext.Get(), (void**)d2D1Effect.GetAddressOf());

        ConstantBufferSizeTestShader shader = new(1, 2, new float3(3, 4, 5), 6, 7);

        D2D1PixelShaderEffect.SetConstantBufferForD2D1Effect(d2D1Effect.Get(), in shader);

        byte[] expected = D2D1PixelShader.GetConstantBuffer(in shader).ToArray();
        byte[] actual = new byte[expected.Length];

        fixed (byte* p = actual)
        {
            d2D1Effect.Get()->GetValue(D2D1PixelShaderEffectProperty.ConstantBuffer, p, (uint)actual.Length).Assert();
        }

        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public unsafe void DefaultEffectId_MatchesValue()
    {
        using ComPtr<ID2D1Factory2> d2D1Factory2 = D2D1Helper.CreateD2D1Factory2();
        using ComPtr<ID2D1Device> d2D1Device = D2D1Helper.CreateD2D1Device(d2D1Factory2.Get());
        using ComPtr<ID2D1DeviceContext> d2D1DeviceContext = D2D1Helper.CreateD2D1DeviceContext(d2D1Device.Get());

        D2D1PixelShaderEffect.RegisterForD2D1Factory1<ShaderWithDefaultEffectId>(d2D1Factory2.Get(), out Guid effectId);
        D2D1PixelShaderEffect.RegisterForD2D1Factory1<ShaderWithDefaultEffectId2>(d2D1Factory2.Get(), out Guid effectId2);

        // Ensure that the dynamically generated GUIDs are deterministic and stable
        Assert.AreEqual(Guid.Parse("AAFA580E-9A4A-1B2F-5F91-44E0C281CEED"), effectId);
        Assert.AreEqual(Guid.Parse("4565397D-4EF5-083E-9593-4CBE3634E5E6"), effectId2);

        Assert.AreEqual(D2D1PixelShaderEffect.GetEffectId<ShaderWithDefaultEffectId>(), effectId);
        Assert.AreEqual(D2D1PixelShaderEffect.GetEffectId<ShaderWithDefaultEffectId2>(), effectId2);
    }

    [D2DInputCount(0)]
    [D2DGeneratedPixelShaderDescriptor]
    internal partial struct ShaderWithDefaultEffectId : ID2D1PixelShader
    {
        public float4 Execute()
        {
            return 0;
        }
    }

    [D2DInputCount(0)]
    [D2DGeneratedPixelShaderDescriptor]
    internal partial struct ShaderWithDefaultEffectId2 : ID2D1PixelShader
    {
        public float4 Execute()
        {
            return 0;
        }
    }

    [TestMethod]
    public unsafe void ExplicitEffectId_MatchesValue()
    {
        using ComPtr<ID2D1Factory2> d2D1Factory2 = D2D1Helper.CreateD2D1Factory2();
        using ComPtr<ID2D1Device> d2D1Device = D2D1Helper.CreateD2D1Device(d2D1Factory2.Get());
        using ComPtr<ID2D1DeviceContext> d2D1DeviceContext = D2D1Helper.CreateD2D1DeviceContext(d2D1Device.Get());

        D2D1PixelShaderEffect.RegisterForD2D1Factory1<ShaderWithExplicitEffectId>(d2D1Factory2.Get(), out Guid effectId);

        Assert.AreEqual(Guid.Parse("8E1F7F49-EF0D-4242-8912-08ADA36AB4EC"), effectId);
        Assert.AreEqual(D2D1PixelShaderEffect.GetEffectId<ShaderWithExplicitEffectId>(), effectId);
    }

    [D2DInputCount(0)]
    [D2DEffectId("8E1F7F49-EF0D-4242-8912-08ADA36AB4EC")]
    [D2DGeneratedPixelShaderDescriptor]
    internal partial struct ShaderWithExplicitEffectId : ID2D1PixelShader
    {
        public float4 Execute()
        {
            return 0;
        }
    }

    [TestMethod]
    public unsafe void DefaultEffectMetadata_MatchesValue()
    {
        Assert.AreEqual(D2D1PixelShaderEffect.GetEffectDisplayName<ShaderWithDefaultEffectDisplayName>(), null);
        Assert.AreEqual(D2D1PixelShaderEffect.GetEffectDisplayName<ShaderWithDefaultEffectDisplayName>(), null);
        Assert.AreEqual(D2D1PixelShaderEffect.GetEffectDisplayName<ShaderWithDefaultEffectDisplayName>(), null);
        Assert.AreEqual(D2D1PixelShaderEffect.GetEffectDisplayName<ShaderWithDefaultEffectDisplayName>(), null);
    }

    [D2DInputCount(0)]
    [D2DGeneratedPixelShaderDescriptor]
    internal partial struct ShaderWithDefaultEffectDisplayName : ID2D1PixelShader
    {
        public float4 Execute()
        {
            return 0;
        }
    }

    [TestMethod]
    public unsafe void ExplicitEffectMetadata1_MatchesValue()
    {
        Assert.AreEqual(D2D1PixelShaderEffect.GetEffectDisplayName<ShaderWithExplicitEffectDisplayName1>(), "Fancy blur");
        Assert.AreEqual(D2D1PixelShaderEffect.GetEffectDescription<ShaderWithExplicitEffectDisplayName1>(), null);
        Assert.AreEqual(D2D1PixelShaderEffect.GetEffectCategory<ShaderWithExplicitEffectDisplayName1>(), null);
        Assert.AreEqual(D2D1PixelShaderEffect.GetEffectAuthor<ShaderWithExplicitEffectDisplayName1>(), null);
    }

    [D2DInputCount(0)]
    [D2DEffectDisplayName("Fancy blur")]
    [D2DGeneratedPixelShaderDescriptor]
    internal partial struct ShaderWithExplicitEffectDisplayName1 : ID2D1PixelShader
    {
        public float4 Execute()
        {
            return 0;
        }
    }

    [TestMethod]
    public unsafe void ExplicitEffectMetadata2_MatchesValue()
    {
        Assert.AreEqual(D2D1PixelShaderEffect.GetEffectDisplayName<ShaderWithExplicitEffectDisplayName2>(), "Fancy&quot;&lt;");
        Assert.AreEqual(D2D1PixelShaderEffect.GetEffectDescription<ShaderWithExplicitEffectDisplayName2>(), "A test effect with some custom metadata");
        Assert.AreEqual(D2D1PixelShaderEffect.GetEffectCategory<ShaderWithExplicitEffectDisplayName2>(), "Test effects!");
        Assert.AreEqual(D2D1PixelShaderEffect.GetEffectAuthor<ShaderWithExplicitEffectDisplayName2>(), "Bob Ross");
    }

    [D2DInputCount(0)]
    [D2DEffectDisplayName("F\r\na\nncy\"<")]
    [D2DEffectDescription("A test effect with \nsome custom metadata")]
    [D2DEffectCategory("Test effects!")]
    [D2DEffectAuthor("Bob \r\nRoss")]
    [D2DGeneratedPixelShaderDescriptor]
    internal partial struct ShaderWithExplicitEffectDisplayName2 : ID2D1PixelShader
    {
        public float4 Execute()
        {
            return 0;
        }
    }
}