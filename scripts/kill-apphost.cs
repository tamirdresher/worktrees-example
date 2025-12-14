// kill-apphost.cs
// Usage:
//   dotnet run kill-apphost.cs -- <PID>
//   dotnet run kill-apphost.cs -- -Tree <PID>
//   dotnet run kill-apphost.cs -- -All
//   (Aliases: -T for -Tree)

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// ------------------------
// Argument Parsing
// ------------------------

bool tree = false;
bool all = false;
int pid = 0;

if (args.Length == 0)
{
    PrintError("Usage:");
    PrintError("  dotnet run kill-apphost.cs -- <PID>");
    PrintError("  dotnet run kill-apphost.cs -- -Tree <PID>");
    PrintError("  dotnet run kill-apphost.cs -- -All");
    return;
}

foreach (var arg in args)
{
    if (arg.Equals("-Tree", StringComparison.OrdinalIgnoreCase) ||
        arg.Equals("-T", StringComparison.OrdinalIgnoreCase))
    {
        tree = true;
        continue;
    }

    if (arg.Equals("-All", StringComparison.OrdinalIgnoreCase))
    {
        all = true;
        continue;
    }

    if (int.TryParse(arg, out int parsed))
    {
        pid = parsed;
        continue;
    }
}

if (all && pid > 0)
{
    PrintError("Error: -All cannot be combined with a PID.");
    return;
}

if (tree && pid <= 0)
{
    PrintError("Error: -Tree requires a valid PID.");
    return;
}

if (!all && pid <= 0)
{
    PrintError("Error: A valid PID must be provided unless -All is specified.");
    return;
}

// ------------------------
// Main
// ------------------------

WriteHeader("Aspire AppHost Killer");

var currentDir = Environment.CurrentDirectory;
var worktreeRoot = FindGitWorktreeRoot(currentDir);

if (all)
{
    if (worktreeRoot is null)
    {
        PrintError($"Refusing to bulk-kill (-All) without a detectable git worktree root (.git).");
        PrintError($"Run from inside your repo/worktree. Current directory: {currentDir}");
        return;
    }

    WriteInfo($"Worktree root: {worktreeRoot}");
    WriteInfo("Discovering AppHost processes in this worktree...");

    var targets = FindAppHostPidsInWorktree(worktreeRoot);

    if (targets.Count == 0)
    {
        WriteInfo("No matching AppHost processes found to kill.");
        return;
    }

    WriteInfo($"Killing {targets.Count} AppHost process(es) in this worktree (process tree)...");
    foreach (var t in targets)
    {
        WriteInfo($" - Killing PID {t} (tree) ...");
        KillProcessTree(t);
    }

    WriteSuccess("Done.");
    return;
}

// PID mode
if (!ProcessExists(pid))
{
    PrintWarning($"Process {pid} does not exist.");
    return;
}

if (tree)
{
    WriteInfo($"Killing process tree for PID {pid}...");
    KillProcessTree(pid);
}
else
{
    WriteInfo($"Stopping PID {pid}...");
    try
    {
        using var p = Process.GetProcessById(pid);
        p.Kill(entireProcessTree: false);
        p.WaitForExit(5000);
        WriteSuccess($"Process {pid} terminated.");
    }
    catch (Exception ex)
    {
        PrintError($"Error killing process: {ex.Message}");
    }
}

return;

// ------------------------
// Discovery: same logic as list-apphosts.cs
// ------------------------

static List<int> FindAppHostPidsInWorktree(string worktreeRoot)
{
    var pids = new List<int>();

    IReadOnlyList<ProcessSnapshot> snapshots;
    try
    {
        snapshots = ProcessInventory.ListAll(maxDegreeOfParallelism: Environment.ProcessorCount);
    }
    catch
    {
        return pids;
    }

    foreach (var s in snapshots)
    {
        if (!LooksLikeAspireAppHost(s))
            continue;

        if (!BelongsToWorktree(s, worktreeRoot))
            continue;

        pids.Add(s.Pid);
    }

    return pids.Distinct().OrderBy(x => x).ToList();
}

