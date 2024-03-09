using System;
using System.Diagnostics;
using System.IO;
using Updater.Settings;

namespace Updater.Helpers;

internal static class LinuxCommands
{
    //
    // START OR STOP THE PROCCESS
    // ACCORDING TO THE SETTING
    //

    internal static void ConfigureProcess(bool start)
    {
        // Use start if start is true
        // otherwise use stop
        string command = (start) ? "start" : "stop";

        ExceptionHandler.LogMessage($"{command} {JsonSettings.ProcessName}");

        using Process process = new();
        process.StartInfo = new()
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"sudo systemctl {command} {JsonSettings.ProcessName}\"",
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        process.Exited += Process_Exited;
        process.Start();
    }

    //
    // RESTART THE PROCCESS
    //

    internal static void RestartProcess()
    {
        ExceptionHandler.LogMessage($"restarting {JsonSettings.ProcessName}");

        using Process process = new();
        process.StartInfo = new()
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"sudo systemctl restart {JsonSettings.ProcessName}\"",
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        process.Exited += Process_Exited;
        process.Start();
    }

    //
    // GET FILE VERSION
    //

    internal static string GetFileVersion()
    {
        string path = AppDomain.CurrentDomain.BaseDirectory.Replace("Updater/", "AzzyBot.dll", StringComparison.InvariantCultureIgnoreCase);

        if (!File.Exists(path))
        {
            if (File.Exists(path.Replace("AzzyBot.dll", "AzzyBot-Dev.dll", StringComparison.InvariantCultureIgnoreCase)))
            {
                path = path.Replace("AzzyBot.dll", "AzzyBot-Dev.dll", StringComparison.CurrentCultureIgnoreCase);
            }
            else
            {
                throw new IOException("AzzyBot-Dev.dll does not exist!");
            }
        }

        return FileVersionInfo.GetVersionInfo(path).FileVersion ?? string.Empty;
    }

    //
    // SET FILE PERMISSIONS
    //

    internal static void SetFilePermission(string file)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameof(file));

        using Process process = new();
        process.StartInfo = new()
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"chmod {JsonSettings.Permissions} '{file}'\"",
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        process.Exited += Process_Exited;
        process.Start();
    }

    private static void Process_Exited(object? sender, EventArgs e) => ExceptionHandler.LogMessage("Process finished");
}
