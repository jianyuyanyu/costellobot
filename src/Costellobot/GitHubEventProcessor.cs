﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Octokit.Webhooks;

namespace MartinCostello.Costellobot;

public sealed partial class GitHubEventProcessor(
    GitHubEventHandler handler,
    IHubContext<GitHubWebhookHub, IWebhookClient> hub,
    CostellobotMetrics metrics,
    IOptionsMonitor<WebhookOptions> options,
    ILogger<GitHubEventProcessor> logger) : WebhookEventProcessor
{
    private static readonly string[] HeadersToLog =
    [
        "Accept",
        "Content-Type",
        "User-Agent",
        "X-GitHub-Delivery",
        "X-GitHub-Event",
        "X-GitHub-Hook-ID",
        "X-GitHub-Hook-Installation-Target-ID",
        "X-GitHub-Hook-Installation-Target-Type",
        "X-Hub-Signature",
        "X-Hub-Signature-256",
    ];

    public override async ValueTask ProcessWebhookAsync(
        IDictionary<string, StringValues> headers,
        string body,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(body);

        if (options.CurrentValue.Disable)
        {
            return;
        }

        (var rawHeaders, var rawPayload) = await BroadcastLogAsync(headers, body, cancellationToken);

        var webhookHeaders = WebhookHeaders.Parse(headers);

        using (logger.BeginWebhookScope(webhookHeaders))
        {
            metrics.WebhookDelivery(webhookHeaders.Event, webhookHeaders.HookInstallationTargetId);

            var webhookEvent = DeserializeWebhookEvent(webhookHeaders, body);

            using (logger.BeginWebhookScope(webhookEvent))
            {
                Log.ReceivedWebhook(logger, webhookHeaders.Delivery);

                var payload = new GitHubEvent(webhookHeaders, webhookEvent, rawHeaders, rawPayload);

                await handler.HandleAsync(payload, cancellationToken);

                Log.ProcessedWebhook(logger, webhookHeaders.Delivery);
            }
        }
    }

    private async Task<(IDictionary<string, string> Headers, JsonElement Payload)> BroadcastLogAsync(
        IDictionary<string, StringValues> headers,
        string body,
        CancellationToken cancellationToken)
    {
        try
        {
            // Cannot serialize the parsed webhook objects as-is because DateTimeOffset does
            // not support being serialized and throws an exception, which breaks the SignalR connection.
            // See https://github.com/octokit/webhooks.net/blob/1a6ce29f8312c555227703057ba45723e3c78574/src/Octokit.Webhooks/Converter/DateTimeOffsetConverter.cs#L14.
            using var document = JsonDocument.Parse(body);

            var webhookHeaders = new Dictionary<string, string>(HeadersToLog.Length);

            foreach (string header in HeadersToLog)
            {
                if (headers.TryGetValue(header, out var value))
                {
                    webhookHeaders[header] = value!;
                }
            }

            await hub.Clients.All.WebhookAsync(webhookHeaders, document.RootElement, cancellationToken);

            return (webhookHeaders, document.RootElement.Clone());
        }
        catch (Exception)
        {
            // Swallow exception
            return default;
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Received webhook with ID {HookId}.")]
        public static partial void ReceivedWebhook(ILogger logger, string? hookId);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Debug,
            Message = "Processed webhook with ID {HookId}.")]
        public static partial void ProcessedWebhook(ILogger logger, string? hookId);
    }
}
