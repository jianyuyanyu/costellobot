﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using JustEat.HttpClientInterception;
using MartinCostello.Costellobot.Builders;
using MartinCostello.Costellobot.Infrastructure;
using NSubstitute;
using Octokit;
using Octokit.Internal;

namespace MartinCostello.Costellobot.Registries;

public static class GitHubActionsPackageRegistryTests
{
    [Theory]
    [InlineData("actions/checkout", "3", new[] { "actions" })]
    [InlineData("actions/checkout", "v3", new[] { "actions" })]
    [InlineData("actions/checkout", "2541b1294d2704b0964813337f33b291d3f8596b", new[] { "actions" })]
    [InlineData("foo", "1.0.0", new string[0])]
    [InlineData("foo/bar", "v1", new string[0])]
    public static async Task Can_Get_Package_Owners(string id, string version, string[] expected)
    {
        // Arrange
        var repository = new RepositoryId("some-org", "some-repo");

        var options = await new HttpClientInterceptorOptions()
            .ThrowsOnMissingRegistration()
            .RegisterBundleFromResourceStreamAsync("github-refs", cancellationToken: TestContext.Current.CancellationToken);

        using var httpClient = new HttpClientAdapter(options.CreateHttpMessageHandler);
        var connection = new Connection(new("costellobot", "1.0.0"), httpClient);
        var client = new GitHubClientAdapter(connection);
        var graphConnection = Substitute.For<Octokit.GraphQL.IConnection>();

        var clientFactory = Substitute.For<IGitHubClientFactory>();

        clientFactory.CreateForGraphQL(GitHubFixtures.InstallationId)
                     .Returns(graphConnection);

        clientFactory.CreateForApp(GitHubFixtures.AppId)
                     .Returns(client);

        clientFactory.CreateForInstallation(GitHubFixtures.InstallationId)
                     .Returns(client);

        clientFactory.CreateForUser()
                     .Returns(client);

        var context = new GitHubWebhookContext(
            clientFactory,
            new GitHubOptions().ToMonitor(),
            new WebhookOptions().ToMonitor())
        {
            AppId = GitHubFixtures.AppId,
            InstallationId = GitHubFixtures.InstallationId,
        };

        var target = new GitHubActionsPackageRegistry(context);

        // Act
        var actual = await target.GetPackageOwnersAsync(
            repository,
            id,
            version,
            TestContext.Current.CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.ShouldBe(expected);
    }
}
