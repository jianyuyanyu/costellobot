﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.Builders;
using MartinCostello.Costellobot.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Octokit;

namespace MartinCostello.Costellobot;

[Collection<AppCollection>]
public sealed class AuthenticationTests(AppFixture fixture, ITestOutputHelper outputHelper) : IntegrationTests<AppFixture>(fixture, outputHelper)
{
    [Fact]
    public async Task Can_Create_Authenticated_GitHub_Client_For_App()
    {
        // Arrange
        await Fixture.ClearCacheAsync();

        var appId = GitHubFixtures.AppId;
        var target = Fixture.Services.GetRequiredKeyedService<IGitHubClientForApp>(appId);

        // Act
        var actual = await target.Connection.CredentialStore.GetCredentials();

        // Assert
        actual.ShouldNotBeNull();
        actual.AuthenticationType.ShouldBe(AuthenticationType.Bearer);

        string jwt1 = actual.GetToken();

        var handler = new JsonWebTokenHandler();
        var token1 = handler.ReadJsonWebToken(jwt1);

        token1.ShouldNotBeNull();
        token1.Alg.ShouldBe("RS256");
        token1.Issuer.ShouldBe(appId);

        var tolerance = TimeSpan.FromSeconds(20);
        var utcNow = DateTime.UtcNow;

        token1.IssuedAt.ShouldBe(utcNow.AddMinutes(-1), tolerance);
        token1.ValidFrom.ShouldBe(utcNow, tolerance);
        token1.ValidTo.ShouldBe(utcNow.AddMinutes(10), tolerance);

        // Arrange
        await Task.Delay(TimeSpan.FromSeconds(1.1), CancellationToken);

        await Fixture.ClearCacheAsync();

        utcNow = DateTime.UtcNow;

        // Act
        actual = await target.Connection.CredentialStore.GetCredentials();

        // Assert
        actual.ShouldNotBeNull();
        actual.AuthenticationType.ShouldBe(AuthenticationType.Bearer);

        string jwt2 = actual.GetToken();

        jwt2.ShouldNotBe(jwt1);

        var token2 = handler.ReadJsonWebToken(jwt2);

        token2.ShouldNotBeNull();
        token2.Alg.ShouldBe("RS256");
        token2.Issuer.ShouldBe(appId);

        token2.IssuedAt.ShouldBe(utcNow.AddMinutes(-1), tolerance);
        token2.ValidFrom.ShouldBe(utcNow, tolerance);
        token2.ValidTo.ShouldBe(utcNow.AddMinutes(10), tolerance);

        token2.IssuedAt.ShouldBeGreaterThan(token1.IssuedAt);
        token2.ValidFrom.ShouldBeGreaterThan(token1.ValidFrom);
        token2.ValidTo.ShouldBeGreaterThan(token1.ValidTo);
    }

    [Fact]
    public async Task Can_Create_Authenticated_GitHub_Client_For_Installation()
    {
        // Arrange
        await Fixture.ClearCacheAsync();

        var accessToken = GitHubFixtures.CreateAccessToken();
        var installationId = GitHubFixtures.InstallationId;

        RegisterGetAccessToken(accessToken);

        var target = Fixture.Services.GetRequiredKeyedService<IGitHubClientForInstallation>(installationId);

        // Act
        var actual = await target.Connection.CredentialStore.GetCredentials();

        // Assert
        actual.ShouldNotBeNull();
        actual.AuthenticationType.ShouldBe(AuthenticationType.Oauth);

        string token1 = actual.GetToken();

        token1.ShouldBe(accessToken.Token);

        // Arrange
        accessToken.Token = Guid.NewGuid().ToString();

        await Fixture.ClearCacheAsync();

        // Act
        actual = await target.Connection.CredentialStore.GetCredentials();

        // Assert
        actual.ShouldNotBeNull();
        actual.AuthenticationType.ShouldBe(AuthenticationType.Oauth);

        string token2 = actual.GetToken();

        token2.ShouldNotBe(token1);
    }
}
