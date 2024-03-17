using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace Updater.Settings;

internal static class JsonSettings
{
    internal static string ApiUrl { get; private set; } = string.Empty;
    internal static string ArtifactName { get; private set; } = string.Empty;
    internal static string BranchName { get; private set; } = string.Empty;
    internal static string PathToApp { get; private set; } = AppContext.BaseDirectory;
    internal static string Permissions { get; private set; } = string.Empty;
    internal static string ProcessName { get; private set; } = string.Empty;
    internal static bool SettingsSet { get; private set; }

    internal static void Load()
    {
        string dllName = Assembly.GetExecutingAssembly().Location;
        if (string.IsNullOrWhiteSpace(dllName))
            throw new InvalidOperationException("DLL PATH CAN'T BE FOUND!");

        string? pathName = Path.GetDirectoryName(dllName);
        if (string.IsNullOrWhiteSpace(pathName))
            throw new InvalidOperationException("DLL PATH CAN'T BE EXTRACTED!");

        IConfigurationBuilder builder = new ConfigurationBuilder()
            .SetBasePath(pathName)
            .AddJsonFile("appsettings.json", true, false);

        IConfiguration config = builder.Build();

        ApiUrl = config["Updater:ApiUrl"] ?? string.Empty;
        ArtifactName = config["Updater:ArtifactName"] ?? string.Empty;
        BranchName = config["Updater:BranchName"] ?? string.Empty;
        PathToApp = config["Updater:PathToApp"] ?? string.Empty;
        Permissions = config["Updater:Permissions"] ?? string.Empty;
        ProcessName = config["Updater:ProcessName"] ?? string.Empty;

        List<string> excluded = [nameof(ArtifactName), nameof(BranchName), nameof(SettingsSet)];
        List<string> failed = CheckSettings(excluded);

        if (failed.Count == 0)
            return;

        foreach (string item in failed)
        {
            Console.Error.WriteLine($"{item} has to be filled out!");
        }

        Environment.Exit(1);
    }

    private static List<string> CheckSettings(List<string> excluded)
    {
        // Get all Properties of this class
        Type type = typeof(JsonSettings);
        PropertyInfo[] properties = type.GetProperties(BindingFlags.NonPublic | BindingFlags.Static);

        List<string> failed = [];

        // Loop through all properties and check if they are null or whitespace
        // If yes add them to the list
        foreach (PropertyInfo property in properties)
        {
            object? value = property.GetValue(null);

            if (excluded.Contains(property.Name))
                continue;

            if (string.IsNullOrWhiteSpace(value as string))
                failed.Add(property.Name);
        }

        if (!string.IsNullOrWhiteSpace(ArtifactName) && !string.IsNullOrWhiteSpace(BranchName))
            SettingsSet = true;

        return failed;
    }
}
