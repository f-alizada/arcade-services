// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Contracts;
using Maestro.Data.Models;
using NUnit.Framework;
using SubscriptionActorService.StateModel;
using Asset = Maestro.Contracts.Asset;

namespace SubscriptionActorService.Tests;

[TestFixture, NonParallelizable]
internal class UpdateAssetsAsyncTests : PullRequestActorTests
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
                    NewCommit,
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

    [TestCase(false)]
    [TestCase(true)]
    public async Task UpdateWithAssetsNoExistingPR(bool batchable)
    {
        GivenATestChannel();
        GivenASubscription(
            new SubscriptionPolicy
            {
                Batchable = batchable,
                UpdateFrequency = UpdateFrequency.EveryBuild
            });
        Build b = GivenANewBuild(true);

        WithRequireNonCoherencyUpdates();
        WithNoRequiredCoherencyUpdates();

        CreatePullRequestShouldReturnAValidValue();

        await WhenUpdateAssetsAsyncIsCalled(b);

        ThenGetRequiredUpdatesShouldHaveBeenCalled(b);
        AndCreateNewBranchShouldHaveBeenCalled();
        AndCommitUpdatesShouldHaveBeenCalled(b);
        AndCreatePullRequestShouldHaveBeenCalled();
        AndShouldHavePullRequestCheckReminder();
        AndShouldHaveInProgressPullRequestState(b);
        AndDependencyFlowEventsShouldBeAdded();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task UpdateWithAssetsExistingPR(bool batchable)
    {
        GivenATestChannel();
        GivenASubscription(
            new SubscriptionPolicy
            {
                Batchable = batchable,
                UpdateFrequency = UpdateFrequency.EveryBuild
            });
        Build b = GivenANewBuild(true);

        WithRequireNonCoherencyUpdates();
        WithNoRequiredCoherencyUpdates();

        using (WithExistingPullRequest(SynchronizePullRequestResult.InProgressCanUpdate))
        {
            await WhenUpdateAssetsAsyncIsCalled(b);
            ThenGetRequiredUpdatesShouldHaveBeenCalled(b);
            AndCommitUpdatesShouldHaveBeenCalled(b);
            AndUpdatePullRequestShouldHaveBeenCalled();
            AndShouldHavePullRequestCheckReminder();
            AndDependencyFlowEventsShouldBeAdded();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task UpdateWithAssetsExistingPRNotUpdatable(bool batchable)
    {
        GivenATestChannel();
        GivenASubscription(
            new SubscriptionPolicy
            {
                Batchable = batchable,
                UpdateFrequency = UpdateFrequency.EveryBuild
            });
        Build b = GivenANewBuild(true);

        WithRequireNonCoherencyUpdates();
        WithNoRequiredCoherencyUpdates();
        using (WithExistingPullRequest(SynchronizePullRequestResult.InProgressCannotUpdate))
        {
            await WhenUpdateAssetsAsyncIsCalled(b);

            ThenShouldHavePullRequestUpdateReminder();
            AndShouldHavePendingUpdateState(b);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task UpdateWithNoAssets(bool batchable)
    {
        GivenATestChannel();
        GivenASubscription(
            new SubscriptionPolicy
            {
                Batchable = batchable,
                UpdateFrequency = UpdateFrequency.EveryBuild
            });
        Build b = GivenANewBuild(true, Array.Empty<(string, string, bool)>());

        WithRequireNonCoherencyUpdates();
        WithNoRequiredCoherencyUpdates();

        await WhenUpdateAssetsAsyncIsCalled(b);

        ThenGetRequiredUpdatesShouldHaveBeenCalled(b);
        AndSubscriptionShouldBeUpdatedForMergedPullRequest(b);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task UpdateWithAssetsWhenStrictAlgorithmFails(bool batchable)
    {
        GivenATestChannel();
        GivenASubscription(
            new SubscriptionPolicy
            {
                Batchable = batchable,
                UpdateFrequency = UpdateFrequency.EveryBuild
            });
        Build b = GivenANewBuild(true);

        WithRequireNonCoherencyUpdates();
        WithFailsStrictCheckForCoherencyUpdates();

        CreatePullRequestShouldReturnAValidValue();

        await WhenUpdateAssetsAsyncIsCalled(b);

        ThenGetRequiredUpdatesShouldHaveBeenCalled(b);
        AndCreateNewBranchShouldHaveBeenCalled();
        AndCommitUpdatesShouldHaveBeenCalled(b);
        AndCreatePullRequestShouldHaveBeenCalled();
        AndShouldHavePullRequestCheckReminder();
        AndShouldHaveInProgressPullRequestState(b,
            coherencyCheckSuccessful: false,
            coherencyErrors: [
                new CoherencyErrorDetails()
                    {
                        Error = "Repo @ commit does not contain dependency fakeDependency",
                        PotentialSolutions = new List<string>()
                    }
            ]);
        AndDependencyFlowEventsShouldBeAdded();
    }

    [Test]
    public async Task UpdateWithCodeFlowNoExistingStateOrPrBranch()
    {
        GivenATestChannel();
        GivenACodeFlowSubscription(
            new SubscriptionPolicy
            {
                Batchable = false,
                UpdateFrequency = UpdateFrequency.EveryBuild,
            });
        Build build = GivenANewBuild(true);

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
    public async Task UpdateWithCodeFlowWaitingForPrBranch()
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
        AndPcsShouldNotHaveBeenCalled(build);
        AndShouldHaveCodeFlowState(build, InProgressPrHeadBranch);
        AndShouldHavePendingUpdateState(build, isCodeFlow: true);
        AndShouldHaveFollowingState(
            codeFlowState: true,
            codeFlowReminder: true,
            pullRequestUpdateState: true);
    }

    [Test]
    public async Task UpdateWithCodeFlowWithPrBranchReady()
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
        AndPcsShouldNotHaveBeenCalled(build);
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
}
