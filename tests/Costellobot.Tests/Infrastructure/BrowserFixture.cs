﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.Playwright;

namespace MartinCostello.Costellobot.Infrastructure;

public class BrowserFixture(
    BrowserFixtureOptions options,
    ITestOutputHelper outputHelper)
{
    private const string VideosDirectory = "videos";
    private static readonly string AssetsDirectory = Path.Join("..", "..", "..");

    public BrowserFixture(ITestOutputHelper outputHelper)
        : this(new(), outputHelper)
    {
    }

    internal static bool IsRunningInGitHubActions { get; } = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

    private BrowserFixtureOptions Options { get; } = options;

    public async Task WithPageAsync(
        Func<IPage, Task> action,
        [CallerMemberName] string? testName = null)
    {
        string activeTestName = Options.TestName ?? testName!;

        using var playwright = await Playwright.CreateAsync();

        await using var browser = await CreateBrowserAsync(playwright);

        var options = CreateContextOptions();
        await using var context = await browser.NewContextAsync(options);

        if (Options.CaptureTrace)
        {
            await context.Tracing.StartAsync(new()
            {
                Screenshots = true,
                Snapshots = true,
                Sources = true,
                Title = activeTestName,
            });
        }

        var page = await context.NewPageAsync();

        page.Console += (_, e) => outputHelper.WriteLine(e.Text);
        page.PageError += (_, e) => outputHelper.WriteLine(e);

        try
        {
            await action(page);
        }
        catch (Exception)
        {
            await TryCaptureScreenshotAsync(page, activeTestName);
            throw;
        }
        finally
        {
            if (Options.CaptureTrace)
            {
                string traceName = GenerateFileName(activeTestName, ".zip");
                string path = Path.Join(AssetsDirectory, "traces", traceName);

                await context.Tracing.StopAsync(new() { Path = path });
            }

            await TryCaptureVideoAsync(page, activeTestName);
        }
    }

    protected virtual BrowserNewContextOptions CreateContextOptions()
    {
        var options = new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            Locale = "en-GB",
            TimezoneId = "Europe/London",
        };

        if (Options.CaptureVideo)
        {
            options.RecordVideoDir = VideosDirectory;
        }

        return options;
    }

    private async Task<IBrowser> CreateBrowserAsync(IPlaywright playwright)
    {
        var options = new BrowserTypeLaunchOptions()
        {
            Channel = Options.BrowserChannel,
        };

        // Slow down actions and make the DevTools visible by default
        // to make it easier to debug the app when debugging locally.
        if (System.Diagnostics.Debugger.IsAttached)
        {
#pragma warning disable CS0612
            options.Devtools = true;
#pragma warning restore CS0612
            options.Headless = false;
            options.SlowMo = 250;
        }

        var browserType = playwright[Options.BrowserType];

        return await browserType.LaunchAsync(options);
    }

    private string GenerateFileName(string testName, string extension)
    {
        string browserType = Options.BrowserType;

        if (!string.IsNullOrEmpty(Options.BrowserChannel))
        {
            browserType += "_" + Options.BrowserChannel;
        }

        string os =
            OperatingSystem.IsLinux() ? "linux" :
            OperatingSystem.IsMacOS() ? "macos" :
            OperatingSystem.IsWindows() ? "windows" :
            "other";

        // Remove characters that are disallowed in file names
        browserType = browserType.Replace(':', '_');

        string utcNow = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture);
        return $"{testName}_{browserType}_{os}_{utcNow}{extension}";
    }

    private async Task TryCaptureScreenshotAsync(
        IPage page,
        string testName)
    {
        try
        {
            string fileName = GenerateFileName(testName, ".png");
            string path = Path.Join(AssetsDirectory, "screenshots", fileName);

            await page.ScreenshotAsync(new() { Path = path });

            outputHelper.WriteLine($"Screenshot saved to {path}.");
        }
        catch (Exception ex)
        {
            outputHelper.WriteLine("Failed to capture screenshot: " + ex);
        }
    }

    private async Task TryCaptureVideoAsync(IPage page, string testName)
    {
        if (!Options.CaptureVideo || page.Video is null)
        {
            return;
        }

        try
        {
            string fileName = GenerateFileName(testName, ".webm");
            string path = Path.Join(AssetsDirectory, VideosDirectory, fileName);

            await page.CloseAsync();
            await page.Video.SaveAsAsync(path);

            outputHelper.WriteLine($"Video saved to {path}.");
        }
        catch (Exception ex)
        {
            outputHelper.WriteLine("Failed to capture video: " + ex);
        }
    }
}
