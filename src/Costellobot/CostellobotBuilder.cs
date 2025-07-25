﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.IO.Compression;
using Azure.Data.Tables;
using Azure.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;

namespace MartinCostello.Costellobot;

public static class CostellobotBuilder
{
    public static WebApplicationBuilder AddCostellobot(this WebApplicationBuilder builder)
    {
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions()
        {
            ExcludeVisualStudioCredential = true,
        });

        if (builder.Configuration["ConnectionStrings:AzureKeyVault"] is { Length: > 0 })
        {
            builder.Configuration.AddAzureKeyVaultSecrets("AzureKeyVault", (p) => p.Credential = credential);
        }

        builder.AddAzureServiceBusClient("AzureServiceBus", (p) => p.Credential = credential);

        if (builder.Configuration["ConnectionStrings:AzureBlobStorage"] is { Length: > 0 })
        {
            builder.AddAzureBlobClient("AzureBlobStorage", (p) => p.Credential = credential);
        }

        if (builder.Configuration["ConnectionStrings:AzureTableStorage"] is { Length: > 0 })
        {
            builder.AddAzureTableClient("AzureTableStorage", (p) => p.Credential = credential);
        }

        builder.Services.AddAntiforgery();
        builder.Services.AddApplicationHealthChecks();
        builder.Services.AddGitHub(builder.Configuration);
        builder.Services.AddHsts((options) => options.MaxAge = TimeSpan.FromDays(180));
        builder.Services.AddResponseCaching();
        builder.Services.AddTelemetry(builder.Environment);

        builder.Services.AddResponseCompression((options) =>
        {
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });

        builder.Services.AddMetrics();
        builder.Services.AddSingleton<CostellobotMetrics>();

        builder.Services.AddSignalR();
        builder.Services.AddSingleton<ClientLogQueue>();
        builder.Services.AddHostedService<ClientLogBroadcastService>();

        builder.Logging.AddTelemetry();
        builder.Logging.AddSignalR();

        builder.Services.ConfigureHttpJsonOptions((options) =>
        {
            options.SerializerOptions.WriteIndented = true;
        });

        builder.Services.Configure<StaticFileOptions>((options) =>
        {
            options.OnPrepareResponse = (context) =>
            {
                var maxAge = TimeSpan.FromDays(7);

                if (context.File.Exists)
                {
                    string? extension = Path.GetExtension(context.File.PhysicalPath);

                    // These files are served with a content hash in the URL so can be cached for longer
                    bool isScriptOrStyle =
                        string.Equals(extension, ".css", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(extension, ".js", StringComparison.OrdinalIgnoreCase);

                    if (isScriptOrStyle)
                    {
                        maxAge = TimeSpan.FromDays(365);
                    }
                }

                context.Context.Response.GetTypedHeaders().CacheControl = new() { MaxAge = maxAge };
            };
        });

        builder.Services.Configure<BrotliCompressionProviderOptions>((p) => p.Level = CompressionLevel.Fastest);
        builder.Services.Configure<GzipCompressionProviderOptions>((p) => p.Level = CompressionLevel.Fastest);

        if (string.Equals(builder.Configuration["CODESPACES"], bool.TrueString, StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.Configure<ForwardedHeadersOptions>(
                (options) => options.ForwardedHeaders |= ForwardedHeaders.XForwardedHost);
        }

        builder.WebHost.ConfigureKestrel((p) => p.AddServerHeader = false);

        if (builder.Configuration["Sentry:Dsn"] is { Length: > 0 } dsn)
        {
            builder.WebHost.UseSentry(dsn);
        }

        return builder;
    }

    public static WebApplication UseCostellobot(this WebApplication app)
    {
        if (ApplicationTelemetry.IsPyroscopeConfigured())
        {
            app.UseMiddleware<PyroscopeK6Middleware>();
        }

        if (app.Environment.IsDevelopment() &&
            app.Services.GetService<TableServiceClient>() is { } tableClient)
        {
            tableClient.CreateTableIfNotExists("TrustStore");
        }

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/error");
        }

        app.UseMiddleware<CustomHttpHeadersMiddleware>();

        app.UseStatusCodePagesWithReExecute("/error", "?id={0}");

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();

            if (!string.Equals(app.Configuration["ForwardedHeaders_Enabled"], bool.TrueString, StringComparison.OrdinalIgnoreCase))
            {
                app.UseHttpsRedirection();
            }

            app.UseResponseCompression();
        }

        app.UseStaticFiles();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseAntiforgery();

        app.MapHealthCheckRoutes();
        app.MapRedirects();
        app.MapAuthenticationRoutes();
        app.MapApiRoutes();
        app.MapAdminRoutes();

        return app;
    }
}