static bool LooksLikeAspireAppHost(ProcessSnapshot p)
{
    var exe = p.ExecutablePath ?? string.Empty;
    var cmd = p.CommandLine ?? string.Empty;

    bool exeLooksLikeAppHost =
        ContainsIgnoreCase(exe, ".apphost") &&
        !EndsWithFileName(exe, "dotnet.exe") &&
        !EndsWithFileName(exe, "dotnet");

    bool cmdLooksLikeDotnetHostedAppHost =
        ContainsIgnoreCase(cmd, ".apphost.dll") ||
        ContainsIgnoreCase(cmd, "apphost.csproj") ||
        (ContainsIgnoreCase(cmd, "--project") && ContainsIgnoreCase(cmd, ".apphost")) ||
        (ContainsIgnoreCase(cmd, "dotnet") && ContainsIgnoreCase(cmd, ".apphost") &&
            (ContainsIgnoreCase(cmd, ".dll") || ContainsIgnoreCase(cmd, ".csproj")));

    return exeLooksLikeAppHost || cmdLooksLikeDotnetHostedAppHost;
}

static bool BelongsToWorktree(ProcessSnapshot p, string worktreeRoot)
{
    if (!string.IsNullOrWhiteSpace(p.WorkingDirectory) && IsUnderRoot(worktreeRoot, p.WorkingDirectory!))
        return true;

    if (!string.IsNullOrWhiteSpace(p.ExecutablePath) && IsUnderRoot(worktreeRoot, p.ExecutablePath!))
        return true;

    if (!string.IsNullOrWhiteSpace(p.CommandLine) && ContainsPathFragment(p.CommandLine!, worktreeRoot))
        return true;

    return false;
}

static string? FindGitWorktreeRoot(string startDirectory)
{
    try
    {
        var dir = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (dir is not null)
        {
            var gitMarker = Path.Combine(dir.FullName, ".git");
            if (Directory.Exists(gitMarker) || File.Exists(gitMarker))
                return dir.FullName;

            dir = dir.Parent;
        }
    }
    catch { /* ignore */ }

    return null;
}

static bool IsUnderRoot(string root, string path)
{
    try
    {
        var rootFull = NormalizePath(root);
        var pathFull = NormalizePath(path);

        if (PathsEqual(rootFull, pathFull))
            return true;

        var rootWithSep = EnsureTrailingSeparator(rootFull);

        return StartsWithPath(pathFull, rootWithSep);
    }
    catch
    {
        return false;
    }
}

static string NormalizePath(string p)
{
    var full = Path.GetFullPath(p);
    full = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    return full;
}

static string EnsureTrailingSeparator(string p)
{
    return p.EndsWith(Path.DirectorySeparatorChar) ? p : p + Path.DirectorySeparatorChar;
}

static bool StartsWithPath(string value, string prefixWithSep)
{
    var cmp = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    return value.StartsWith(prefixWithSep, cmp);
}

static bool PathsEqual(string a, string b)
{
    var cmp = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    return string.Equals(a, b, cmp);
}

static bool ContainsIgnoreCase(string haystack, string needle)
{
    return haystack?.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
}

static bool EndsWithFileName(string path, string fileName)
{
    try
    {
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase);
    }
    catch
    {
        return false;
    }
}

static bool ContainsPathFragment(string commandLine, string worktreeRoot)
{
    var root = NormalizePath(worktreeRoot);
    var rootWin = root.Replace('/', '\\');
    var rootUnix = root.Replace('\\', '/');

    return ContainsIgnoreCase(commandLine, root) ||
           ContainsIgnoreCase(commandLine, rootWin) ||
           ContainsIgnoreCase(commandLine, rootUnix) ||
           ContainsIgnoreCase(commandLine, Quote(root)) ||
           ContainsIgnoreCase(commandLine, Quote(rootWin)) ||
           ContainsIgnoreCase(commandLine, Quote(rootUnix));
}

