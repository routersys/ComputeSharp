using System;

namespace ComputeSharp.Graphics.Pipelines;

internal sealed class PendingSubmissionRecordPartition
{
    private readonly PendingSubmissionRecord[] records;

    private readonly bool[] isCheckedOut;

    private readonly int[] freeIndices;

    private int head;

    private int tail;

    private int size;

    public PendingSubmissionRecordPartition(int capacity)
    {
        default(ArgumentOutOfRangeException).ThrowIfNegative(capacity);

        this.records = new PendingSubmissionRecord[capacity];
        this.isCheckedOut = new bool[capacity];
        this.freeIndices = new int[capacity];
        this.head = 0;
        this.tail = 0;
        this.size = capacity;

        for (int i = 0; i < capacity; i++)
        {
            this.records[i].State = SubmissionState.Returned;
            this.freeIndices[i] = i;
        }
    }

    public int Capacity => this.records.Length;

    public int AvailableCount
    {
        get
        {
            lock (this.records)
            {
                return this.size;
            }
        }
    }

    public bool HasCheckedOutRecords
    {
        get
        {
            lock (this.records)
            {
                return this.size != this.records.Length;
            }
        }
    }

    public bool TryCheckout(PipelineKey pipeline, ulong submissionSequence, out int index)
    {
        lock (this.records)
        {
            if (this.size <= 0)
            {
                index = -1;

                return false;
            }

            index = this.freeIndices[this.head++];

            if (this.head == this.freeIndices.Length)
            {
                this.head = 0;
            }

            this.size--;
            this.isCheckedOut[index] = true;
        }

        default(InvalidOperationException).ThrowIf(
            !this.records[index].TryReserve(pipeline, submissionSequence),
            "The checked out pending submission record was not returned.");

        return true;
    }

    public ref PendingSubmissionRecord GetRecord(int index)
    {
        default(ArgumentOutOfRangeException).ThrowIfNotInRange(index, 0, this.records.Length);

        return ref this.records[index];
    }

    public void Return(int index)
    {
        lock (this.records)
        {
            default(ArgumentOutOfRangeException).ThrowIfNotInRange(index, 0, this.records.Length);
            default(InvalidOperationException).ThrowIf(!this.isCheckedOut[index], "The pending submission record is not checked out.");
            default(InvalidOperationException).ThrowIf(
                this.records[index].ReadState() is not SubmissionState.Returned,
                "The pending submission record has not completed its return.");

            this.isCheckedOut[index] = false;
            this.freeIndices[this.tail++] = index;

            if (this.tail == this.freeIndices.Length)
            {
                this.tail = 0;
            }

            this.size++;
        }
    }
}
