using System.Diagnostics;
using System.IO;

internal class ExecutableUtil
{
    public static string? FindFullPathFromPath(string command) => FindFullPathFromPath(command, Environment.GetEnvironmentVariable("PATH"), Path.PathSeparator, File.Exists);

    private static string? FindFullPathFromPath(string command, string? pathVariable, char pathSeparator, Func<string, bool> fileExists)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(command));

        string[] fmtStrings = OperatingSystem.IsWindows() ? ["{0}.exe", "{0}.cmd"] : ["{0}"];

        foreach (var dir in pathVariable?.Split(pathSeparator) ?? [])
        {
            foreach (var fmtString in fmtStrings)
            {
                var fullPath = Path.Combine(dir, string.Format(fmtString, command));

                if (fileExists(fullPath))
                {
                    return fullPath;
                }
            }
        }

        return null;
    }
}