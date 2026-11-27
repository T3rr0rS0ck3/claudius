using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Claudius.Shared;

/// <summary>
/// Saves files so that a crash or a shutdown in the middle of saving cannot leave them empty: the text goes to a
/// temp file that is flushed to disk first, then swapped in, and the previous version stays as <c>.bak</c>.
/// </summary>
public static class SafeFile
{
    private static readonly UTF8Encoding Utf8 = new(false);

    public static string BackupPath(string path) => path + ".bak";

    public static string BrokenPath(string path) => path + ".broken";

    public static void WriteAllText(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + "." + Environment.ProcessId + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(Utf8.GetBytes(text));
            stream.Flush(flushToDisk: true);
        }
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                // An empty file is not worth keeping as the backup.
                if (File.Exists(path) && new FileInfo(path).Length > 0)
                    File.Replace(temp, path, BackupPath(path), ignoreMetadataErrors: true);
                else
                    File.Move(temp, path, overwrite: true);
                return;
            }
            catch (Exception ex) when (attempt < 5 && ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(20); // someone is reading the file right now
            }
            catch
            {
                try { File.Delete(temp); } catch { }
                throw;
            }
        }
    }

    /// <summary>
    /// Reads and parses the file, or its backup when the file is missing, empty or broken; null when neither
    /// works. A broken file is moved to <c>.broken</c>, so the next save does not turn it into the backup.
    /// </summary>
    public static T? Read<T>(string path, Func<string, T?> parse, Action<string>? log = null) where T : class
    {
        T? result = null;
        bool broken = false;
        foreach (string file in new[] { path, BackupPath(path) })
        {
            try
            {
                if (!File.Exists(file)) continue;
                result = parse(File.ReadAllText(file, Encoding.UTF8)) ?? throw new JsonException("The file holds null.");
                break;
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                log?.Invoke(Path.GetFileName(file) + " konnte nicht gelesen werden: " + ex.Message);
                if (file == path && ex is JsonException) broken = true;
            }
        }
        if (broken)
        {
            try { File.Move(path, BrokenPath(path), overwrite: true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return result;
    }
}
