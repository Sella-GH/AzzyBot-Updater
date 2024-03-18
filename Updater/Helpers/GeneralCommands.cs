using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Updater.Settings;

namespace Updater.Helpers;

internal static class GeneralCommands
{
    //
    // START OR STOP THE PROCCESS
    // ACCORDING TO THE SETTING
    //

    internal static void ConfigureProcess(bool start)
    {
        if (!CheckIfLinuxOs)
        {
            if (start)
            {
                string path = AppDomain.CurrentDomain.BaseDirectory.Replace("Updater/", "AzzyBot.exe", StringComparison.InvariantCultureIgnoreCase);
                if (!File.Exists(path))
                    path = AppDomain.CurrentDomain.BaseDirectory.Replace("Updater/", "AzzyBot-Dev.exe", StringComparison.InvariantCultureIgnoreCase);

                if (!File.Exists(path))
                    throw new IOException("AzzyBot does not exist!");

                Process.Start(path);
                return;
            }

            foreach (Process process in Process.GetProcessesByName(JsonSettings.ProcessName))
            {
                process.Kill();
                process.WaitForExit();
            }

            return;
        }

        // Use start if start is true
        // otherwise use stop
        string command = (start) ? "start" : "stop";

        ExceptionHandler.LogMessage($"{command} {JsonSettings.ProcessName}");

        using Process updater = new();
        updater.StartInfo = new()
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"sudo systemctl {command} {JsonSettings.ProcessName}\"",
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        updater.Exited += Process_Exited;
        updater.Start();
    }

    //
    // RESTART THE PROCCESS
    //

    internal static void RestartProcess()
    {
        ExceptionHandler.LogMessage($"restarting {JsonSettings.ProcessName}");

        if (!CheckIfLinuxOs)
        {
            ConfigureProcess(false);
            Task.Delay(1000);
            ConfigureProcess(true);
            return;
        }

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
            path = path.Replace("AzzyBot.dll", "AzzyBot-Dev.dll", StringComparison.CurrentCultureIgnoreCase);

            if (!File.Exists(path))
                throw new IOException("AzzyBot does not exist!");
        }

        return FileVersionInfo.GetVersionInfo(path).FileVersion ?? string.Empty;
    }

    //
    // SET FILE PERMISSIONS
    //

    internal static void SetFilePermission(string file)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameof(file));

        if (!CheckIfLinuxOs)
            return;

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

    //
    // CHECK IF LINUX OS
    //

    private static bool CheckIfLinuxOs => RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD);
}
