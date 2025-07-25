﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using JustEat.HttpClientInterception;
using MartinCostello.Costellobot.Drivers;
using MartinCostello.Costellobot.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using static MartinCostello.Costellobot.Builders.GitHubFixtures;

namespace MartinCostello.Costellobot.Handlers;

[Collection<AppCollection>]
public sealed class DeploymentProtectionRuleHandlerTests : IntegrationTests<AppFixture>
{
    public DeploymentProtectionRuleHandlerTests(AppFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper)
    {
        Fixture.ChangeClock(new(2023, 09, 01, 12, 34, 56, TimeSpan.Zero));
        Fixture.Interceptor.RegisterGoogleBundle();
    }

    [Fact]
    public async Task Deployment_Is_Approved()
    {
        // Arrange
        Fixture.ApproveDeployments();

        var deployment = CreateDeployment("production");
        var driver = new DeploymentProtectionRuleDriver(deployment);

        RegisterGetAccessToken();
        var deploymentApproved = RegisterApprovePendingDeployment(driver);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await deploymentApproved.Task.WaitAsync(ResultTimeout, CancellationToken);
    }

    [Fact]
    public async Task Deployment_Is_Not_Approved_If_Deployment_Approval_Disabled()
    {
        // Arrange
        Fixture.ApproveDeployments(false);

        var deployment = CreateDeployment("production");
        var driver = new DeploymentProtectionRuleDriver(deployment);

        var deploymentApproved = RegisterApprovePendingDeployment(driver);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(deploymentApproved);
    }

    [Fact]
    public async Task Deployment_Is_Approved_On_Public_Holiday()
    {
        // Arrange
        Fixture.ChangeClock(new(2023, 12, 25, 12, 00, 00, TimeSpan.Zero));
        Fixture.ApproveDeployments();

        var deployment = CreateDeployment("production");
        var driver = new DeploymentProtectionRuleDriver(deployment);

        RegisterGetAccessToken();
        var deploymentApproved = RegisterApprovePendingDeployment(driver);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await deploymentApproved.Task.WaitAsync(ResultTimeout, CancellationToken);
    }

    [Fact]
    public async Task Handler_Ignores_Events_That_Are_Not_Deployment_Statuses()
    {
        // Arrange
        var target = Fixture.Services.GetRequiredService<DeploymentProtectionRuleHandler>();
        var message = new Octokit.Webhooks.Events.IssueComment.IssueCommentCreatedEvent();

        // Act
        await Should.NotThrowAsync(() => target.HandleAsync(message, TestContext.Current.CancellationToken));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Fixture?.UseSystemClock();
    }

    private async Task<HttpResponseMessage> PostWebhookAsync(DeploymentProtectionRuleDriver driver, string action = "requested")
    {
        var value = driver.CreateWebhook(action);
        return await PostWebhookAsync("deployment_protection_rule", value);
    }

    private TaskCompletionSource RegisterApprovePendingDeployment(DeploymentProtectionRuleDriver driver)
    {
        var deploymentApproved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        RegisterApproveDeploymentProtectionRule(
            driver,
            (p) => p.WithInterceptionCallback((_) => deploymentApproved.SetResult()));

        return deploymentApproved;
    }

    private void RegisterApproveDeploymentProtectionRule(
        DeploymentProtectionRuleDriver driver,
        Action<HttpRequestInterceptionBuilder> configure)
    {
        var builder = CreateDefaultBuilder()
            .Requests()
            .ForPost()
            .ForPath($"/repos/{driver.Repository.FullName}/actions/runs/{driver.RunId}/deployment_protection_rule")
            .ForRequestHeader("Accept", "application/vnd.github+json")
            .Responds()
            .WithStatus(HttpStatusCode.NoContent);

        configure(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }
}
