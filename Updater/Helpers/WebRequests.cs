using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Updater.Settings;

namespace Updater.Helpers;

internal static class WebRequests
{
    private static readonly HttpClient Client = new()
    {
        DefaultRequestVersion = new(2, 0),
        DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
    };

    private static string CommitDate = string.Empty;
    private static string CommitSha = string.Empty;

    //
    // GET THE LATEST COMMIT
    // FROM GITHUB API
    //

    internal static async Task<string> GetLatestCommitAsync()
    {
        ExceptionHandler.LogMessage("Getting latest GitHub commit");

        AddHeadersToClient(Client);

        string url = string.Join("/", JsonSettings.ApiUrl, "commits", JsonSettings.BranchName);
        HttpResponseMessage? reponse = await Client.GetAsync(new Uri(url)).ConfigureAwait(true);
        reponse.EnsureSuccessStatusCode();

        string content = await reponse.Content.ReadAsStringAsync().ConfigureAwait(true);
        reponse.Dispose();

        using JsonDocument doc = JsonDocument.Parse(content);
        JsonElement commit = doc.RootElement;

        // Check if commit is already the newest
        CommitSha = doc.RootElement.GetProperty("sha").GetString() ?? string.Empty;
        if (await CheckIfAppIsUpToDateAsync(CommitSha).ConfigureAwait(true))
        {
            ExceptionHandler.LogMessage("Bot is already up-to-date");
            Environment.Exit(0);
        }
        else if (string.IsNullOrWhiteSpace(CommitSha))
        {
            ExceptionHandler.LogMessage("No commit found!");
            Environment.Exit(1);
        }

        ExceptionHandler.LogMessage($"Latest commit: {CommitSha} found!");

        // Save the date
        CommitDate = commit.GetProperty("commit").GetProperty("committer").GetProperty("date").GetDateTime().ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        return CommitSha;
    }

    internal static async Task<Version> GetLatestVersionAsync()
    {
        ExceptionHandler.LogMessage("Getting latest GitHub version");

        AddHeadersToClient(Client);

        string url = string.Join("/", JsonSettings.ApiUrl, "tags");
        HttpResponseMessage? reponse = await Client.GetAsync(new Uri(url)).ConfigureAwait(true);
        reponse.EnsureSuccessStatusCode();

        string content = await reponse.Content.ReadAsStringAsync().ConfigureAwait(true);
        reponse.Dispose();

        using JsonDocument doc = JsonDocument.Parse(content);

        // Check if commit is already the newest
        Version onlineVersion = new(doc.RootElement[0].GetProperty("name").GetString() ?? "0.0.0");
        ExceptionHandler.LogMessage($"Online version is {onlineVersion}");

        return onlineVersion;
    }

    //
    // GET THE RIGHT ARTIFACT
    // URL FROM THE GITHUB API
    //

    internal static async Task<string> GetArtifactUrlAsync(string sha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha, nameof(sha));

        ExceptionHandler.LogMessage("Getting the artifact url");

        AddHeadersToClient(Client);

        string url = string.Join("/", JsonSettings.ApiUrl, "actions", "artifacts");
        HttpResponseMessage? response = await Client.GetAsync(new Uri(url)).ConfigureAwait(true);
        response.EnsureSuccessStatusCode();

        string content = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
        response.Dispose();

