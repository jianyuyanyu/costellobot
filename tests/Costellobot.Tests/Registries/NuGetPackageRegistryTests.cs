﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using JustEat.HttpClientInterception;
using MartinCostello.Costellobot.Infrastructure;

namespace MartinCostello.Costellobot.Registries;

public class NuGetPackageRegistryTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [InlineData("AWSSDK.S3", "3.7.9.32", new[] { "awsdotnet" })]
    [InlineData("JustEat.HttpClientInterception", "3.1.1", new[] { "JUSTEAT_OSS" })]
    [InlineData("MartinCostello.Logging.XUnit", "0.3.0", new[] { "martin_costello" })]
    [InlineData("Microsoft.AspNetCore.Mvc.Testing", "6.0.7", new[] { "aspnet", "Microsoft" })]
    [InlineData("Newtonsoft.Json", "13.0.1", new[] { "dotnetfoundation", "jamesnk", "newtonsoft" })]
    [InlineData("Octokit.GraphQL", "0.1.9-beta", new[] { "GitHub", "grokys", "jcansdale", "nickfloyd", "StanleyGoldman" })]
    [InlineData("Octokit.Webhooks.AspNetCore", "1.4.0", new[] { "GitHub", "kfcampbell" })]
    [InlineData("foo", "1.0.0", new string[0])]
    [InlineData("System.Text.Json", "malformed", new string[0])]
    public async Task Can_Get_Package_Owners(string id, string version, string[] expected)
    {
        // Arrange
        var repository = new RepositoryId("some-org", "some-repo");

        var options = await new HttpClientInterceptorOptions()
            .ThrowsOnMissingRegistration()
            .RegisterNuGetBundleAsync(TestContext.Current.CancellationToken);

        using var client = options.CreateHttpClient();
        client.BaseAddress = new Uri("https://api.nuget.org");

        using var cache = new ApplicationCache();

        var target = new NuGetPackageRegistry(client, cache, outputHelper.ToLogger<NuGetPackageRegistry>());

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
