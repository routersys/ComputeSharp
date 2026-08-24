using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ComputeWeave.Core.Intrinsics;

#pragma warning disable IDE0055

namespace ComputeWeave.SourceGeneration.Mappings;

/// <summary>
/// A <see langword="class"/> that contains and maps known HLSL identifier names valid HLSL names.
/// </summary>
internal static partial class HlslKnownKeywords
{
    /// <summary>
    /// The mapping of known HLSL keywords.
    /// </summary>
    private static readonly HashSet<string> KnownKeywords = BuildKnownKeywordsMap();

    /// <summary>
    /// Builds the mapping of all known HLSL keywords.
    /// </summary>
    private static HashSet<string> BuildKnownKeywordsMap()
    {
        // HLSL keywords
        HashSet<string> knownKeywords = new(
        [
            "asm", "asm_fragment", "cbuffer", "buffer", "texture", "centroid",
            "column_major", "compile", "discard", "dword", "export", "fxgroup",
            "groupshared", "half", "inline", "inout", "line", "lineadj", "linear",
            "matrix", "nointerpolation", "noperspective", "NULL", "packoffset", "pass",
            "pixelfragment", "point", "precise", "register", "row_major", "sample",
            "sampler", "shared", "snorm", "stateblock", "stateblock_state", "tbuffer",
            "technique", "typedef", "triangle", "triangleadj", "uniform", "unorm",
            "unsigned", "vector", "vertexfragment", "zero", "float1", "double1",
            "int1", "uint1", "bool1", "fragmentKeyword", "compile_fragment", "shaderProfile",
            "min10float", "min12int", "min16float", "min16int", "min16uint",
            "maxvertexcount", "TriangleStream", "LineStream", "PointStream",
            "AppendStructuredBuffer", "Buffer", "ByteAddressBuffer", "ConsumeStructuredBuffer",
            "InputPatch", "OutputPatch", "RWBuffer", "RWByteAddressBuffer", "RWStructuredBuffer",
            "RWTexture1D", "RWTexture1DArray", "RWTexture2D", "RWTexture2DArray", "RWTexture3D",
            "StructuredBuffer", "Texture1D", "Texture1DArray", "Texture2D", "Texture2DArray",
            "Texture3D", "Texture2DMS", "Texture2DMSArray", "TextureCube", "TextureCubeArray",
            "ConstantBuffer", "TextureBuffer", "RayQuery", "SubpassInput", "SubpassInputMS",
            "FeedbackTexture2D", "FeedbackTexture2DArray", "RasterizerOrderedBuffer",
            "RasterizerOrderedStructuredBuffer", "RasterizerOrderedTexture1D",
            "RasterizerOrderedTexture2D", "RasterizerOrderedTexture2DArray", "RasterizerOrderedTexture3D",
            "SV_DispatchThreadID", "SV_DomainLocation", "SV_GroupID", "SV_GroupIndex", "SV_GroupThreadID",
            "SV_GSInstanceID", "SV_InsideTessFactor", "SV_OutputControlPointID", "SV_TessFactor",
            "SV_InnerCoverage", "SV_StencilRef", "globallycoherent",
            "CANDIDATE_NON_OPAQUE_TRIANGLE", "CANDIDATE_PROCEDURAL_PRIMITIVE", "CANDIDATE_TYPE",
            "COMMITTED_NOTHING", "COMMITTED_PROCEDURAL_PRIMITIVE_HIT", "COMMITTED_STATUS", "COMMITTED_TRIANGLE_HIT",
            "HIT_KIND_NONE", "HIT_KIND_TRIANGLE_BACK_FACE", "HIT_KIND_TRIANGLE_FRONT_FACE",
            "RAYTRACING_PIPELINE_FLAG_NONE", "RAYTRACING_PIPELINE_FLAG_SKIP_TRIANGLES", "RAY_FLAG",
            "RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH", "RAY_FLAG_CULL_BACK_FACING_TRIANGLES",
            "RAY_FLAG_CULL_FRONT_FACING_TRIANGLES", "RAY_FLAG_CULL_NON_OPAQUE", "RAY_FLAG_CULL_OPAQUE",
            "RAY_FLAG_FORCE_NON_OPAQUE", "RAY_FLAG_FORCE_OPAQUE", "RAY_FLAG_NONE",
            "RAY_FLAG_SKIP_CLOSEST_HIT_SHADER", "RAY_FLAG_SKIP_PROCEDURAL_PRIMITIVES", "RAY_FLAG_SKIP_TRIANGLES",
            "RWTexture2DMS", "RWTexture2DMSArray", "RasterizerOrderedTexture1DArray", "SAMPLER_FEEDBACK_MIN_MIP",
            "SAMPLER_FEEDBACK_MIP_REGION_USED", "Technique", "_Alignas", "_Alignof", "_Atomic", "_Complex",
            "_Decimal128", "_Decimal32", "_Decimal64", "_Generic", "_Imaginary", "_Nonnull", "_Noreturn",
            "_Null_unspecified", "_Nullable", "_Pragma", "_Static_assert", "_Thread_local", "__BASE_FILE__",
            "__BYTE_ORDER__", "__COUNTER__", "__DATE__", "__DXC_VERSION_COMMITS", "__DXC_VERSION_MAJOR",
            "__DXC_VERSION_MINOR", "__DXC_VERSION_RELEASE", "__FILE__", "__FLT_RADIX__", "__FUNCTION__",
            "__GNUC_MINOR__", "__GNUC_PATCHLEVEL__", "__GNUC__", "__GXX_ABI_VERSION", "__HLSL_VERSION",
            "__INCLUDE_LEVEL__", "__LINE__", "__LITTLE_ENDIAN__", "__ORDER_BIG_ENDIAN__", "__ORDER_LITTLE_ENDIAN__",
            "__ORDER_PDP_ENDIAN__", "__PRETTY_FUNCTION__", "__SHADER_STAGE_AMPLIFICATION", "__SHADER_STAGE_COMPUTE",
            "__SHADER_STAGE_DOMAIN", "__SHADER_STAGE_GEOMETRY", "__SHADER_STAGE_HULL", "__SHADER_STAGE_LIBRARY",
            "__SHADER_STAGE_MESH", "__SHADER_STAGE_PIXEL", "__SHADER_STAGE_VERTEX", "__SHADER_TARGET_MAJOR",
            "__SHADER_TARGET_MINOR", "__SHADER_TARGET_STAGE", "__TIME__", "__VERSION__", "__alignof", "__alignof__",
            "__array_extent", "__array_rank", "__asm", "__asm__", "__attribute", "__attribute__",
            "__builtin_choose_expr", "__builtin_convertvector", "__builtin_offsetof",
            "__builtin_omp_required_simd_align", "__builtin_va_arg", "__builtin_va_list", "__cdecl", "__char16_t",
            "__char32_t", "__clang__", "__clang_major__", "__clang_minor__", "__clang_patchlevel__",
            "__clang_version__", "__complex", "__complex__", "__const", "__const__", "__declspec", "__decltype",
            "__extension__", "__fastcall", "__fp16", "__func__", "__has_attribute", "__has_builtin",
            "__has_cpp_attribute", "__has_declspec_attribute", "__has_extension", "__has_feature", "__has_include",
            "__has_include_next", "__has_nothrow_assign", "__has_nothrow_constructor", "__has_nothrow_copy",
            "__has_nothrow_move_assign", "__has_trivial_assign", "__has_trivial_constructor", "__has_trivial_copy",
            "__has_trivial_destructor", "__has_trivial_move_assign", "__has_trivial_move_constructor",
            "__has_virtual_destructor", "__has_warning", "__hlsl_dx_compiler", "__imag", "__imag__", "__inline",
            "__inline__", "__int128", "__is_abstract", "__is_arithmetic", "__is_array", "__is_base_of",
            "__is_class", "__is_complete_type", "__is_compound", "__is_const", "__is_constructible",
            "__is_convertible", "__is_convertible_to", "__is_empty", "__is_enum", "__is_final",
            "__is_floating_point", "__is_function", "__is_fundamental", "__is_identifier", "__is_integral",
            "__is_literal", "__is_literal_type", "__is_lvalue_expr", "__is_lvalue_reference",
            "__is_member_function_pointer", "__is_member_object_pointer", "__is_member_pointer",
            "__is_nothrow_assignable", "__is_nothrow_constructible", "__is_object", "__is_pod", "__is_pointer",
            "__is_polymorphic", "__is_reference", "__is_rvalue_expr", "__is_rvalue_reference", "__is_same",
            "__is_scalar", "__is_signed", "__is_standard_layout", "__is_trivial", "__is_trivially_assignable",
            "__is_trivially_constructible", "__is_trivially_copyable", "__is_union", "__is_unsigned", "__is_void",
            "__is_volatile", "__label__", "__llvm__", "__module_private__", "__null", "__nullptr", "__objc_no",
            "__objc_yes", "__pascal", "__private_extern__", "__real", "__real__", "__restrict", "__restrict__",
            "__signed", "__signed__", "__stdcall", "__thiscall", "__thread", "__typeof", "__typeof__",
            "__underlying_type", "__vectorcall", "__volatile", "__volatile__", "auto", "break", "case", "catch",
            "char", "class", "const", "const_cast", "continue", "default", "delete", "do", "dynamic_cast", "else",
            "enum", "explicit", "ext_result_id", "ext_type", "extern", "false", "float32_t", "float64_t", "for",
            "friend", "goto", "if", "in", "int32_t", "int64_t", "int8_t4_packed", "interface", "long", "mutable",
            "namespace", "new", "operator", "out", "private", "protected", "public", "reinterpret_cast", "return",
            "sampler_state", "short", "signed", "sizeof", "static", "static_cast", "std", "string", "struct",
            "switch", "technique10", "technique11", "template", "this", "throw", "true", "try", "typeid",
            "typename", "uint32_t", "uint64_t", "uint8_t4_packed", "union", "using", "virtual", "void", "volatile",
            "wchar_t", "while"

        ]);

        // HLSL primitive names
        foreach (Type? type in HlslKnownTypes.EnumerateKnownVectorTypes().Concat(HlslKnownTypes.EnumerateKnownMatrixTypes()))
        {
            _ = knownKeywords.Add(type.Name.ToLowerInvariant());
        }

        // HLSL intrinsics method names
        foreach (MethodInfo? method in typeof(Hlsl).GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            string name = method.GetCustomAttribute<HlslIntrinsicNameAttribute>()?.Name ?? method.Name;

            _ = knownKeywords.Add(name);
        }

        // Let other types inject additional keywords
        AddKnownKeywords(knownKeywords);

        return knownKeywords;
    }

    /// <summary>
    /// Adds more known keywords to the collection to use.
    /// </summary>
    /// <param name="knownKeywords">The collection of known keywords being built.</param>
    static partial void AddKnownKeywords(ICollection<string> knownKeywords);

    /// <summary>
    /// Tries to get the mapped HLSL-compatible identifier name for the input identifier name.
    /// </summary>
    /// <param name="name">The input identifier name.</param>
    /// <param name="mapped">The mapped identifier name, if a replacement is needed.</param>
    /// <returns>The HLSL-compatible identifier name that can be used in an HLSL shader.</returns>
    public static bool TryGetMappedName(string name, out string? mapped)
    {
        mapped = KnownKeywords.Contains(name) switch
        {
            true => $"__reserved__{name}",
            false => null
        };

        return mapped is not null;
    }
}