        using JsonDocument doc = JsonDocument.Parse(content);
        foreach (JsonElement artifact in doc.RootElement.GetProperty("artifacts").EnumerateArray())
        {
            if (artifact.GetProperty("workflow_run").GetProperty("head_sha").GetString() == sha && artifact.GetProperty("name").GetString() == JsonSettings.ArtifactName)
            {
                ExceptionHandler.LogMessage("Artifact url found");
                return artifact.GetProperty("archive_download_url").GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    //
    // DOWNLOAD AND EXTRACT
    // THE GITHUB ARTIFACT
    //

    internal static async Task DownloadAndExtractArtifactsAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            ExceptionHandler.LogMessage("There is no artifact attached to this commit!");
            return;
        }

        if (!Directory.Exists(JsonSettings.PathToApp))
            Directory.CreateDirectory(JsonSettings.PathToApp);

        // Stop the application
        GeneralCommands.ConfigureProcess(false);

        // Delete all files except three important ones
        string[] existingFiles = Directory.GetFiles(JsonSettings.PathToApp);
        if (existingFiles.Length > 1)
        {
            foreach (string filePath in existingFiles)
            {
                string fileName = Path.GetFileName(filePath);
                if (fileName is not ".env" and not "appsettings.json" and not "appsettings.development.json")
                    File.Delete(filePath);
            }
        }

        ExceptionHandler.LogMessage("Old files deleted");

        // Add URL headers
        AddHeadersToClient(Client);

        // Get the path for the zip
        string tempZipPath = Path.Combine(JsonSettings.PathToApp, "zip.zip");

        // Build a new HttpClient to retrieve the file
        using HttpClient httpClient = new()
        {
            DefaultRequestVersion = new(1, 1),
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
        };

        // Add URL headers again
        AddHeadersToClient(httpClient);
        ExceptionHandler.LogMessage("Downloading files now");

        using HttpResponseMessage response = await httpClient.GetAsync(new Uri(url)).ConfigureAwait(true);

        // Download the file to the location
        await using (Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(true))
        {
            await using FileStream fs = new(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(fs).ConfigureAwait(true);
            ExceptionHandler.LogMessage("Zip file downloaded");
        }

        // Extract the zip file
        using ZipArchive archive = ZipFile.OpenRead(tempZipPath);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string fullPath = Path.GetFullPath(Path.Combine(JsonSettings.PathToApp, entry.FullName));

            if (!fullPath.StartsWith(JsonSettings.PathToApp, StringComparison.Ordinal))
                throw new InvalidOperationException("Extracting path isn't correct!");

            // If it's a directory, create it
            if (string.IsNullOrEmpty(entry.Name))
            {
                if (!Directory.Exists(fullPath))
                    Directory.CreateDirectory(fullPath);
            }
            else
            {
                string? directoryPath = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(directoryPath))
                    Directory.CreateDirectory(directoryPath!);

                if (File.Exists(fullPath))
                    continue;

                entry.ExtractToFile(fullPath);

                // Set correct file permissions
                GeneralCommands.SetFilePermission(fullPath);
            }
        }

        ExceptionHandler.LogMessage("Zip file extracted");

        // Delete the zip file and start the process again
        File.Delete(tempZipPath);

        // Write the commit name to a file
        await WriteCommitToFileAsync(CommitSha, "Commit.txt").ConfigureAwait(true);
        ExceptionHandler.LogMessage("Commit written to file");

        // Write the commit date to a file
        await WriteCommitToFileAsync(CommitDate, "BuildDate.txt").ConfigureAwait(true);
        ExceptionHandler.LogMessage("CommitDate written to file");

        ExceptionHandler.LogMessage($"AzzyBot updated to commit {CommitSha} created at {CommitDate}!");

        GeneralCommands.ConfigureProcess(true);
    }

    private static void AddHeadersToClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient, nameof(httpClient));

        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        httpClient.DefaultRequestHeaders.Add("User-Agent", "AzzyBot Updater");
    }

    private static async Task<bool> CheckIfAppIsUpToDateAsync(string commitSha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commitSha, nameof(commitSha));

        string currentCommitSha = await ReadCommitFromFileAsync().ConfigureAwait(true);
        return currentCommitSha == commitSha;
    }

    private static async Task<string> ReadCommitFromFileAsync()
    {
        try
        {
            return await File.ReadAllTextAsync(Path.Combine(JsonSettings.PathToApp, "Commit.txt")).ConfigureAwait(true);
        }
        catch (FileNotFoundException ex)
        {
            ExceptionHandler.LogMessage($"{ex.Message} Creating new one...");
        }

        return string.Empty;
    }

    private static async Task WriteCommitToFileAsync(string text, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text, nameof(text));
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        string path = Path.Combine(JsonSettings.PathToApp, name);

        await File.WriteAllTextAsync(path, text).ConfigureAwait(true);
        GeneralCommands.SetFilePermission(path);
    }
}
