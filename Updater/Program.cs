using System;
using System.IO;
using System.Threading.Tasks;
using Updater.Helpers;
using Updater.Settings;

namespace Updater;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        ExceptionHandler.LogMessage("Updater started");

        AppDomain.CurrentDomain.UnhandledException += GlobalExceptionHandler;

        JsonSettings.Load();
        ExceptionHandler.LogMessage("Settings loaded");

        if (args.Length == 0 && JsonSettings.SettingsSet)
        {
            ExceptionHandler.LogMessage("No arguments found, Updater will be executed");
            await WebRequests.DownloadAndExtractArtifactsAsync(await WebRequests.GetArtifactUrlAsync(await WebRequests.GetLatestCommitAsync().ConfigureAwait(true)).ConfigureAwait(true)).ConfigureAwait(true);
            return;
        }

        foreach (string arg in args)
        {
            ExceptionHandler.LogMessage($"{arg} - argument found!");
            switch (arg)
            {
                case "check":
                    await CheckForUpdatesAsync().ConfigureAwait(true);
                    break;

                case "restart":
                    RestartBot();
                    break;
            }
        }

        ExceptionHandler.LogMessage("No more found, exit now...");
    }

    private static async Task CheckForUpdatesAsync()
    {
        Version localVersion = new(GeneralCommands.GetFileVersion());
        Version onlineVersion = await WebRequests.GetLatestVersionAsync().ConfigureAwait(true);

        if (onlineVersion > localVersion)
        {
            ExceptionHandler.LogMessage("There's an update available!");
            await File.WriteAllTextAsync(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "version.txt"), onlineVersion.ToString()).ConfigureAwait(true);
            Environment.Exit(100);
        }

        Environment.Exit(0);
    }

    private static void RestartBot() => GeneralCommands.RestartProcess();
    private static void GlobalExceptionHandler(object sender, UnhandledExceptionEventArgs e) => ExceptionHandler.LogError((Exception)e.ExceptionObject);
}
