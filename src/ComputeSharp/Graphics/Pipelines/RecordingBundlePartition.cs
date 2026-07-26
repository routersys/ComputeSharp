using System;
using System.Threading;
using ComputeSharp.Resources.Lifetime;

namespace ComputeSharp.Graphics.Pipelines;

internal struct RecordingBundleEntry
{
    public int StorageOffset;

    public int Capacity;

    public int Count;
}

internal sealed class RecordingBundlePartition
{
    private readonly RecordingBundleEntry[] bundles;

    private readonly ResourceGenerationPin[] pins;

    private readonly bool[] isRented;

    private readonly Lock gate = new();

    public RecordingBundlePartition(int bundleCount, int pinCapacity)
    {
        default(ArgumentOutOfRangeException).ThrowIfNegative(bundleCount);
        default(ArgumentOutOfRangeException).ThrowIfNegative(pinCapacity);

        this.bundles = new RecordingBundleEntry[bundleCount];
        this.pins = new ResourceGenerationPin[checked(bundleCount * pinCapacity)];
        this.isRented = new bool[bundleCount];

        PinCapacity = pinCapacity;

        for (int i = 0; i < bundleCount; i++)
        {
            this.bundles[i] = new RecordingBundleEntry
            {
                StorageOffset = checked(i * pinCapacity),
                Capacity = pinCapacity,
                Count = 0
            };
        }
    }

    public int Capacity => this.bundles.Length;

    public int PinCapacity { get; }

    public Span<ResourceGenerationPin> Storage => this.pins;

    public int AvailableCount
    {
        get
        {
            lock (this.gate)
            {
                int count = 0;

                for (int i = 0; i < this.isRented.Length; i++)
                {
                    if (!this.isRented[i])
                    {
                        count++;
                    }
                }

                return count;
            }
        }
    }

    public bool TryRent(out int bundleIndex)
    {
        lock (this.gate)
        {
            for (int i = 0; i < this.isRented.Length; i++)
            {
                if (this.isRented[i])
                {
                    continue;
                }

                this.isRented[i] = true;
                bundleIndex = i;

                return true;
            }
        }

        bundleIndex = -1;

        return false;
    }

    public void Return(int bundleIndex)
    {
        lock (this.gate)
        {
            default(ArgumentOutOfRangeException).ThrowIfNotInRange(bundleIndex, 0, this.isRented.Length);
            default(InvalidOperationException).ThrowIf(!this.isRented[bundleIndex], "The recording bundle is not rented.");
            default(InvalidOperationException).ThrowIf(this.bundles[bundleIndex].Count != 0, "The recording bundle still holds pinned generations.");

            this.isRented[bundleIndex] = false;
        }
    }

    public ref RecordingBundleEntry GetBundle(int bundleIndex)
    {
        default(ArgumentOutOfRangeException).ThrowIfNotInRange(bundleIndex, 0, this.bundles.Length);

        return ref this.bundles[bundleIndex];
    }
}
