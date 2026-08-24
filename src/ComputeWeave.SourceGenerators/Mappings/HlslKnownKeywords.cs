using System.Collections.Generic;

namespace ComputeWeave.SourceGeneration.Mappings;

/// <inheritdoc/>
internal static partial class HlslKnownKeywords
{
    /// <inheritdoc/>
    static partial void AddKnownKeywords(ICollection<string> knownKeywords)
    {
        // Dispatch type names
        knownKeywords.Add(nameof(ThreadIds));
        knownKeywords.Add(nameof(GroupIds));
        knownKeywords.Add(nameof(GroupSize));
        knownKeywords.Add(nameof(GridIds));

        knownKeywords.Add("__x");
        knownKeywords.Add("__y");
        knownKeywords.Add("__z");
    }
}