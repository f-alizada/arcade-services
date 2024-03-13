// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Threading.Tasks;
using Maestro.Data.Models;
using NUnit.Framework;
using SubscriptionActorService.StateModel;

using Asset = Maestro.Contracts.Asset;

namespace SubscriptionActorService.Tests;

/// <summary>
/// Tests the code flow PR update logic.
/// The tests are writter in the order in which the different phases of the PR are written.
/// Each test should have the inner state that is left behind by the previous state.
/// </summary>
[TestFixture, NonParallelizable]
internal class UpdateAssetsForCodeFlowTests : PullRequestActorTests
{
    private async Task WhenUpdateAssetsAsyncIsCalled(Build forBuild)
    {
        await Execute(
            async context =>
            {
                PullRequestActor actor = CreateActor(context);
                await actor.Implementation!.UpdateAssetsAsync(
                    Subscription.Id,
                    forBuild.Id,
                    SourceRepo,
                    forBuild.Commit,
                    forBuild.Assets.Select(
                        a => new Asset
                        {
                            Name = a.Name,
                            Version = a.Version
                        })
                    .ToList(),
                    Subscription.SourceEnabled);
            });
    }

    [Test]
    public async Task UpdateWithNoExistingStateOrPrBranch()
    {
        GivenATestChannel();
        GivenACodeFlowSubscription(
            new SubscriptionPolicy
            {
                Batchable = false,
                UpdateFrequency = UpdateFrequency.EveryBuild,
            });
        Build build = GivenANewBuild(true);
        ExpectPcsToGetCalled(build);

        await WhenUpdateAssetsAsyncIsCalled(build);

        ThenShouldHaveCodeFlowReminder();
        var requestedBranch = AndPcsShouldHaveBeenCalled(build);
        AndShouldHaveCodeFlowState(build, requestedBranch);
        AndShouldHavePendingUpdateState(build, isCodeFlow: true);
        AndShouldHaveFollowingState(
            codeFlowState: true,
            codeFlowReminder: true,
            pullRequestUpdateState: true);
    }

    [Test]
    public async Task WaitForPrBranch()
    {
        GivenATestChannel();
        GivenACodeFlowSubscription(
            new SubscriptionPolicy
            {
                Batchable = false,
                UpdateFrequency = UpdateFrequency.EveryBuild,
            });
        Build build = GivenANewBuild(true);

        GivenAPendingCodeFlowReminder();
        GivenPendingUpdates(build, true);
        WithExistingCodeFlowStatus(build);
        WithoutExistingPrBranch();

        await WhenUpdateAssetsAsyncIsCalled(build);

        ThenShouldHaveCodeFlowReminder();
        AndShouldHaveCodeFlowState(build, InProgressPrHeadBranch);
        AndShouldHavePendingUpdateState(build, isCodeFlow: true);
        AndShouldHaveFollowingState(
            codeFlowState: true,
            codeFlowReminder: true,
            pullRequestUpdateState: true);
    }

    [Test]
    public async Task UpdateWithPrBranchReady()
    {
        GivenATestChannel();
        GivenACodeFlowSubscription(
            new SubscriptionPolicy
            {
                Batchable = false,
                UpdateFrequency = UpdateFrequency.EveryBuild,
            });
        Build build = GivenANewBuild(true);

        GivenAPendingCodeFlowReminder();
        GivenPendingUpdates(build, true);
        WithExistingCodeFlowStatus(build);
        WithExistingPrBranch();
        CreatePullRequestShouldReturnAValidValue();

        await WhenUpdateAssetsAsyncIsCalled(build);

        ThenUpdateReminderIsRemoved();
        AndCodeFlowPullRequestShouldHaveBeenCreated();
        AndShouldHaveCodeFlowState(build, InProgressPrHeadBranch);
        AndShouldHavePullRequestCheckReminder();
        AndShouldHaveInProgressCodeFlowPullRequestState(build);
        AndDependencyFlowEventsShouldBeAdded();
        AndPendingUpdateIsRemoved();
        AndShouldHaveFollowingState(
            codeFlowState: true,
            pullRequestState: true,
            pullRequestCheckReminder: true);
    }

    [Test]
    public async Task UpdateWithPrNotUpdatable()
    {
        GivenATestChannel();
        GivenACodeFlowSubscription(
            new SubscriptionPolicy
            {
                Batchable = false,
                UpdateFrequency = UpdateFrequency.EveryBuild,
            });
        Build build = GivenANewBuild(true);

        GivenAPullRequestCheckReminder();
        WithExistingCodeFlowStatus(build);
        WithExistingPrBranch();

        using (WithExistingCodeFlowPullRequest(SynchronizePullRequestResult.InProgressCannotUpdate))
        {
            await WhenUpdateAssetsAsyncIsCalled(build);

            AndShouldHaveCodeFlowState(build, InProgressPrHeadBranch);
            AndShouldHavePullRequestCheckReminder();
            AndShouldHaveFollowingState(
                codeFlowState: true,
                pullRequestState: true,
                pullRequestCheckReminder: true);
        }
    }

    [Test]
    public async Task UpdateWithPrUpdatableButNoUpdates()
    {
        GivenATestChannel();
        GivenACodeFlowSubscription(
            new SubscriptionPolicy
            {
                Batchable = false,
                UpdateFrequency = UpdateFrequency.EveryBuild,
            });
        Build build = GivenANewBuild(true);

        GivenAPullRequestCheckReminder();
        WithExistingCodeFlowStatus(build);
        WithExistingPrBranch();

        using (WithExistingCodeFlowPullRequest(SynchronizePullRequestResult.InProgressCanUpdate))
        {
            await WhenUpdateAssetsAsyncIsCalled(build);

            AndShouldHaveCodeFlowState(build, InProgressPrHeadBranch);
            AndShouldHavePullRequestCheckReminder();
            AndShouldHaveFollowingState(
                codeFlowState: true,
                pullRequestState: true,
                pullRequestCheckReminder: true);
        }
    }

    [Test]
    public async Task UpdateCodeFlowPrWithNewBuild()
    {
        GivenATestChannel();
        GivenACodeFlowSubscription(
            new SubscriptionPolicy
            {
                Batchable = false,
                UpdateFrequency = UpdateFrequency.EveryBuild,
            });

        Build oldBuild = GivenANewBuild(true);
        Build newBuild = GivenANewBuild(true);
        newBuild.Commit = "sha456";

        GivenAPullRequestCheckReminder();
        WithExistingCodeFlowStatus(oldBuild);
        WithExistingPrBranch();
        ExpectPcsToGetCalled(newBuild);

        using (WithExistingCodeFlowPullRequest(SynchronizePullRequestResult.InProgressCanUpdate))
        {
            await WhenUpdateAssetsAsyncIsCalled(newBuild);

            AndPcsShouldHaveBeenCalled(newBuild, InProgressPrUrl);
            AndShouldHaveCodeFlowState(newBuild, InProgressPrHeadBranch);
            AndShouldHavePullRequestCheckReminder();
            AndShouldHaveFollowingState(
                codeFlowState: true,
                pullRequestState: true,
                pullRequestCheckReminder: true);
        }
    }
}
