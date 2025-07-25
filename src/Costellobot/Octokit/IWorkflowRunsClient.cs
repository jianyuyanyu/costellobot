﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace Octokit;

public interface IWorkflowRunsClient
{
    Task<IReadOnlyList<PendingDeployment>> GetPendingDeploymentsAsync(
        string owner,
        string name,
        long runId);

    Task ReviewCustomProtectionRuleAsync(
        string deploymentCallbackUrl,
        ReviewDeploymentProtectionRule review,
        CancellationToken cancellationToken);
}
