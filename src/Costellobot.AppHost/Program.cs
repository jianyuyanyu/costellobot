﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

var builder = DistributedApplication.CreateBuilder(args);

const string BlobStorage = "AzureBlobStorage";
const string KeyVault = "AzureKeyVault";
const string Project = "Costellobot";
const string ServiceBus = "AzureServiceBus";
const string Storage = "AzureStorage";
const string TableStorage = "AzureTableStorage";

var storage = builder.AddAzureStorage(Storage)
                     .RunAsEmulator((container) =>
                     {
                         container.WithDataVolume()
                                  .WithLifetime(ContainerLifetime.Persistent);
                     });

var blobStorage = storage.AddBlobs(BlobStorage);

var tableStorage = storage.AddTables(TableStorage);

var secrets = builder.ExecutionContext.IsPublishMode
    ? builder.AddAzureKeyVault(KeyVault)
    : builder.AddConnectionString(KeyVault);

var serviceBus = builder.AddAzureServiceBus(ServiceBus)
                        .RunAsEmulator((container) => container.WithLifetime(ContainerLifetime.Persistent));

var webhooks = serviceBus.AddServiceBusQueue("webhooks");

var dashboardUrl = DashboardUrl(builder.Configuration["DASHBOARD_URL"]);

builder.AddProject<Projects.Costellobot>(Project)
       .WithEnvironment("Site:LogsUrl", dashboardUrl)
       .WithHttpHealthCheck("/health/liveness")
       .WithHttpHealthCheck("/health/readiness")
       .WithHttpHealthCheck("/health/startup")
       .WithReference(secrets)
       .WithReference(blobStorage)
       .WithReference(tableStorage)
       .WithReference(serviceBus)
       .WaitFor(blobStorage)
       .WaitFor(tableStorage)
       .WaitFor(serviceBus)
       .WaitFor(webhooks);

var app = builder.Build();

app.Run();

static string? DashboardUrl(string? urls)
{
    if (string.IsNullOrWhiteSpace(urls))
    {
        return null;
    }

    var url = urls.Split(';').FirstOrDefault();

    if (string.IsNullOrWhiteSpace(url))
    {
        return null;
    }

    var builder = new UriBuilder(url)
    {
        Path = $"/structuredlogs/resource/{Project}",
    };

    return builder.Uri.ToString();
}
