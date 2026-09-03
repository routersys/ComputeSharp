using System;
using ComputeWeave;
using ComputeWeave.Descriptors;
using ComputeWeave.Interop;
using ComputeWeave.Tests.Misc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable IDE0008, IDE0022, IDE0009, IDE0060, IDE0290

namespace ComputeWeave.Tests
{
    [TestClass]
    public partial class ShaderCompilerTests
    {
        [TestMethod]
        public void ReflectionBytecode()
        {
            static ReadOnlyMemory<byte> GetHlslBytecode<T>()
            where T : struct, IComputeShaderDescriptor<T>
            {
                return T.HlslBytecode;
            }

            ShaderInfo shaderInfo = ReflectionServices.GetShaderInfo<ReservedKeywordsShader>();

            CollectionAssert.AreEqual(GetHlslBytecode<ReservedKeywordsShader>().ToArray(), shaderInfo.HlslBytecode.ToArray());
        }

        [TestMethod]
        public void ReservedKeywords()
        {
            _ = ReflectionServices.GetShaderInfo<ReservedKeywordsShader>();
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        public readonly partial struct ReservedKeywordsShader : IComputeShader
        {
            public readonly ReadWriteBuffer<float> row_major;
            public readonly float dword;
            public readonly float float2;
            public readonly int int2x2;

            public void Execute()
            {
                float exp = Hlsl.Exp(dword * row_major[ThreadIds.X]);
                float log = Hlsl.Log(1 + exp);

                row_major[ThreadIds.X] = (log / dword) + float2 + int2x2;
            }
        }

        [TestMethod]
        public void ReservedKeywordsInCustomTypes()
        {
            _ = ReflectionServices.GetShaderInfo<ReservedKeywordsInCustomTypesShader>();
        }

        public struct CellData
        {
            public float testX;
            public float testY;
            public uint seed;

            public float distance;
            public readonly float dword;
            public readonly float float2;
            public readonly int int2x2;
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        public readonly partial struct ReservedKeywordsInCustomTypesShader : IComputeShader
        {
            public readonly ReadWriteBuffer<float> row_major;
            public readonly CellData cellData;
            public readonly float dword;
            public readonly float float2;
            public readonly int int2x2;
            public readonly CellData cbuffer;

            public void Execute()
            {
                float exp = Hlsl.Exp(cellData.distance * row_major[ThreadIds.X]);
                float log = Hlsl.Log(1 + exp);
                float temp = log + cellData.dword + cellData.int2x2;

                row_major[ThreadIds.X] = (log / dword) + float2 + int2x2 + cbuffer.float2 + temp;
            }
        }

        // See https://github.com/Sergio0694/ComputeSharp/issues/313
        [TestMethod]
        public void ReservedKeywordsFromHlslTypesAndBuiltInValues()
        {
            _ = ReflectionServices.GetShaderInfo<ReservedKeywordsFromHlslTypesAndBuiltInValuesShader>();
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        public readonly partial struct ReservedKeywordsFromHlslTypesAndBuiltInValuesShader : IComputeShader
        {
            public readonly ReadWriteBuffer<float> fragmentKeyword;
            public readonly ReadWriteBuffer<float> compile_fragment;
            public readonly ReadWriteBuffer<float> shaderProfile;
            public readonly ReadWriteBuffer<float> maxvertexcount;
            public readonly ReadWriteBuffer<float> TriangleStream;
            public readonly ReadWriteBuffer<float> Buffer;
            public readonly ReadWriteBuffer<float> ByteAddressBuffer;
            public readonly int ConsumeStructuredBuffer;
            public readonly int RWTexture2D;
            public readonly int Texture2D;
            public readonly int Texture2DArray;
            public readonly int SV_DomainLocation;
            public readonly int SV_GroupIndex;
            public readonly int SV_OutputControlPointID;
            public readonly int SV_StencilRef;

            public void Execute()
            {
                float sum = ConsumeStructuredBuffer + RWTexture2D + Texture2D + Texture2DArray;

                sum += SV_DomainLocation + SV_GroupIndex + SV_OutputControlPointID + SV_StencilRef;

                fragmentKeyword[ThreadIds.X] = sum;
                compile_fragment[ThreadIds.X] = sum;
                shaderProfile[ThreadIds.X] = sum;
                maxvertexcount[ThreadIds.X] = sum;
                TriangleStream[ThreadIds.X] = sum;
                Buffer[ThreadIds.X] = sum;
                ByteAddressBuffer[ThreadIds.X] = sum;
            }
        }

        [TestMethod]
        public void ReservedKeywordsPrecompiled()
        {
            _ = ReflectionServices.GetShaderInfo<ReservedKeywordsPrecompiledShader>();
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        public readonly partial struct ReservedKeywordsPrecompiledShader : IComputeShader
        {
            public readonly ReadWriteBuffer<float> row_major;
            public readonly float dword;
            public readonly float float2;
            public readonly int int2x2;
            private readonly float sin;
            private readonly float cos;
            private readonly float scale;
            private readonly float intensity;

            public void Execute()
            {
                float exp = Hlsl.Exp(dword * row_major[ThreadIds.X]);
                float log = Hlsl.Log(1 + exp);

                float s1 = this.cos * exp * this.sin * log;
                float t1 = -this.sin * exp * this.cos * log;

                float s2 = s1 + this.intensity + Hlsl.Tan(s1 * this.scale);
                float t2 = t1 + this.intensity + Hlsl.Tan(t1 * this.scale);

                float u2 = (this.cos * s2) - (this.sin * t2);
                float v2 = (this.sin * s2) - (this.cos * t2);

                row_major[ThreadIds.X] = (log / dword) + float2 + int2x2 + u2 + v2;
            }
        }

        [TestMethod]
        public void SpecialTypeAsReturnType()
        {
            _ = ReflectionServices.GetShaderInfo<SpecialTypeAsReturnTypeShader>();
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        public readonly partial struct SpecialTypeAsReturnTypeShader : IComputeShader
        {
            public readonly ReadWriteBuffer<float2> buffer;

            float2 Foo(float x) => x;

            public void Execute()
            {
                static float3 Bar(float x) => x;

                buffer[ThreadIds.X] = Foo(ThreadIds.X) + Bar(ThreadIds.X).XY;
            }
        }

        [TestMethod]
        public void LocalFunctionInExternalMethods()
        {
            _ = ReflectionServices.GetShaderInfo<LocalFunctionInExternalMethodsShader>();
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        public readonly partial struct LocalFunctionInExternalMethodsShader : IComputeShader
        {
            public readonly ReadWriteBuffer<float2> buffer;

            float2 Foo(float x)
            {
                static float2 Baz(float y) => y;

                return Baz(x);
            }

            public void Execute()
            {
                buffer[ThreadIds.X] = Foo(ThreadIds.X);
            }
        }

        [TestMethod]
        public void CapturedNestedStructType()
        {
            _ = ReflectionServices.GetShaderInfo<CapturedNestedStructTypeShader>();
        }

        [AutoConstructor]
        public readonly partial struct CustomStructType
        {
            public readonly float2 a;
            public readonly int b;
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        public readonly partial struct CapturedNestedStructTypeShader : IComputeShader
        {
            public readonly ReadWriteBuffer<float> buffer;
            public readonly CustomStructType foo;

            /// <inheritdoc/>
            public void Execute()
            {
                buffer[ThreadIds.X] *= foo.a.X + foo.b;
            }
        }

        [TestMethod]
        public void ExternalStructType_Ok()
        {
            _ = ReflectionServices.GetShaderInfo<ExternalStructTypeShader>();
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        public readonly partial struct ExternalStructTypeShader : IComputeShader
        {
            public readonly ReadWriteBuffer<float> buffer;

            /// <inheritdoc/>
            public void Execute()
            {
                float value = buffer[ThreadIds.X];
                ExternalStructType type = ExternalStructType.New((int)value, Hlsl.Abs(value));

                buffer[ThreadIds.X] = ExternalStructType.Sum(type);
            }
        }

        [TestMethod]
        public void OutOfOrderMethods()
        {
            _ = ReflectionServices.GetShaderInfo<OutOfOrderMethodsShader>();
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        public readonly partial struct OutOfOrderMethodsShader : IComputeShader
        {
            public readonly ReadWriteBuffer<float> buffer;

            static int A() => B();

            static int B() => 1 + C();

            static int C() => 1;

            public int D() => A() + E() + F();

            int E() => 1;

            static int F() => 1;

            /// <inheritdoc/>
            public void Execute()
            {
                float value = buffer[ThreadIds.X];
                ExternalStructType type = ExternalStructType.New((int)value, Hlsl.Abs(value));

                buffer[ThreadIds.X] = ExternalStructType.Sum(type);
            }
        }

        [TestMethod]
        public void PixelShader()
        {
            ShaderInfo info = ReflectionServices.GetShaderInfo<StatelessPixelShader, float4>();

            Assert.AreEqual(info.TextureStoreInstructionCount, 1u);
            Assert.AreEqual(info.BoundResourceCount, 2u);
            Assert.AreEqual("""
                #define __GroupSize__get_X 8
                #define __GroupSize__get_Y 8
                #define __GroupSize__get_Z 1
                
                cbuffer _ : register(b0)
                {
                    int __x;
                    int __y;
                }
                
                RWTexture2D<unorm float4> __outputTexture : register(u0);
                
                [NumThreads(__GroupSize__get_X, __GroupSize__get_Y, __GroupSize__get_Z)]
                void Execute(int3 ThreadIds : SV_DispatchThreadID)
                {
                    if (ThreadIds.x < __x && ThreadIds.y < __y)
                    {
                        {
                            __outputTexture[ThreadIds.xy] = float4(1, 1, 1, 1);
                            return;
                        }
                    }
                }
                """, info.HlslSource);
        }

        [TestMethod]
        public void BooleanConstant()
        {
            ShaderInfo info = ReflectionServices.GetShaderInfo<BooleanConstantShader>();

            Assert.AreEqual("""
                #define __GroupSize__get_X 64
                #define __GroupSize__get_Y 1
                #define __GroupSize__get_Z 1
                #define __ComputeWeave_Tests_ShaderCompilerTests_BooleanConstantShader__Flag true
                
                cbuffer _ : register(b0)
                {
                    int __x;
                    int __y;
                    int __z;
                }
                
                RWStructuredBuffer<float> __reserved__buffer : register(u0);
                
                [NumThreads(__GroupSize__get_X, __GroupSize__get_Y, __GroupSize__get_Z)]
                void Execute(int3 ThreadIds : SV_DispatchThreadID)
                {
                    if (ThreadIds.x < __x && ThreadIds.y < __y && ThreadIds.z < __z)
                    {
                        if (__ComputeWeave_Tests_ShaderCompilerTests_BooleanConstantShader__Flag)
                        {
                            __reserved__buffer[ThreadIds.x] = 1;
                        }
                    }
                }
                """, info.HlslSource);
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct BooleanConstantShader : IComputeShader
        {
            private const bool Flag = true;

            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                if (Flag)
                {
                    this.buffer[ThreadIds.X] = 1;
                }
            }
        }

        [ThreadGroupSize(DefaultThreadGroupSizes.XY)]
        [GeneratedComputeShaderDescriptor]
        public readonly partial struct StatelessPixelShader : IComputeShader<float4>
        {
            /// <inheritdoc/>
            public float4 Execute()
            {
                return new(1, 1, 1, 1);
            }
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        public readonly partial struct LoopWithVarCounterShader : IComputeShader
        {
            public readonly ReadWriteBuffer<float> buffer;

            /// <inheritdoc/>
            public void Execute()
            {
                for (var i = 0; i < 10; i++)
                {
                    buffer[(ThreadIds.X * 10) + i] = i;
                }
            }
        }

        [TestMethod]
        public void LoopWithVarCounter()
        {
            _ = ReflectionServices.GetShaderInfo<LoopWithVarCounterShader>();
        }

        [TestMethod]
        public void DoublePrecisionSupport()
        {
            ShaderInfo info = ReflectionServices.GetShaderInfo<DoublePrecisionSupportShader>();

            Assert.IsTrue(info.RequiresDoublePrecisionSupport);
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [RequiresDoublePrecisionSupport]
        [GeneratedComputeShaderDescriptor]
        public readonly partial struct DoublePrecisionSupportShader : IComputeShader
        {
            public readonly ReadWriteBuffer<double> buffer;
            public readonly double factor;

            /// <inheritdoc/>
            public void Execute()
            {
                buffer[ThreadIds.X] *= factor + 3.14;
            }
        }

        [TestMethod]
        public void FieldAccessWithThisExpression()
        {
            _ = ReflectionServices.GetShaderInfo<FieldAccessWithThisExpressionShader>();
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct FieldAccessWithThisExpressionShader : IComputeShader
        {
            public readonly ReadWriteBuffer<float> buffer;
            public readonly float number;

            /// <inheritdoc/>
            public void Execute()
            {
                this.buffer[ThreadIds.X] *= this.number;
            }
        }

        [TestMethod]
        public void ComputeShaderWithInheritedShaderInterface()
        {
            _ = ReflectionServices.GetShaderInfo<ComputeShaderWithInheritedShaderInterfaceShader>();
        }

        public interface IMyBaseShader : IComputeShader
        {
            public int A { get; }

            public void B();
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct ComputeShaderWithInheritedShaderInterfaceShader : IMyBaseShader
        {
            int IMyBaseShader.A => 42;

            void IMyBaseShader.B()
            {
            }

            public readonly ReadWriteBuffer<float> buffer;
            public readonly float number;

            /// <inheritdoc/>
            public void Execute()
            {
                this.buffer[ThreadIds.X] *= this.number;
            }
        }

        [TestMethod]
        public void PixelShaderWithInheritedShaderInterface()
        {
            _ = ReflectionServices.GetShaderInfo<PixelShaderWithInheritedShaderInterfaceShader, float4>();
        }

        public interface IMyBaseShader<T> : IComputeShader<T>
            where T : unmanaged
        {
            public int A { get; }

            public void B();
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct PixelShaderWithInheritedShaderInterfaceShader : IMyBaseShader<float4>
        {
            int IMyBaseShader<float4>.A => 42;

            void IMyBaseShader<float4>.B()
            {
            }

            public readonly float number;

            /// <inheritdoc/>
            public float4 Execute()
            {
                return default;
            }
        }

        [TestMethod]
        public void StructInstanceMethods()
        {
            _ = ReflectionServices.GetShaderInfo<StructInstanceMethodsShader>();
        }

        public struct MyStructTypeA
        {
            public int A;
            public float B;

            public float Sum()
            {
                return A + Bar();
            }

            public float Bar() => this.B;
        }

        public struct MyStructTypeB
        {
            public MyStructTypeA A;
            public float B;

            public float Combine()
            {
                return A.Sum() + this.B;
            }
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct StructInstanceMethodsShader : IComputeShader
        {
            public readonly MyStructTypeA a;
            public readonly MyStructTypeB b;
            public readonly ReadWriteBuffer<MyStructTypeA> bufferA;
            public readonly ReadWriteBuffer<MyStructTypeB> bufferB;
            public readonly ReadWriteBuffer<float> results;

            /// <inheritdoc/>
            public void Execute()
            {
                float result1 = a.Sum() + a.Bar();
                float result2 = b.Combine();

                results[ThreadIds.X] = result1 + result2 + bufferA[ThreadIds.X].Sum() + bufferB[0].Combine();
            }
        }

        [TestMethod]
        public void ComputeShaderWithScopedParameterInMethods()
        {
            _ = ReflectionServices.GetShaderInfo<ComputeShaderWithScopedParameterInMethodsShader>();
        }

        internal static class HelpersForComputeShaderWithScopedParameterInMethods
        {
            public static void Baz(scoped in float a, scoped ref float b)
            {
                b = a;
            }
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct ComputeShaderWithScopedParameterInMethodsShader : IComputeShader
        {
            public readonly ReadWriteBuffer<float> buffer;
            public readonly float number;

            private static void Foo(scoped ref float a, ref float b)
            {
                b = a;
            }

            private void Bar(scoped ref float a, scoped ref float b)
            {
                b = a;
            }

            /// <inheritdoc/>
            public void Execute()
            {
                float x = this.number + ThreadIds.X;

                Foo(ref this.buffer[ThreadIds.X], ref x);
                Bar(ref this.buffer[ThreadIds.X], ref x);
                HelpersForComputeShaderWithScopedParameterInMethods.Baz(in this.buffer[ThreadIds.X], ref x);

                this.buffer[ThreadIds.X] *= x;
            }
        }

        [TestMethod]
        public void ShaderWithStrippedReflectionData()
        {
            ShaderInfo info1 = ReflectionServices.GetShaderInfo<ShaderWithStrippedReflectionData1>();

            // With no reflection data available, the instruction count is just 0
            Assert.AreEqual(0u, info1.InstructionCount);

            ShaderInfo info2 = ReflectionServices.GetShaderInfo<ShaderWithStrippedReflectionData2>();

            // Sanity check, here instead we should have some valid count
            Assert.AreNotEqual(0u, info2.InstructionCount);

            // Verify that the bytecode with stripped reflection is much smaller
            Assert.IsTrue(info1.HlslBytecode.Length < 1800);
            Assert.IsTrue(info2.HlslBytecode.Length > 3300);
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [CompileOptions(CompileOptions.Default | CompileOptions.StripReflectionData)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct ShaderWithStrippedReflectionData1 : IComputeShader
        {
            public readonly ReadWriteBuffer<float> buffer;

            /// <inheritdoc/>
            public void Execute()
            {
                this.buffer[ThreadIds.X] = ThreadIds.X;
            }
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [CompileOptions(CompileOptions.Default)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct ShaderWithStrippedReflectionData2 : IComputeShader
        {
            public readonly ReadWriteBuffer<float> buffer;

            /// <inheritdoc/>
            public void Execute()
            {
                this.buffer[ThreadIds.X] = ThreadIds.X;
            }
        }

        [TestMethod]
        public void GloballyCoherentBuffers()
        {
            ShaderInfo info = ReflectionServices.GetShaderInfo<GloballyCoherentBufferShader>();

            Assert.AreEqual(
                """
                #define __GroupSize__get_X 64
                #define __GroupSize__get_Y 1
                #define __GroupSize__get_Z 1

                cbuffer _ : register(b0)
                {
                    int __x;
                    int __y;
                    int __z;
                }

                globallycoherent RWStructuredBuffer<int> __reserved__buffer : register(u0);

                [NumThreads(__GroupSize__get_X, __GroupSize__get_Y, __GroupSize__get_Z)]
                void Execute(int3 ThreadIds : SV_DispatchThreadID)
                {
                    if (ThreadIds.x < __x && ThreadIds.y < __y && ThreadIds.z < __z)
                    {
                        InterlockedAdd(__reserved__buffer[0], 1);
                    }
                }
                """,
                info.HlslSource);
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct GloballyCoherentBufferShader : IComputeShader
        {
            [GloballyCoherent]
            private readonly ReadWriteBuffer<int> buffer;

            public void Execute()
            {
                Hlsl.InterlockedAdd(ref this.buffer[0], 1);
            }
        }

        [TestMethod]
        public void ComputeShaderWithRefReadonlyParameterInMethods()
        {
            _ = ReflectionServices.GetShaderInfo<ComputeShaderWithRefReadonlyParameterInMethodsShader>();
        }

        internal static class HelpersForCommputeShaderWithRefReadonlyParameterInMethods
        {
            public static float Baz(ref readonly float a, scoped ref readonly float b)
            {
                return a + b;
            }
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct ComputeShaderWithRefReadonlyParameterInMethodsShader : IComputeShader
        {
            public readonly ReadWriteBuffer<float> buffer;
            public readonly float number;

            private static float Foo(ref readonly float a, scoped ref readonly float b)
            {
                return a + b;
            }

            private float Bar(ref readonly float a, scoped ref readonly float b)
            {
                return a + b;
            }

            /// <inheritdoc/>
            public void Execute()
            {
                float x = this.number + ThreadIds.X;

                x += Foo(ref x, in x);
                x += Foo(in this.number, in this.number);
                x += Bar(ref x, in x);
                x += HelpersForCommputeShaderWithRefReadonlyParameterInMethods.Baz(in this.buffer[ThreadIds.X], ref x);

                this.buffer[ThreadIds.X] = x;
            }
        }

        [TestMethod]
        public void AllRefTypesShader_RewritesRefParametersCorrectly()
        {
            ShaderInfo info = ReflectionServices.GetShaderInfo<AllRefTypesShader>();

            Assert.AreEqual(
                """
                #define __GroupSize__get_X 64
                #define __GroupSize__get_Y 1
                #define __GroupSize__get_Z 1

                static void Foo(in int a, in int b, inout int c, out int d);

                static void Bar(in int a, in int b, inout int c, out int d);

                cbuffer _ : register(b0)
                {
                    int __x;
                    int __y;
                    int __z;
                }

                RWStructuredBuffer<float> __reserved__buffer : register(u0);

                static void Foo(in int a, in int b, inout int c, out int d)
                {
                    d = 0;
                }

                static void Bar(in int a, in int b, inout int c, out int d)
                {
                    d = 0;
                }

                [NumThreads(__GroupSize__get_X, __GroupSize__get_Y, __GroupSize__get_Z)]
                void Execute(int3 ThreadIds : SV_DispatchThreadID)
                {
                    if (ThreadIds.x < __x && ThreadIds.y < __y && ThreadIds.z < __z)
                    {
                    }
                }
                """,
                info.HlslSource);
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct AllRefTypesShader : IComputeShader
        {
            public readonly ReadWriteBuffer<float> buffer;

            public static void Foo(
                in int a,
                ref readonly int b,
                ref int c,
                out int d)
            {
                d = 0;
            }

            public static void Bar(
                scoped in int a,
                scoped ref readonly int b,
                scoped ref int c,
                scoped out int d)
            {
                d = 0;
            }

            /// <inheritdoc/>
            public void Execute()
            {
            }
        }

        [TestMethod]
        public void ShaderWithPartialDeclarations_IsCombinedCorrectly()
        {
            ShaderInfo info = ReflectionServices.GetShaderInfo<ShaderWithPartialDeclarations>();

            Assert.AreEqual(
                """
                #define __GroupSize__get_X 64
                #define __GroupSize__get_Y 1
                #define __GroupSize__get_Z 1
                #define __ComputeWeave_Tests_ShaderCompilerTests_ShaderWithPartialDeclarations__a 2

                static int Sum(int x, int y);

                static const int b = 4;

                cbuffer _ : register(b0)
                {
                    int __x;
                    int __y;
                    int __z;
                }

                RWStructuredBuffer<float> __reserved__buffer : register(u0);

                static int Sum(int x, int y)
                {
                    return x + y;
                }

                [NumThreads(__GroupSize__get_X, __GroupSize__get_Y, __GroupSize__get_Z)]
                void Execute(int3 ThreadIds : SV_DispatchThreadID)
                {
                    if (ThreadIds.x < __x && ThreadIds.y < __y && ThreadIds.z < __z)
                    {
                        __reserved__buffer[ThreadIds.x] = Sum(__ComputeWeave_Tests_ShaderCompilerTests_ShaderWithPartialDeclarations__a, b);
                    }
                }
                """,
                info.HlslSource);
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct ShaderWithPartialDeclarations : IComputeShader;

        partial struct ShaderWithPartialDeclarations
        {
            public readonly ReadWriteBuffer<float> buffer;
        }

        partial struct ShaderWithPartialDeclarations
        {
            /// <inheritdoc/>
            public void Execute()
            {
                buffer[ThreadIds.X] = Sum(a, b);
            }
        }

        partial struct ShaderWithPartialDeclarations
        {
            private const int a = 2;
        }

        partial struct ShaderWithPartialDeclarations
        {
            private static readonly int b = 4;
        }

        partial struct ShaderWithPartialDeclarations
        {
            private static int Sum(int x, int y)
            {
                return x + y;
            }
        }

        [TestMethod]
        public void ShaderWithStructMethodCallingOtherStructMethod_IsProcessedCorrectly()
        {
            ShaderInfo info = ReflectionServices.GetShaderInfo<ShaderWithStructMethodCallingOtherStructMethod>();

            Assert.AreEqual(
                """
                #define __GroupSize__get_X 64
                #define __GroupSize__get_Y 1
                #define __GroupSize__get_Z 1

                struct ComputeWeave_Tests_ShaderCompilerTests_StructWithInstanceMethod1;
                struct ComputeWeave_Tests_ShaderCompilerTests_StructWithInstanceMethod2;

                struct ComputeWeave_Tests_ShaderCompilerTests_StructWithInstanceMethod1
                {
                    int InstanceMethod();
                };

                struct ComputeWeave_Tests_ShaderCompilerTests_StructWithInstanceMethod2
                {
                    int InstanceMethod();
                };

                cbuffer _ : register(b0)
                {
                    int __x;
                    int __y;
                    int __z;
                }

                RWStructuredBuffer<int> __reserved__buffer : register(u0);

                int ComputeWeave_Tests_ShaderCompilerTests_StructWithInstanceMethod1::InstanceMethod()
                {
                    ComputeWeave_Tests_ShaderCompilerTests_StructWithInstanceMethod2 bar = (ComputeWeave_Tests_ShaderCompilerTests_StructWithInstanceMethod2)0;
                    return bar.InstanceMethod();
                }

                int ComputeWeave_Tests_ShaderCompilerTests_StructWithInstanceMethod2::InstanceMethod()
                {
                    return 42;
                }

                [NumThreads(__GroupSize__get_X, __GroupSize__get_Y, __GroupSize__get_Z)]
                void Execute(int3 ThreadIds : SV_DispatchThreadID)
                {
                    if (ThreadIds.x < __x && ThreadIds.y < __y && ThreadIds.z < __z)
                    {
                        ComputeWeave_Tests_ShaderCompilerTests_StructWithInstanceMethod1 foo = (ComputeWeave_Tests_ShaderCompilerTests_StructWithInstanceMethod1)0;
                        __reserved__buffer[ThreadIds.x] = foo.InstanceMethod();
                    }
                }
                """,
                info.HlslSource);
        }

        // See https://github.com/Sergio0694/ComputeSharp/issues/479
        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        public readonly partial struct ShaderWithStructMethodCallingOtherStructMethod : IComputeShader
        {
            private readonly ReadWriteBuffer<int> buffer;

            public void Execute()
            {
                StructWithInstanceMethod1 foo = default;

                buffer[ThreadIds.X] = foo.InstanceMethod();
            }
        }

        public struct StructWithInstanceMethod1
        {
            public int InstanceMethod()
            {
                StructWithInstanceMethod2 bar = default;

                return bar.InstanceMethod();
            }
        }

        public struct StructWithInstanceMethod2
        {
            public int InstanceMethod()
            {
                return 42;
            }
        }

        [TestMethod]
        public void ShaderWithAllSupportedMembers_IsProcessedCorrectly()
        {
            ShaderInfo info = ReflectionServices.GetShaderInfo<ShaderWithAllSupportedMembers>();

            Assert.AreEqual(
                """
                #define __GroupSize__get_X 64
                #define __GroupSize__get_Y 1
                #define __GroupSize__get_Z 1
                #define __ComputeWeave_Tests_ShaderCompilerTests_ExternalContainerClass__Factor 8
                #define __ComputeWeave_Tests_ShaderCompilerTests_ShaderWithAllSupportedMembers__PI 3.14

                struct ComputeWeave_Tests_ShaderCompilerTests_StructType1;
                struct ComputeWeave_Tests_ShaderCompilerTests_StructType2;

                struct ComputeWeave_Tests_ShaderCompilerTests_StructType1
                {
                    int X;
                    float Y;
                    float Combine(ComputeWeave_Tests_ShaderCompilerTests_StructType2 other);
                    static ComputeWeave_Tests_ShaderCompilerTests_StructType1 __ctor(int x);
                    void __ctor__init(int x);
                };

                struct ComputeWeave_Tests_ShaderCompilerTests_StructType2
                {
                    float2 V;
                    float Combine(ComputeWeave_Tests_ShaderCompilerTests_StructType1 other);
                };

                int InstanceMethodInShader();

                static float StaticMethodInShader(float x);

                static float ComputeWeave_Tests_ShaderCompilerTests_StructType1_StaticMethod(int x);

                static float ComputeWeave_Tests_ShaderCompilerTests_StructType2_StaticMethod(int x);

                static const float Init = abs(__ComputeWeave_Tests_ShaderCompilerTests_ShaderWithAllSupportedMembers__PI);
                static int Temp;
                static int ComputeWeave_Tests_ShaderCompilerTests_ExternalContainerClass_Temp;
                static const float ComputeWeave_Tests_ShaderCompilerTests_ExternalContainerClass_PI2 = 3.14 * 2;

                cbuffer _ : register(b0)
                {
                    int __x;
                    int __y;
                    int __z;
                    int number;
                    float4 __reserved__vector;
                }

                RWStructuredBuffer<int> __reserved__buffer : register(u0);

                float ComputeWeave_Tests_ShaderCompilerTests_StructType1::Combine(ComputeWeave_Tests_ShaderCompilerTests_StructType2 other)
                {
                    return Y + other.V.y;
                }

                static ComputeWeave_Tests_ShaderCompilerTests_StructType1 ComputeWeave_Tests_ShaderCompilerTests_StructType1::__ctor(int x)
                {
                    ComputeWeave_Tests_ShaderCompilerTests_StructType1 __this = (ComputeWeave_Tests_ShaderCompilerTests_StructType1)0;
                    __this.__ctor__init(x);
                    return __this;
                }

                void ComputeWeave_Tests_ShaderCompilerTests_StructType1::__ctor__init(int x)
                {
                    X = x;
                    Y = (float)0;
                }

                float ComputeWeave_Tests_ShaderCompilerTests_StructType2::Combine(ComputeWeave_Tests_ShaderCompilerTests_StructType1 other)
                {
                    return V.x + other.X;
                }

                int InstanceMethodInShader()
                {
                    return (int)(number + __reserved__vector.x);
                }

                static float StaticMethodInShader(float x)
                {
                    return x + 1;
                }

                static float ComputeWeave_Tests_ShaderCompilerTests_StructType1_StaticMethod(int x)
                {
                    return x * 2;
                }

                static float ComputeWeave_Tests_ShaderCompilerTests_StructType2_StaticMethod(int x)
                {
                    return x * 4;
                }

                [NumThreads(__GroupSize__get_X, __GroupSize__get_Y, __GroupSize__get_Z)]
                void Execute(int3 ThreadIds : SV_DispatchThreadID)
                {
                    if (ThreadIds.x < __x && ThreadIds.y < __y && ThreadIds.z < __z)
                    {
                        ComputeWeave_Tests_ShaderCompilerTests_StructType1 type1 = ComputeWeave_Tests_ShaderCompilerTests_StructType1::__ctor(__ComputeWeave_Tests_ShaderCompilerTests_ExternalContainerClass__Factor);
                        ComputeWeave_Tests_ShaderCompilerTests_StructType2 type2 = (ComputeWeave_Tests_ShaderCompilerTests_StructType2)0;
                        float combine1 = type1.Combine(type2);
                        float combine2 = type2.Combine(type1);
                        combine1 += ComputeWeave_Tests_ShaderCompilerTests_StructType1_StaticMethod(Temp++);
                        combine2 += ComputeWeave_Tests_ShaderCompilerTests_StructType2_StaticMethod(ComputeWeave_Tests_ShaderCompilerTests_ExternalContainerClass_Temp++);
                        float dummy = Init + ComputeWeave_Tests_ShaderCompilerTests_ExternalContainerClass_PI2;
                        __reserved__buffer[ThreadIds.x] = (int)(combine1 + combine2 + dummy);
                    }
                }
                """,
                info.HlslSource);
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct ShaderWithAllSupportedMembers : IComputeShader
        {
            private const float PI = 3.14f;

            private static readonly float Init = Hlsl.Abs(PI);
            private static int Temp;

            private readonly ReadWriteBuffer<int> buffer;
            private readonly int number;
            private readonly float4 vector;

            public void Execute()
            {
                StructType1 type1 = new(ExternalContainerClass.Factor);
                StructType2 type2 = default;

                float combine1 = type1.Combine(type2);
                float combine2 = type2.Combine(type1);

                combine1 += StructType1.StaticMethod(Temp++);
                combine2 += StructType2.StaticMethod(ExternalContainerClass.Temp++);

                float dummy = Init + ExternalContainerClass.PI2;

                buffer[ThreadIds.X] = (int)(combine1 + combine2 + dummy);
            }

            public int InstanceMethodInShader()
            {
                return (int)(number + vector.X);
            }

            public static float StaticMethodInShader(float x)
            {
                return x + 1;
            }
        }

        public static class ExternalContainerClass
        {
            public const int Factor = 8;

            public static readonly float PI2 = 3.14f * 2;
            public static int Temp;
        }

        internal struct StructType1
        {
            public int X;
            public float Y;

            public StructType1(int x)
            {
                X = x;
                Y = default;
            }

            public float Combine(StructType2 other)
            {
                return Y + other.V.Y;
            }

            public float InstanceMethod()
            {
                StructType2 other = default;
                other.V = ExternalContainerClass.PI2;

                return Y + other.Combine(default) + StructType2.StaticMethod(X);
            }

            public static float StaticMethod(int x)
            {
                return x * 2;
            }
        }

        internal struct StructType2
        {
            public float2 V;

            public float Combine(StructType1 other)
            {
                return V.X + other.X;
            }

            public float InstanceMethod()
            {
                StructType1 other = new(ExternalContainerClass.Temp);

                return V.X + other.Combine(default) + StructType1.StaticMethod((int)V.X);
            }

            public static float StaticMethod(int x)
            {
                return x * 4;
            }
        }

        // See https://github.com/Sergio0694/ComputeSharp/issues/726
        [TestMethod]
        public void ShaderUsingThisExpressions_IsProcessedCorrectly()
        {
            ShaderInfo info = ReflectionServices.GetShaderInfo<ShaderUsingThisExpressions>();

            Assert.AreEqual(
                """
                #define __GroupSize__get_X 64
                #define __GroupSize__get_Y 1
                #define __GroupSize__get_Z 1

                struct ComputeWeave_Tests_ShaderCompilerTests_ShaderUsingThisExpressions_Data;

                struct ComputeWeave_Tests_ShaderCompilerTests_ShaderUsingThisExpressions_Data
                {
                    int value;
                    void SetValue(int value);
                };

                cbuffer _ : register(b0)
                {
                    int __x;
                    int __y;
                    int __z;
                    float alpha;
                }

                RWStructuredBuffer<float4> __reserved__buffer : register(u0);

                void ComputeWeave_Tests_ShaderCompilerTests_ShaderUsingThisExpressions_Data::SetValue(int value)
                {
                    this.value = value;
                }

                [NumThreads(__GroupSize__get_X, __GroupSize__get_Y, __GroupSize__get_Z)]
                void Execute(int3 ThreadIds : SV_DispatchThreadID)
                {
                    if (ThreadIds.x < __x && ThreadIds.y < __y && ThreadIds.z < __z)
                    {
                        ComputeWeave_Tests_ShaderCompilerTests_ShaderUsingThisExpressions_Data data = (ComputeWeave_Tests_ShaderCompilerTests_ShaderUsingThisExpressions_Data)0;
                        data.SetValue(3);
                        __reserved__buffer[ThreadIds.x] = float4(data.value, data.value, data.value, alpha);
                    }
                }
                """,
                info.HlslSource);
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct ShaderUsingThisExpressions : IComputeShader
        {
            public readonly ReadWriteBuffer<float4> buffer;
            public readonly float alpha;

            private struct Data
            {
                public int value;

                public void SetValue(int value)
                {
                    this.value = value;
                }
            }

            public void Execute()
            {
                Data data = default;

                data.SetValue(3);

                // The 'this.' expression for shader captured values should be stripped.
                // The one for accessing struct instance members, however, should not be.
                this.buffer[ThreadIds.X] = new float4(data.value, data.value, data.value, this.alpha);
            }
        }

        // See https://github.com/Sergio0694/ComputeSharp/issues/435
        [TestMethod]
        public void HlslVectorTypeConstructorCombinations()
        {
            _ = ReflectionServices.GetShaderInfo<HlslVectorTypeConstructorCombinationsShader>();
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        public readonly partial struct HlslVectorTypeConstructorCombinationsShader : IComputeShader
        {
            public readonly ReadWriteBuffer<float> buffer;
            public readonly float2 f2;
            public readonly int4 i4;
            public readonly int2 i2;
            public readonly int3 i3;

            public void Execute()
            {
                float3 f3 = new float3(f2, 0) + new float3(0, f2);
                float4 f4 = new float4(0, f3) + new float4(0, f2, 1) + new float4(0, 1, f2) + new float4((float1x3)f3, 0);

                int4 temp = new int4(i2, 0, 1) + new int4(new int3x1(i3.X, i3.Y, i3.Z), 0);

                // Just here to avoid warnings, this shader doesn't really have to do anything
                buffer[0] = f3[0];
                buffer[1] = f4[1];
                buffer[2] = temp[0];
            }
        }
    }
}

namespace ExternalNamespace
{
    [TestClass]
    public partial class ShaderCompilerTestsInExternalNamespace
    {
        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        public readonly partial struct UserDefinedTypeShader : IComputeShader
        {
            public readonly ReadWriteBuffer<float> buffer;

            /// <inheritdoc/>
            public void Execute()
            {
                for (var i = 0; i < 10; i++)
                {
                    buffer[(ThreadIds.X * 10) + i] = i;
                }
            }
        }

        [TestMethod]
        public void UserDefinedType()
        {
            _ = ReflectionServices.GetShaderInfo<UserDefinedTypeShader>();
        }

        [TestMethod]
        public void ReservedKeywordsForResourceTypes()
        {
            _ = ReflectionServices.GetShaderInfo<ReservedKeywordsForResourceTypesShader>();
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        public readonly partial struct ReservedKeywordsForResourceTypesShader : IComputeShader
        {
            public readonly ReadWriteBuffer<float> buffer;
            public readonly float AppendStructuredBuffer;
            public readonly float LineStream;
            public readonly float PointStream;
            public readonly float min10float;
            public readonly float min12int;
            public readonly float min16float;
            public readonly float min16int;
            public readonly float min16uint;
            public readonly float TextureBuffer;
            public readonly float RayQuery;
            public readonly float SubpassInput;
            public readonly float SubpassInputMS;
            public readonly float FeedbackTexture2D;
            public readonly float FeedbackTexture2DArray;
            public readonly float RasterizerOrderedBuffer;
            public readonly float RasterizerOrderedStructuredBuffer;
            public readonly float RasterizerOrderedTexture1D;
            public readonly float RasterizerOrderedTexture2D;
            public readonly float RasterizerOrderedTexture2DArray;
            public readonly float RasterizerOrderedTexture3D;

            public void Execute()
            {
                float sum = 0;

                sum += AppendStructuredBuffer;
                sum += LineStream;
                sum += PointStream;
                sum += min10float;
                sum += min12int;
                sum += min16float;
                sum += min16int;
                sum += min16uint;
                sum += TextureBuffer;
                sum += RayQuery;
                sum += SubpassInput;
                sum += SubpassInputMS;
                sum += FeedbackTexture2D;
                sum += FeedbackTexture2DArray;
                sum += RasterizerOrderedBuffer;
                sum += RasterizerOrderedStructuredBuffer;
                sum += RasterizerOrderedTexture1D;
                sum += RasterizerOrderedTexture2D;
                sum += RasterizerOrderedTexture2DArray;
                sum += RasterizerOrderedTexture3D;

                buffer[ThreadIds.X] = sum;
            }
        }

        [TestMethod]
        public void ReservedKeywordsFromDxcSweep()
        {
            _ = ReflectionServices.GetShaderInfo<DxcReservedNamesShader0>();
            _ = ReflectionServices.GetShaderInfo<DxcReservedNamesShader1>();
            _ = ReflectionServices.GetShaderInfo<DxcReservedNamesShader2>();
            _ = ReflectionServices.GetShaderInfo<DxcReservedNamesShader3>();
            _ = ReflectionServices.GetShaderInfo<DxcReservedNamesShader4>();
            _ = ReflectionServices.GetShaderInfo<DxcReservedNamesShader5>();
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        public readonly partial struct DxcReservedNamesShader0 : IComputeShader
        {
            public readonly ReadWriteBuffer<float> buffer;
            public readonly float CANDIDATE_NON_OPAQUE_TRIANGLE;
            public readonly float CANDIDATE_PROCEDURAL_PRIMITIVE;
            public readonly float CANDIDATE_TYPE;
            public readonly float COMMITTED_NOTHING;
            public readonly float COMMITTED_PROCEDURAL_PRIMITIVE_HIT;
            public readonly float COMMITTED_STATUS;
            public readonly float COMMITTED_TRIANGLE_HIT;
            public readonly float HIT_KIND_NONE;
            public readonly float HIT_KIND_TRIANGLE_BACK_FACE;
            public readonly float HIT_KIND_TRIANGLE_FRONT_FACE;
            public readonly float RAYTRACING_PIPELINE_FLAG_NONE;
            public readonly float RAYTRACING_PIPELINE_FLAG_SKIP_TRIANGLES;
            public readonly float RAY_FLAG;
            public readonly float RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH;
            public readonly float RAY_FLAG_CULL_BACK_FACING_TRIANGLES;
            public readonly float RAY_FLAG_CULL_FRONT_FACING_TRIANGLES;
            public readonly float RAY_FLAG_CULL_NON_OPAQUE;
            public readonly float RAY_FLAG_CULL_OPAQUE;
            public readonly float RAY_FLAG_FORCE_NON_OPAQUE;
            public readonly float RAY_FLAG_FORCE_OPAQUE;
            public readonly float RAY_FLAG_NONE;
            public readonly float RAY_FLAG_SKIP_CLOSEST_HIT_SHADER;
            public readonly float RAY_FLAG_SKIP_PROCEDURAL_PRIMITIVES;
            public readonly float RAY_FLAG_SKIP_TRIANGLES;
            public readonly float RWTexture2DMS;
            public readonly float RWTexture2DMSArray;
            public readonly float RasterizerOrderedTexture1DArray;
            public readonly float SAMPLER_FEEDBACK_MIN_MIP;
            public readonly float SAMPLER_FEEDBACK_MIP_REGION_USED;
            public readonly float Technique;
            public readonly float _Alignas;
            public readonly float _Alignof;
            public readonly float _Atomic;
            public readonly float _Complex;
            public readonly float _Decimal128;
            public readonly float _Decimal32;
            public readonly float _Decimal64;
            public readonly float _Generic;
            public readonly float _Imaginary;
            public readonly float _Nonnull;
            public readonly float _Noreturn;
            public readonly float _Null_unspecified;
            public readonly float _Nullable;
            public readonly float _Pragma;
            public readonly float _Static_assert;
            public readonly float _Thread_local;
            public readonly float __BASE_FILE__;
            public readonly float __BYTE_ORDER__;

            public void Execute()
            {
                float sum = 0;

                sum += CANDIDATE_NON_OPAQUE_TRIANGLE;
                sum += CANDIDATE_PROCEDURAL_PRIMITIVE;
                sum += CANDIDATE_TYPE;
                sum += COMMITTED_NOTHING;
                sum += COMMITTED_PROCEDURAL_PRIMITIVE_HIT;
                sum += COMMITTED_STATUS;
                sum += COMMITTED_TRIANGLE_HIT;
                sum += HIT_KIND_NONE;
                sum += HIT_KIND_TRIANGLE_BACK_FACE;
                sum += HIT_KIND_TRIANGLE_FRONT_FACE;
                sum += RAYTRACING_PIPELINE_FLAG_NONE;
                sum += RAYTRACING_PIPELINE_FLAG_SKIP_TRIANGLES;
                sum += RAY_FLAG;
                sum += RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH;
                sum += RAY_FLAG_CULL_BACK_FACING_TRIANGLES;
                sum += RAY_FLAG_CULL_FRONT_FACING_TRIANGLES;
                sum += RAY_FLAG_CULL_NON_OPAQUE;
                sum += RAY_FLAG_CULL_OPAQUE;
                sum += RAY_FLAG_FORCE_NON_OPAQUE;
                sum += RAY_FLAG_FORCE_OPAQUE;
                sum += RAY_FLAG_NONE;
                sum += RAY_FLAG_SKIP_CLOSEST_HIT_SHADER;
                sum += RAY_FLAG_SKIP_PROCEDURAL_PRIMITIVES;
                sum += RAY_FLAG_SKIP_TRIANGLES;
                sum += RWTexture2DMS;
                sum += RWTexture2DMSArray;
                sum += RasterizerOrderedTexture1DArray;
                sum += SAMPLER_FEEDBACK_MIN_MIP;
                sum += SAMPLER_FEEDBACK_MIP_REGION_USED;
                sum += Technique;
                sum += _Alignas;
                sum += _Alignof;
                sum += _Atomic;
                sum += _Complex;
                sum += _Decimal128;
                sum += _Decimal32;
                sum += _Decimal64;
                sum += _Generic;
                sum += _Imaginary;
                sum += _Nonnull;
                sum += _Noreturn;
                sum += _Null_unspecified;
                sum += _Nullable;
                sum += _Pragma;
                sum += _Static_assert;
                sum += _Thread_local;
                sum += __BASE_FILE__;
                sum += __BYTE_ORDER__;

                buffer[ThreadIds.X] = sum;
            }
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        public readonly partial struct DxcReservedNamesShader1 : IComputeShader
        {
            public readonly ReadWriteBuffer<float> buffer;
            public readonly float __COUNTER__;
            public readonly float __DATE__;
            public readonly float __DXC_VERSION_COMMITS;
            public readonly float __DXC_VERSION_MAJOR;
            public readonly float __DXC_VERSION_MINOR;
            public readonly float __DXC_VERSION_RELEASE;
            public readonly float __FILE__;
            public readonly float __FLT_RADIX__;
            public readonly float __FUNCTION__;
            public readonly float __GNUC_MINOR__;
            public readonly float __GNUC_PATCHLEVEL__;
            public readonly float __GNUC__;
            public readonly float __GXX_ABI_VERSION;
            public readonly float __HLSL_VERSION;
            public readonly float __INCLUDE_LEVEL__;
            public readonly float __LINE__;
            public readonly float __LITTLE_ENDIAN__;
            public readonly float __ORDER_BIG_ENDIAN__;
            public readonly float __ORDER_LITTLE_ENDIAN__;
            public readonly float __ORDER_PDP_ENDIAN__;
            public readonly float __PRETTY_FUNCTION__;
            public readonly float __SHADER_STAGE_AMPLIFICATION;
            public readonly float __SHADER_STAGE_COMPUTE;
            public readonly float __SHADER_STAGE_DOMAIN;
            public readonly float __SHADER_STAGE_GEOMETRY;
            public readonly float __SHADER_STAGE_HULL;
            public readonly float __SHADER_STAGE_LIBRARY;
            public readonly float __SHADER_STAGE_MESH;
            public readonly float __SHADER_STAGE_PIXEL;
            public readonly float __SHADER_STAGE_VERTEX;
            public readonly float __SHADER_TARGET_MAJOR;
            public readonly float __SHADER_TARGET_MINOR;
            public readonly float __SHADER_TARGET_STAGE;
            public readonly float __TIME__;
            public readonly float __VERSION__;
            public readonly float __alignof;
            public readonly float __alignof__;
            public readonly float __array_extent;
            public readonly float __array_rank;
            public readonly float __asm;
            public readonly float __asm__;
            public readonly float __attribute;
            public readonly float __attribute__;
            public readonly float __builtin_choose_expr;
            public readonly float __builtin_convertvector;
            public readonly float __builtin_offsetof;
            public readonly float __builtin_omp_required_simd_align;
            public readonly float __builtin_va_arg;

            public void Execute()
            {
                float sum = 0;

                sum += __COUNTER__;
                sum += __DATE__;
                sum += __DXC_VERSION_COMMITS;
                sum += __DXC_VERSION_MAJOR;
                sum += __DXC_VERSION_MINOR;
                sum += __DXC_VERSION_RELEASE;
                sum += __FILE__;
                sum += __FLT_RADIX__;
                sum += __FUNCTION__;
                sum += __GNUC_MINOR__;
                sum += __GNUC_PATCHLEVEL__;
                sum += __GNUC__;
                sum += __GXX_ABI_VERSION;
                sum += __HLSL_VERSION;
                sum += __INCLUDE_LEVEL__;
                sum += __LINE__;
                sum += __LITTLE_ENDIAN__;
                sum += __ORDER_BIG_ENDIAN__;
                sum += __ORDER_LITTLE_ENDIAN__;
                sum += __ORDER_PDP_ENDIAN__;
                sum += __PRETTY_FUNCTION__;
                sum += __SHADER_STAGE_AMPLIFICATION;
                sum += __SHADER_STAGE_COMPUTE;
                sum += __SHADER_STAGE_DOMAIN;
                sum += __SHADER_STAGE_GEOMETRY;
                sum += __SHADER_STAGE_HULL;
                sum += __SHADER_STAGE_LIBRARY;
                sum += __SHADER_STAGE_MESH;
                sum += __SHADER_STAGE_PIXEL;
                sum += __SHADER_STAGE_VERTEX;
                sum += __SHADER_TARGET_MAJOR;
                sum += __SHADER_TARGET_MINOR;
                sum += __SHADER_TARGET_STAGE;
                sum += __TIME__;
                sum += __VERSION__;
                sum += __alignof;
                sum += __alignof__;
                sum += __array_extent;
                sum += __array_rank;
                sum += __asm;
                sum += __asm__;
                sum += __attribute;
                sum += __attribute__;
                sum += __builtin_choose_expr;
                sum += __builtin_convertvector;
                sum += __builtin_offsetof;
                sum += __builtin_omp_required_simd_align;
                sum += __builtin_va_arg;

                buffer[ThreadIds.X] = sum;
            }
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        public readonly partial struct DxcReservedNamesShader2 : IComputeShader
        {
            public readonly ReadWriteBuffer<float> buffer;
            public readonly float __builtin_va_list;
            public readonly float __cdecl;
            public readonly float __char16_t;
            public readonly float __char32_t;
            public readonly float __clang__;
            public readonly float __clang_major__;
            public readonly float __clang_minor__;
            public readonly float __clang_patchlevel__;
            public readonly float __clang_version__;
            public readonly float __complex;
            public readonly float __complex__;
            public readonly float __const;
            public readonly float __const__;
            public readonly float __declspec;
            public readonly float __decltype;
            public readonly float __extension__;
            public readonly float __fastcall;
            public readonly float __fp16;
            public readonly float __func__;
            public readonly float __has_attribute;
            public readonly float __has_builtin;
            public readonly float __has_cpp_attribute;
            public readonly float __has_declspec_attribute;
            public readonly float __has_extension;
            public readonly float __has_feature;
            public readonly float __has_include;
            public readonly float __has_include_next;
            public readonly float __has_nothrow_assign;
            public readonly float __has_nothrow_constructor;
            public readonly float __has_nothrow_copy;
            public readonly float __has_nothrow_move_assign;
            public readonly float __has_trivial_assign;
            public readonly float __has_trivial_constructor;
            public readonly float __has_trivial_copy;
            public readonly float __has_trivial_destructor;
            public readonly float __has_trivial_move_assign;
            public readonly float __has_trivial_move_constructor;
            public readonly float __has_virtual_destructor;
            public readonly float __has_warning;
            public readonly float __hlsl_dx_compiler;
            public readonly float __imag;
            public readonly float __imag__;
            public readonly float __inline;
            public readonly float __inline__;
            public readonly float __int128;
            public readonly float __is_abstract;
            public readonly float __is_arithmetic;
            public readonly float __is_array;

            public void Execute()
            {
                float sum = 0;

                sum += __builtin_va_list;
                sum += __cdecl;
                sum += __char16_t;
                sum += __char32_t;
                sum += __clang__;
                sum += __clang_major__;
                sum += __clang_minor__;
                sum += __clang_patchlevel__;
                sum += __clang_version__;
                sum += __complex;
                sum += __complex__;
                sum += __const;
                sum += __const__;
                sum += __declspec;
                sum += __decltype;
                sum += __extension__;
                sum += __fastcall;
                sum += __fp16;
                sum += __func__;
                sum += __has_attribute;
                sum += __has_builtin;
                sum += __has_cpp_attribute;
                sum += __has_declspec_attribute;
                sum += __has_extension;
                sum += __has_feature;
                sum += __has_include;
                sum += __has_include_next;
                sum += __has_nothrow_assign;
                sum += __has_nothrow_constructor;
                sum += __has_nothrow_copy;
                sum += __has_nothrow_move_assign;
                sum += __has_trivial_assign;
                sum += __has_trivial_constructor;
                sum += __has_trivial_copy;
                sum += __has_trivial_destructor;
                sum += __has_trivial_move_assign;
                sum += __has_trivial_move_constructor;
                sum += __has_virtual_destructor;
                sum += __has_warning;
                sum += __hlsl_dx_compiler;
                sum += __imag;
                sum += __imag__;
                sum += __inline;
                sum += __inline__;
                sum += __int128;
                sum += __is_abstract;
                sum += __is_arithmetic;
                sum += __is_array;

                buffer[ThreadIds.X] = sum;
            }
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        public readonly partial struct DxcReservedNamesShader3 : IComputeShader
        {
            public readonly ReadWriteBuffer<float> buffer;
            public readonly float __is_base_of;
            public readonly float __is_class;
            public readonly float __is_complete_type;
            public readonly float __is_compound;
            public readonly float __is_const;
            public readonly float __is_constructible;
            public readonly float __is_convertible;
            public readonly float __is_convertible_to;
            public readonly float __is_empty;
            public readonly float __is_enum;
            public readonly float __is_final;
            public readonly float __is_floating_point;
            public readonly float __is_function;
            public readonly float __is_fundamental;
            public readonly float __is_identifier;
            public readonly float __is_integral;
            public readonly float __is_literal;
            public readonly float __is_literal_type;
            public readonly float __is_lvalue_expr;
            public readonly float __is_lvalue_reference;
            public readonly float __is_member_function_pointer;
            public readonly float __is_member_object_pointer;
            public readonly float __is_member_pointer;
            public readonly float __is_nothrow_assignable;
            public readonly float __is_nothrow_constructible;
            public readonly float __is_object;
            public readonly float __is_pod;
            public readonly float __is_pointer;
            public readonly float __is_polymorphic;
            public readonly float __is_reference;
            public readonly float __is_rvalue_expr;
            public readonly float __is_rvalue_reference;
            public readonly float __is_same;
            public readonly float __is_scalar;
            public readonly float __is_signed;
            public readonly float __is_standard_layout;
            public readonly float __is_trivial;
            public readonly float __is_trivially_assignable;
            public readonly float __is_trivially_constructible;
            public readonly float __is_trivially_copyable;
            public readonly float __is_union;
            public readonly float __is_unsigned;
            public readonly float __is_void;
            public readonly float __is_volatile;
            public readonly float __label__;
            public readonly float __llvm__;
            public readonly float __module_private__;
            public readonly float __null;

            public void Execute()
            {
                float sum = 0;

                sum += __is_base_of;
                sum += __is_class;
                sum += __is_complete_type;
                sum += __is_compound;
                sum += __is_const;
                sum += __is_constructible;
                sum += __is_convertible;
                sum += __is_convertible_to;
                sum += __is_empty;
                sum += __is_enum;
                sum += __is_final;
                sum += __is_floating_point;
                sum += __is_function;
                sum += __is_fundamental;
                sum += __is_identifier;
                sum += __is_integral;
                sum += __is_literal;
                sum += __is_literal_type;
                sum += __is_lvalue_expr;
                sum += __is_lvalue_reference;
                sum += __is_member_function_pointer;
                sum += __is_member_object_pointer;
                sum += __is_member_pointer;
                sum += __is_nothrow_assignable;
                sum += __is_nothrow_constructible;
                sum += __is_object;
                sum += __is_pod;
                sum += __is_pointer;
                sum += __is_polymorphic;
                sum += __is_reference;
                sum += __is_rvalue_expr;
                sum += __is_rvalue_reference;
                sum += __is_same;
                sum += __is_scalar;
                sum += __is_signed;
                sum += __is_standard_layout;
                sum += __is_trivial;
                sum += __is_trivially_assignable;
                sum += __is_trivially_constructible;
                sum += __is_trivially_copyable;
                sum += __is_union;
                sum += __is_unsigned;
                sum += __is_void;
                sum += __is_volatile;
                sum += __label__;
                sum += __llvm__;
                sum += __module_private__;
                sum += __null;

                buffer[ThreadIds.X] = sum;
            }
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        public readonly partial struct DxcReservedNamesShader4 : IComputeShader
        {
            public readonly ReadWriteBuffer<float> buffer;
            public readonly float __nullptr;
            public readonly float __objc_no;
            public readonly float __objc_yes;
            public readonly float __pascal;
            public readonly float __private_extern__;
            public readonly float __real;
            public readonly float __real__;
            public readonly float __restrict;
            public readonly float __restrict__;
            public readonly float __signed;
            public readonly float __signed__;
            public readonly float __stdcall;
            public readonly float __thiscall;
            public readonly float __thread;
            public readonly float __typeof;
            public readonly float __typeof__;
            public readonly float __underlying_type;
            public readonly float __vectorcall;
            public readonly float __volatile;
            public readonly float __volatile__;
            public readonly float auto;
            public readonly float const_cast;
            public readonly float delete;
            public readonly float dynamic_cast;
            public readonly float ext_result_id;
            public readonly float ext_type;
            public readonly float float32_t;
            public readonly float float64_t;
            public readonly float friend;
            public readonly float int32_t;
            public readonly float int64_t;
            public readonly float int8_t4_packed;
            public readonly float mutable;
            public readonly float reinterpret_cast;
            public readonly float sampler_state;
            public readonly float signed;
            public readonly float static_cast;
            public readonly float std;
            public readonly float technique10;
            public readonly float technique11;
            public readonly float template;
            public readonly float typeid;
            public readonly float typename;
            public readonly float uint32_t;
            public readonly float uint64_t;
            public readonly float uint8_t4_packed;
            public readonly float union;
            public readonly float wchar_t;

            public void Execute()
            {
                float sum = 0;

                sum += __nullptr;
                sum += __objc_no;
                sum += __objc_yes;
                sum += __pascal;
                sum += __private_extern__;
                sum += __real;
                sum += __real__;
                sum += __restrict;
                sum += __restrict__;
                sum += __signed;
                sum += __signed__;
                sum += __stdcall;
                sum += __thiscall;
                sum += __thread;
                sum += __typeof;
                sum += __typeof__;
                sum += __underlying_type;
                sum += __vectorcall;
                sum += __volatile;
                sum += __volatile__;
                sum += auto;
                sum += const_cast;
                sum += delete;
                sum += dynamic_cast;
                sum += ext_result_id;
                sum += ext_type;
                sum += float32_t;
                sum += float64_t;
                sum += friend;
                sum += int32_t;
                sum += int64_t;
                sum += int8_t4_packed;
                sum += mutable;
                sum += reinterpret_cast;
                sum += sampler_state;
                sum += signed;
                sum += static_cast;
                sum += std;
                sum += technique10;
                sum += technique11;
                sum += template;
                sum += typeid;
                sum += typename;
                sum += uint32_t;
                sum += uint64_t;
                sum += uint8_t4_packed;
                sum += union;
                sum += wchar_t;

                buffer[ThreadIds.X] = sum;
            }
        }

        [AutoConstructor]
        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        public readonly partial struct DxcReservedNamesShader5 : IComputeShader
        {
            public readonly ReadWriteBuffer<float> buffer;
            public readonly float RAYTRACING_PIPELINE_FLAG_SKIP_PROCEDURAL_PRIMITIVES;
            public readonly float STATE_OBJECT_FLAG_ALLOW_STATE_OBJECT_ADDITIONS;
            public readonly float STATE_OBJECT_FLAGS_ALLOW_EXTERNAL_DEPENDENCIES_ON_LOCAL_DEFINITIONS;
            public readonly float STATE_OBJECT_FLAGS_ALLOW_LOCAL_DEPENDENCIES_ON_EXTERNAL_DEFINITONS;
            public readonly float STATE_OBJECT_FLAG_ALLOW_EXTERNAL_DEPENDENCIES_ON_LOCAL_DEFINITIONS;
            public readonly float STATE_OBJECT_FLAG_ALLOW_LOCAL_DEPENDENCIES_ON_EXTERNAL_DEFINITONS;
            public readonly float __TIMESTAMP__;
            public readonly float ALL_MEMORY;
            public readonly float BARRIER_SEMANTIC_FLAG;
            public readonly float DEVICE_SCOPE;
            public readonly float DispatchNodeInputRecord;
            public readonly float GROUP_SCOPE;
            public readonly float GROUP_SHARED_MEMORY;
            public readonly float GROUP_SYNC;
            public readonly float GroupNodeInputRecords;
            public readonly float GroupNodeOutputRecords;
            public readonly float MEMORY_TYPE_FLAG;
            public readonly float NODE_INPUT_MEMORY;
            public readonly float NODE_OUTPUT_MEMORY;
            public readonly float NodeOutput;
            public readonly float NodeOutputArray;
            public readonly float RWDispatchNodeInputRecord;
            public readonly float RWGroupNodeInputRecords;
            public readonly float RWThreadNodeInputRecord;
            public readonly float ThreadNodeInputRecord;
            public readonly float ThreadNodeOutputRecords;
            public readonly float UAV_MEMORY;

            public void Execute()
            {
                float sum = 0;

                sum += RAYTRACING_PIPELINE_FLAG_SKIP_PROCEDURAL_PRIMITIVES;
                sum += STATE_OBJECT_FLAG_ALLOW_STATE_OBJECT_ADDITIONS;
                sum += STATE_OBJECT_FLAGS_ALLOW_EXTERNAL_DEPENDENCIES_ON_LOCAL_DEFINITIONS;
                sum += STATE_OBJECT_FLAGS_ALLOW_LOCAL_DEPENDENCIES_ON_EXTERNAL_DEFINITONS;
                sum += STATE_OBJECT_FLAG_ALLOW_EXTERNAL_DEPENDENCIES_ON_LOCAL_DEFINITIONS;
                sum += STATE_OBJECT_FLAG_ALLOW_LOCAL_DEPENDENCIES_ON_EXTERNAL_DEFINITONS;
                sum += __TIMESTAMP__;
                sum += ALL_MEMORY;
                sum += BARRIER_SEMANTIC_FLAG;
                sum += DEVICE_SCOPE;
                sum += DispatchNodeInputRecord;
                sum += GROUP_SCOPE;
                sum += GROUP_SHARED_MEMORY;
                sum += GROUP_SYNC;
                sum += GroupNodeInputRecords;
                sum += GroupNodeOutputRecords;
                sum += MEMORY_TYPE_FLAG;
                sum += NODE_INPUT_MEMORY;
                sum += NODE_OUTPUT_MEMORY;
                sum += NodeOutput;
                sum += NodeOutputArray;
                sum += RWDispatchNodeInputRecord;
                sum += RWGroupNodeInputRecords;
                sum += RWThreadNodeInputRecord;
                sum += ThreadNodeInputRecord;
                sum += ThreadNodeOutputRecords;
                sum += UAV_MEMORY;

                buffer[ThreadIds.X] = sum;
            }
        }
    }
}