static string Quote(string s) => "\"" + s + "\"";

// ------------------------
// Kill Helpers
// ------------------------

static bool ProcessExists(int pid)
{
    try
    {
        Process.GetProcessById(pid);
        return true;
    }
    catch
    {
        return false;
    }
}

static void KillProcessTree(int pid)
{
    try
    {
        if (ProcessExists(pid))
        {
            using var p = Process.GetProcessById(pid);
            p.Kill(entireProcessTree: true);
            p.WaitForExit(5000);
            WriteSuccess($"Killed PID {pid} and its process tree");
        }
    }
    catch (Exception ex)
    {
        PrintError($"Failed to kill PID {pid}: {ex.Message}");
        if (ex.InnerException != null)
            PrintError($"Inner exception: {ex.InnerException.Message}");
    }
}

// ------------------------
// Console Coloring Helpers
// ------------------------

static void WriteHeader(string text)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("========================================");
    Console.WriteLine(text);
    Console.WriteLine("========================================");
    Console.WriteLine();
    Console.ResetColor();
}

static void WriteInfo(string text)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine(text);
    Console.ResetColor();
}

static void WriteSuccess(string text)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine(text);
    Console.ResetColor();
}

static void PrintError(string text)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine(text);
    Console.ResetColor();
}

static void PrintWarning(string text)
{
    Console.ForegroundColor = ConsoleColor.DarkYellow;
    Console.WriteLine(text);
    Console.ResetColor();
}

// ------------------------
// Cross-platform process inventory (Windows + Linux)
// ------------------------

public sealed record ProcessSnapshot(
    int Pid,
    string? Name,
    string? ExecutablePath,
    string? CommandLine,
    string? WorkingDirectory,
    string? Error);

public static class ProcessInventory
{
    public static IReadOnlyList<ProcessSnapshot> ListAll(int maxDegreeOfParallelism = 8, CancellationToken cancellationToken = default)
    {
        var procs = Process.GetProcesses();
        var bag = new ConcurrentBag<ProcessSnapshot>();

        var po = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, maxDegreeOfParallelism),
            CancellationToken = cancellationToken
        };

        Parallel.ForEach(procs, po, p =>
        {
            try
            {
                var pid = p.Id;

                string? name = null;
                try { name = p.ProcessName; } catch { /* ignore */ }

                var info = ProcessInfoReader.Get(pid);

                bag.Add(new ProcessSnapshot(
                    pid,
                    name,
                    info.ExecutablePath,
                    info.CommandLine,
                    info.WorkingDirectory,
                    null));
            }
            catch (Exception ex)
            {
                int pid = 0;
                string? name = null;
                try { pid = p.Id; } catch { /* ignore */ }
                try { name = p.ProcessName; } catch { /* ignore */ }

                bag.Add(new ProcessSnapshot(
                    pid,
                    name,
                    null,
                    null,
                    null,
                    ex.GetType().Name + ": " + ex.Message));
            }
            finally
            {
                try { p.Dispose(); } catch { /* ignore */ }
            }
        });

        return bag.OrderBy(x => x.Pid).ToList();
    }
}

public sealed record ProcessInfo(int Pid, string? CommandLine, string? WorkingDirectory, string? ExecutablePath);

public static class ProcessInfoReader
{
    public static ProcessInfo Get(int pid)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return GetLinux(pid);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return GetWindows(pid);

