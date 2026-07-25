namespace ComputeSharp.Graphics.Pipelines;

internal readonly record struct HostRegistrationId(ulong Value);

internal readonly record struct ResourceSetRegistrationId(ulong Value);

internal readonly record struct SlotOrdinal(uint Value);

internal readonly record struct PipelineOrdinal(uint Value);

internal readonly record struct PipelineKey(HostRegistrationId Host, PipelineOrdinal Pipeline);

internal readonly record struct ResourceOrdinal(uint Value);

internal readonly record struct ResourceId(ulong Value);

internal readonly record struct ResourceGenerationId(ulong Value);

internal readonly record struct ResourceGenerationSetId(ulong Value);

internal readonly record struct ExternalDomainId(ulong Value);
