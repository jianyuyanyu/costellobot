﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit.Webhooks;

namespace MartinCostello.Costellobot.Handlers;

public interface IHandler
{
    Task HandleAsync(WebhookEvent message, CancellationToken cancellationToken);
}
