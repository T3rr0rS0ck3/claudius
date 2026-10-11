using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Claudius.Shared;

namespace Claudius.Core;

/// <summary>Opens the assistant in a given folder: in a console window or in the desktop app.</summary>
public static class AssistantLauncher
{
    private const int MaxRecent = 10;
    /// <summary>
    /// Carries the start prompt to the console, already quoted as one argument. Expanded only after the console
    /// has parsed its command line (cmd's delayed expansion, PowerShell's <c>--%</c>), so file names with
    /// &amp;, % or ! in them reach the assistant unchanged.
    /// </summary>
    private const string PromptVariable = "CLAUDIUS_PROMPT";

    /// <summary>Full path of the CLI, or null if the assistant is not installed.</summary>
    public static string? FindAssistant() => FindAssistant(UserEnvironment());

    public static bool HasWindowsTerminal => FindWindowsTerminal(UserEnvironment()) != null;

    private static string? FindAssistant(Dictionary<string, string> env)
    {
        string native = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "bin", AssistantCli.Exe);
        return FindOnPath(env, AssistantCli.Exe) ?? FindOnPath(env, AssistantCli.Cmd) ?? (File.Exists(native) ? native : null);
    }

    /// <summary>
    /// Starts the assistant in <paramref name="folder"/> and remembers the folder as recent project.
    /// Throws <see cref="FileNotFoundException"/> if the assistant is missing. For the Desktop app it returns
    /// the short-lived <c>--desktop</c> process of the CLI, whose exit code tells whether the hand-over worked.
    /// A <paramref name="prompt"/> is sent as the first message; the Desktop app does not take one.
    /// </summary>
    public static Process? Launch(AppSettings settings, string folder, string? prompt = null)
    {
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException(folder);
        var env = UserEnvironment();
        string command = FindAssistant(env)
            ?? throw new FileNotFoundException(Strings.AssistantNotFound);

        var terminal = settings.Terminal;
        string? wt = FindWindowsTerminal(env);
        if (terminal == TerminalKind.Auto) terminal = wt != null ? TerminalKind.WindowsTerminal : TerminalKind.Cmd;
        if (terminal == TerminalKind.WindowsTerminal && (wt == null || folder.Contains(';'))) terminal = TerminalKind.Cmd;

        var start = terminal switch
        {
            // Hands the folder over to the Desktop app and exits; needs a recent CLI and Desktop installed.
            TerminalKind.Desktop => new ProcessStartInfo(command) { ArgumentList = { "--desktop" }, CreateNoWindow = true },
            // wt treats ";" as a command separator, so keep it out of the arguments.
            TerminalKind.WindowsTerminal => new ProcessStartInfo(wt!)
            {
                ArgumentList =
                {
                    "new-tab", "-d", folder, "--title", "Claudius – " + FolderName(folder).Replace(';', ','),
                    "cmd.exe", "/k", command,
                },
            },
            TerminalKind.PowerShell => new ProcessStartInfo("powershell.exe")
            {
                ArgumentList =
                {
                    "-NoExit", "-Command",
                    "& '" + command.Replace("'", "''") + "'" + (prompt != null ? $" --% %{PromptVariable}%" : ""),
                },
            },
            // The folder goes via WorkingDirectory, never into the command line: names may contain & or ^.
            _ => new ProcessStartInfo("cmd.exe")
            {
                Arguments = prompt != null ? $"/v:on /s /k \"\"{command}\" !{PromptVariable}!\"" : $"/k \"{command}\"",
            },
        };
        if (prompt != null && terminal == TerminalKind.WindowsTerminal)
        {
            start.ArgumentList.Insert(start.ArgumentList.IndexOf("/k"), "/v:on");
            start.ArgumentList.Add($"!{PromptVariable}!");
        }
        start.WorkingDirectory = folder;
        // No shell execute, so the environment can be replaced. The pet is a GUI app without a console,
        // so cmd/PowerShell still get a console window of their own.
        start.UseShellExecute = false;
        start.Environment.Clear();
        foreach (var (name, value) in env) start.Environment[name] = value;
        if (prompt != null) start.Environment[PromptVariable] = QuoteArgument(prompt);

        Process? process;
        try
        {
            process = Process.Start(start);
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException((terminal == TerminalKind.Desktop
                ? Strings.DesktopOpenFailed : Strings.ConsoleStartFailed) + ex.Message, ex);
        }

        Remember(settings, folder);
        if (terminal == TerminalKind.Desktop) return process;
        process?.Dispose();
        return null;
    }

    /// <summary>
    /// A prompt that hands <paramref name="paths"/> to the assistant as <c>@"…"</c> mentions,
    /// relative to <paramref name="folder"/> where they are inside it.
    /// </summary>
    public static string MentionPrompt(string folder, IEnumerable<string> paths) =>
        string.Join(" ", paths.Select(p => "@\"" + Path.GetRelativePath(folder, p) + "\""));

    /// <summary>Quotes <paramref name="arg"/> so that a program's command-line parser gets it back as one argument.</summary>
    private static string QuoteArgument(string arg)
    {
        var quoted = new System.Text.StringBuilder("\"");
        int backslashes = 0;
        foreach (char c in arg)
        {
            if (c == '\\') { backslashes++; continue; }
            // Backslashes count only right before a quote: double them there, and escape the quote.
            quoted.Append('\\', c == '"' ? backslashes * 2 + 1 : backslashes).Append(c);
            backslashes = 0;
        }
        return quoted.Append('\\', backslashes * 2).Append('"').ToString();
    }

    public static string FolderName(string folder)
    {
        string trimmed = folder.TrimEnd('\\', '/');
        string name = Path.GetFileName(trimmed);
        return name.Length > 0 ? name : trimmed;
    }

    private static void Remember(AppSettings settings, string folder)
    {
        string full = Path.GetFullPath(folder).TrimEnd('\\');
        settings.RecentProjects.RemoveAll(p => string.Equals(p.TrimEnd('\\'), full, StringComparison.OrdinalIgnoreCase));
        settings.RecentProjects.Insert(0, full);
        if (settings.RecentProjects.Count > MaxRecent)
            settings.RecentProjects.RemoveRange(MaxRecent, settings.RecentProjects.Count - MaxRecent);
    }

    private static string? FindWindowsTerminal(Dictionary<string, string> env)
    {
        // wt.exe is an app execution alias; File.Exists sees it in WindowsApps.
        string alias = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WindowsApps", "wt.exe");
        return FindOnPath(env, "wt.exe") ?? (File.Exists(alias) ? alias : null);
    }

    private static string? FindOnPath(Dictionary<string, string> env, string file)
    {
        foreach (string dir in env.GetValueOrDefault("Path", "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            try
            {
                string candidate = Path.Combine(dir.Trim('"'), file);
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException) { }
        }
        return null;
    }

    /// <summary>
    /// A fresh environment built from the user's and the system's stored variables, as Explorer gives
    /// to programs it starts. The pet's own environment is not passed on: if the pet was started from
    /// inside the assistant it carries that session's markers (it would think it is a child session)
    /// and e.g. NO_COLOR (the logo would lose its color). Also picks up PATH changes made since the pet started.
    /// </summary>
    private static Dictionary<string, string> UserEnvironment()
    {
        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        IntPtr token = IntPtr.Zero, block = IntPtr.Zero;
        try
        {
            if (OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY | TOKEN_DUPLICATE, out token)
                && CreateEnvironmentBlock(out block, token, false))
            {
                // "NAME=value\0NAME=value\0...\0\0" in UTF-16
                for (IntPtr p = block; ;)
                {
                    string? entry = Marshal.PtrToStringUni(p);
                    if (string.IsNullOrEmpty(entry)) break;
                    int eq = entry.IndexOf('=', 1);   // entries like "=C:=C:\" start with '='
                    if (eq > 0) env[entry[..eq]] = entry[(eq + 1)..];
                    p += (entry.Length + 1) * 2;
                }
                return env;
            }
            Log.Write("Benutzerumgebung nicht verfügbar, verwende die eigene: " + Marshal.GetLastWin32Error());
        }
        finally
        {
            if (block != IntPtr.Zero) DestroyEnvironmentBlock(block);
            if (token != IntPtr.Zero) CloseHandle(token);
        }

        foreach (System.Collections.DictionaryEntry e in Environment.GetEnvironmentVariables())
            env[(string)e.Key] = (string?)e.Value ?? "";
        return env;
    }

    private const uint TOKEN_DUPLICATE = 0x0002;
    private const uint TOKEN_QUERY = 0x0008;

    [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool CreateEnvironmentBlock(out IntPtr block, IntPtr token, bool inherit);
    [DllImport("userenv.dll")] private static extern bool DestroyEnvironmentBlock(IntPtr block);
}
