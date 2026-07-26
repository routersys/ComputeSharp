using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Tests.Attributes;
using ComputeSharp.Tests.Extensions;
using ComputeSharp.Tests.Internals.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public unsafe partial class ComputeSubmissionTests
{
    [TestMethod]
    public void DefaultSubmissionIsACompletedNoOp()
    {
        ComputeSubmission submission = default;

        Assert.IsTrue(submission.Completion.IsNone);
        Assert.AreEqual(ComputeQueueKind.None, submission.Completion.Queue);
        Assert.AreEqual(ComputeSubmissionStatus.Succeeded, submission.Status);
        Assert.IsTrue(submission.IsCompleted);

        submission.Wait();

        Assert.AreEqual(ComputeSubmissionStatus.Succeeded, submission.Status);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReportsSucceededOnceTheCompletionFenceIsReached(Device device)
    {
        PipelineHostRuntime host = PipelineSubmissionSetup.Host(device, out DeviceRegistrationRegistry registry);

        try
        {
            CompletionRegistry completion = new();
            int index = PipelineSubmissionSetup.RecordAndPrepare(host, 1, out SubmissionRetention retention);

            ComputeSubmission submission = ComputeSubmissionExecutor.Submit(
                device.Get(),
                host,
                completion,
                index,
                copyFenceWaitValue: 0,
                in retention);

            Assert.AreEqual(ComputeQueueKind.Compute, submission.Completion.Queue);
            Assert.AreNotEqual(0ul, submission.Completion.Value);

            submission.Wait();

            Assert.AreEqual(ComputeSubmissionStatus.Succeeded, submission.Status);
            Assert.IsTrue(submission.IsCompleted);

            Assert.IsTrue(ComputeSubmissionExecutor.TryReleaseCompleted(device.Get(), completion));

            Assert.AreEqual(ComputeSubmissionStatus.Succeeded, submission.Status);
            Assert.IsTrue(submission.IsCompleted);
        }
        finally
        {
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReportsSucceededForEverySubmissionOfASequence(Device device)
    {
        PipelineHostRuntime host = PipelineSubmissionSetup.Host(device, out DeviceRegistrationRegistry registry, maximumPendingSubmissions: 2);

        try
        {
            CompletionRegistry completion = new();

            int first = PipelineSubmissionSetup.RecordAndPrepare(host, 1, out SubmissionRetention firstRetention);
            ComputeSubmission firstSubmission = ComputeSubmissionExecutor.Submit(
                device.Get(), host, completion, first, 0, in firstRetention);

            int second = PipelineSubmissionSetup.RecordAndPrepare(host, 2, out SubmissionRetention secondRetention);
            ComputeSubmission secondSubmission = ComputeSubmissionExecutor.Submit(
                device.Get(), host, completion, second, 0, in secondRetention);

            Assert.IsTrue(secondSubmission.Completion.Value > firstSubmission.Completion.Value);

            secondSubmission.Wait();

            Assert.AreEqual(ComputeSubmissionStatus.Succeeded, firstSubmission.Status);
            Assert.AreEqual(ComputeSubmissionStatus.Succeeded, secondSubmission.Status);
        }
        finally
        {
            registry.Dispose();
        }
    }
}
