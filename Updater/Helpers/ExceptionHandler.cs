using System;

namespace Updater.Helpers;

internal static class ExceptionHandler
{
    internal static void LogError(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex, nameof(ex));

        Console.Error.WriteLine(ex.ToString());
    }

    internal static void LogMessage(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message, nameof(message));

        Console.Out.WriteLine(message);
    }
}