        return new ProcessInfo(pid, null, null, null);
    }

    private static ProcessInfo GetLinux(int pid)
    {
        string? cmd = null;
        string? cwd = null;
        string? exe = null;

        try
        {
            var cmdlinePath = $"/proc/{pid}/cmdline";
            if (File.Exists(cmdlinePath))
            {
                var bytes = File.ReadAllBytes(cmdlinePath);
                cmd = Encoding.UTF8.GetString(bytes).Replace('\0', ' ').Trim();
            }
        }
        catch { /* ignore */ }

        try
        {
            var cwdPath = $"/proc/{pid}/cwd";
            if (Directory.Exists(cwdPath))
            {
                var di = new DirectoryInfo(cwdPath);
                var target = di.ResolveLinkTarget(true);
                cwd = target?.FullName;
            }
        }
        catch { /* ignore */ }

        try
        {
            var exeLink = $"/proc/{pid}/exe";
            if (File.Exists(exeLink))
            {
                var fi = new FileInfo(exeLink);
                var target = fi.ResolveLinkTarget(true);
                exe = target?.FullName;
            }
        }
        catch { /* ignore */ }

        return new ProcessInfo(pid, cmd, cwd, exe);
    }

    private static ProcessInfo GetWindows(int pid)
    {
        string? exePath = TryGetWindowsExePath(pid);

        if (TryReadWindowsPebStrings(pid, out var cmd, out var cwd))
        {
            cwd ??= exePath is null ? null : Path.GetDirectoryName(exePath);
            return new ProcessInfo(pid, cmd, cwd, exePath);
        }

        return new ProcessInfo(pid, null, exePath is null ? null : Path.GetDirectoryName(exePath), exePath);
    }

    private static string? TryGetWindowsExePath(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            try { return p.MainModule?.FileName; } catch { /* ignore */ }

            using var h = OpenProcess(ProcessAccess.QueryLimitedInformation, false, pid);
            if (h.IsInvalid) return null;

            var sb = new StringBuilder(4096);
            int size = sb.Capacity;
            if (QueryFullProcessImageName(h, 0, sb, ref size))
                return sb.ToString();

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryReadWindowsPebStrings(int pid, out string? commandLine, out string? workingDirectory)
    {
        commandLine = null;
        workingDirectory = null;

        using var hProcess = OpenProcess(
            ProcessAccess.QueryInformation | ProcessAccess.VmRead | ProcessAccess.QueryLimitedInformation,
            false,
            pid);

        if (hProcess.IsInvalid)
            return false;

        bool is64Host = Environment.Is64BitProcess;

        IntPtr pebAddress;
        bool targetIs32Bit;

        if (is64Host)
        {
            if (NtQueryInformationProcess(hProcess, PROCESSINFOCLASS.ProcessWow64Information,
                    out IntPtr wow64Peb, IntPtr.Size, out _) == 0 && wow64Peb != IntPtr.Zero)
            {
                targetIs32Bit = true;
                pebAddress = wow64Peb;
            }
            else
            {
                targetIs32Bit = false;
                var pbi = new PROCESS_BASIC_INFORMATION();
                int status = NtQueryInformationProcess(hProcess, PROCESSINFOCLASS.ProcessBasicInformation,
                    ref pbi, Marshal.SizeOf<PROCESS_BASIC_INFORMATION>(), out _);
                if (status != 0) return false;
                pebAddress = pbi.PebBaseAddress;
            }
        }
        else
        {
            targetIs32Bit = true;
            var pbi = new PROCESS_BASIC_INFORMATION();
            int status = NtQueryInformationProcess(hProcess, PROCESSINFOCLASS.ProcessBasicInformation,
                ref pbi, Marshal.SizeOf<PROCESS_BASIC_INFORMATION>(), out _);
            if (status != 0) return false;
            pebAddress = pbi.PebBaseAddress;
        }

        IntPtr processParameters = targetIs32Bit
            ? ReadPtr32(hProcess, pebAddress + 0x10)
            : ReadPtr64(hProcess, pebAddress + 0x20);

        if (processParameters == IntPtr.Zero)
            return false;

        workingDirectory = ReadUnicodeString(hProcess, processParameters + (targetIs32Bit ? 0x24 : 0x38), targetIs32Bit);
        commandLine      = ReadUnicodeString(hProcess, processParameters + (targetIs32Bit ? 0x40 : 0x70), targetIs32Bit);

        return (workingDirectory != null || commandLine != null);
    }

    private static string? ReadUnicodeString(SafeProcessHandle hProcess, IntPtr address, bool targetIs32Bit)
    {
        ushort length = ReadUInt16(hProcess, address + 0);
        IntPtr buffer = targetIs32Bit
            ? ReadPtr32(hProcess, address + 4)
            : ReadPtr64(hProcess, address + 8);

        if (buffer == IntPtr.Zero || length == 0)
            return null;

        var bytes = new byte[length];
        if (!ReadProcessMemory(hProcess, buffer, bytes, bytes.Length, out _))
            return null;

        return Encoding.Unicode.GetString(bytes);
    }

    private static ushort ReadUInt16(SafeProcessHandle hProcess, IntPtr address)
    {
        Span<byte> buf = stackalloc byte[2];
        if (!ReadProcessMemory(hProcess, address, buf, buf.Length, out _))
            return 0;
        return (ushort)(buf[0] | (buf[1] << 8));
    }

    private static IntPtr ReadPtr64(SafeProcessHandle hProcess, IntPtr address)
    {
        Span<byte> buf = stackalloc byte[8];
        if (!ReadProcessMemory(hProcess, address, buf, buf.Length, out _))
            return IntPtr.Zero;

        long v = BitConverter.ToInt64(buf);
        return new IntPtr(v);
    }

    private static IntPtr ReadPtr32(SafeProcessHandle hProcess, IntPtr address)
    {
        Span<byte> buf = stackalloc byte[4];
        if (!ReadProcessMemory(hProcess, address, buf, buf.Length, out _))
            return IntPtr.Zero;

        int v = BitConverter.ToInt32(buf);
        return new IntPtr(v);
    }

    [Flags]
    private enum ProcessAccess : uint
    {
        QueryInformation = 0x0400,
        VmRead = 0x0010,
        QueryLimitedInformation = 0x1000
    }

    private enum PROCESSINFOCLASS : int
    {
        ProcessBasicInformation = 0,
        ProcessWow64Information = 26
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_BASIC_INFORMATION
    {
        public IntPtr Reserved1;
        public IntPtr PebBaseAddress;
        public IntPtr Reserved2_0;
        public IntPtr Reserved2_1;
        public IntPtr UniqueProcessId;
        public IntPtr Reserved3;
    }

    private sealed class SafeProcessHandle : SafeHandle
    {
        public SafeProcessHandle() : base(IntPtr.Zero, true) { }
        public override bool IsInvalid => handle == IntPtr.Zero || handle == new IntPtr(-1);
        protected override bool ReleaseHandle() => CloseHandle(handle);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(ProcessAccess desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(
        SafeProcessHandle hProcess,
        IntPtr lpBaseAddress,
        [Out] byte[] lpBuffer,
        int dwSize,
        out IntPtr lpNumberOfBytesRead);

    private static bool ReadProcessMemory(
        SafeProcessHandle hProcess,
        IntPtr lpBaseAddress,
        Span<byte> buffer,
        int dwSize,
        out IntPtr lpNumberOfBytesRead)
    {
        var tmp = new byte[dwSize];
        bool ok = ReadProcessMemory(hProcess, lpBaseAddress, tmp, dwSize, out lpNumberOfBytesRead);
        if (ok) tmp.AsSpan().CopyTo(buffer);
        return ok;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        SafeProcessHandle processHandle,
        PROCESSINFOCLASS processInformationClass,
        ref PROCESS_BASIC_INFORMATION processInformation,
        int processInformationLength,
        out int returnLength);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        SafeProcessHandle processHandle,
        PROCESSINFOCLASS processInformationClass,
        out IntPtr processInformation,
        int processInformationLength,
        out int returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool QueryFullProcessImageName(
        SafeProcessHandle hProcess,
        int dwFlags,
        [Out] StringBuilder lpExeName,
        ref int lpdwSize);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);
}
