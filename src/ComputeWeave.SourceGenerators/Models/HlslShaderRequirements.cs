namespace ComputeWeave.SourceGeneration.Models;

/// <inheritdoc/>
partial class HlslShaderRequirements
{
    /// <summary>
    /// Gets or sets whether or not the shader uses a texture sampler at least once.
    /// </summary>
    public bool IsSamplerUsed { get; set; }

    /// <summary>
    /// Gets or sets whether or not the shader waits for its whole thread group at least once.
    /// </summary>
    public bool SynchronizesTheWholeThreadGroup { get; set; }
}