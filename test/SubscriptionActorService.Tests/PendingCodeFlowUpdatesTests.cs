// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using NUnit.Framework;

namespace SubscriptionActorService.Tests;

[TestFixture, NonParallelizable]
internal class PendingCodeFlowUpdatesTests : PullRequestActorTests
{
    private async Task WhenProcessCodeFlowReminderAsyncIsCalled()
    {
        await Execute(
            async context =>
            {
                PullRequestActor actor = CreateActor(context);
                await actor.Implementation!.ProcessCodeFlowReminderAsync();
            });
    }
}
