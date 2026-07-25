using System;
using System.IO;

namespace ComputeSharp.Graphics.Pipelines;

internal static class PipelineCanonicalSignatureValidator
{
    private const char ComponentSeparator = '|';

    private const char ParameterSeparator = ':';

    private const string ZeroGenericArity = "00000000";

    private const string VoidReturnTypeMetadataName = "System.Void";

    private const string ComputeContextTypeMetadataName = "ComputeSharp.ComputeContext";

    private const byte InRefKindValue = 3;

    private const byte MaximumRefKindValue = 3;

    public static void Validate(string canonicalSignature, string hostTypeMetadataName, string methodMetadataName)
    {
        ReadOnlySpan<char> remaining = canonicalSignature.AsSpan();

        if (remaining.IsEmpty || remaining[^1] is ComponentSeparator)
        {
            throw Invalid();
        }

        if (!TryReadComponent(ref remaining, out ReadOnlySpan<char> containingTypeName) ||
            containingTypeName.IndexOf(ParameterSeparator) >= 0 ||
            !containingTypeName.SequenceEqual(hostTypeMetadataName.AsSpan()))
        {
            throw Invalid();
        }

        if (!TryReadComponent(ref remaining, out ReadOnlySpan<char> methodName) ||
            methodName.IndexOf(ParameterSeparator) >= 0 ||
            !methodName.SequenceEqual(methodMetadataName.AsSpan()))
        {
            throw Invalid();
        }

        if (!TryReadComponent(ref remaining, out ReadOnlySpan<char> genericArity) ||
            !genericArity.SequenceEqual(ZeroGenericArity.AsSpan()))
        {
            throw Invalid();
        }

        if (!TryReadComponent(ref remaining, out ReadOnlySpan<char> returnTypeName) ||
            !returnTypeName.SequenceEqual(VoidReturnTypeMetadataName.AsSpan()))
        {
            throw Invalid();
        }

        if (!TryReadComponent(ref remaining, out ReadOnlySpan<char> parameterCountText) ||
            !TryParseHexadecimal(parameterCountText, 8, out uint parameterCount) ||
            parameterCount == 0)
        {
            throw Invalid();
        }

        for (uint i = 0; i < parameterCount; i++)
        {
            if (!TryReadComponent(ref remaining, out ReadOnlySpan<char> parameter))
            {
                throw Invalid();
            }

            int separatorIndex = parameter.IndexOf(ParameterSeparator);

            if (separatorIndex != 2)
            {
                throw Invalid();
            }

            if (!TryParseHexadecimal(parameter[..separatorIndex], 2, out uint refKindValue) ||
                refKindValue > MaximumRefKindValue)
            {
                throw Invalid();
            }

            ReadOnlySpan<char> parameterTypeName = parameter[(separatorIndex + 1)..];

            if (parameterTypeName.IsEmpty || parameterTypeName.IndexOf(ParameterSeparator) >= 0)
            {
                throw Invalid();
            }

            if (i == 0 &&
                (refKindValue != InRefKindValue ||
                 !parameterTypeName.SequenceEqual(ComputeContextTypeMetadataName.AsSpan())))
            {
                throw Invalid();
            }
        }

        if (!remaining.IsEmpty)
        {
            throw Invalid();
        }
    }

    private static bool TryReadComponent(ref ReadOnlySpan<char> remaining, out ReadOnlySpan<char> component)
    {
        if (remaining.IsEmpty)
        {
            component = default;

            return false;
        }

        int separatorIndex = remaining.IndexOf(ComponentSeparator);

        if (separatorIndex < 0)
        {
            component = remaining;
            remaining = default;
        }
        else
        {
            component = remaining[..separatorIndex];
            remaining = remaining[(separatorIndex + 1)..];
        }

        return !component.IsEmpty;
    }

    private static bool TryParseHexadecimal(ReadOnlySpan<char> text, int length, out uint value)
    {
        value = 0;

        if (text.Length != length)
        {
            return false;
        }

        for (int i = 0; i < text.Length; i++)
        {
            char character = text[i];
            uint digit;

            if (character is >= '0' and <= '9')
            {
                digit = (uint)(character - '0');
            }
            else if (character is >= 'A' and <= 'F')
            {
                digit = (uint)(character - 'A' + 10);
            }
            else
            {
                return false;
            }

            value = (value << 4) | digit;
        }

        return true;
    }

    private static InvalidDataException Invalid()
    {
        return new InvalidDataException("The canonical pipeline signature is invalid.");
    }
}
