// list-apphosts.cs
// Usage:
//   dotnet run list-apphosts.cs

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// ------------------------
// Main Script Logic
// ------------------------

WriteHeader("Running Aspire AppHost Instances");

var currentDir = Environment.CurrentDirectory;
var worktreeRoot = FindGitWorktreeRoot(currentDir);

if (worktreeRoot is null)
{
    WriteWarning($"Could not locate a .git marker by walking up from: {currentDir}");
    WriteWarning("Will still list AppHost-like processes, but filtering by repo/worktree root is disabled.");
}
else
{
    WriteInfo($"Worktree root: {worktreeRoot}");
}

var apphosts = FindAppHostProcesses(worktreeRoot);

if (!apphosts.Any())
{
    WriteInfo("No AppHost instances found.");
    Console.WriteLine();
    WriteInfo("To start an AppHost:");
    Console.WriteLine("  PS> .\\scripts\\start-apphost.ps1");
    Console.WriteLine();
    return;
}

// ------------------------
// Display Results
// ------------------------

Console.WriteLine();
Console.WriteLine($"{"PID",-10} {"Status",-12} {"Memory (MB)",-15} {"Started",-25}");
Console.WriteLine(new string('-', 70));

foreach (var info in apphosts)
{
    Console.WriteLine($"{info.Pid,-10} {info.Status,-12} {info.MemoryMB,-15:N2} {info.Started,-25}");

    // Extra details (useful for disambiguation)
    if (!string.IsNullOrWhiteSpace(info.WorkingDirectory))
        Console.WriteLine($"  CWD: {info.WorkingDirectory}");

    if (!string.IsNullOrWhiteSpace(info.CommandLine))
    {
        var cmd = info.CommandLine!.Length > 220 ? info.CommandLine.Substring(0, 220) + " ..." : info.CommandLine;
        Console.WriteLine($"  CMD: {cmd}");
    }

    if (!string.IsNullOrWhiteSpace(info.Error))
        Console.WriteLine($"  WARN: {info.Error}");

    Console.WriteLine();
}

WriteInfo("Commands:");
Console.WriteLine("  Stop:       .\\scripts\\kill-apphost.ps1 -PID <ProcessId>");
Console.WriteLine("  Stop all:   .\\scripts\\kill-apphost.ps1 -PID <ProcessId> -Force");
Console.WriteLine();

return;

// ------------------------
// Helper Functions
// ------------------------

static void WriteHeader(string text)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("========================================");
    Console.WriteLine(text);
    Console.WriteLine("========================================");
    Console.ResetColor();
}

static void WriteInfo(string text)
{
    Console.ForegroundColor = ConsoleColor.Gray;
    Console.WriteLine(text);
    Console.ResetColor();
}

static void WriteWarning(string text)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine(text);
    Console.ResetColor();
}

static void PrintError(string text)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine(text);
    Console.ResetColor();
}

static List<AppHostInfo> FindAppHostProcesses(string? worktreeRoot)
{
    var results = new List<AppHostInfo>();

    try
    {
        // Use the cross-platform inventory (Windows PEB + Linux /proc) from earlier.
        var snapshots = ProcessInventory.ListAll(maxDegreeOfParallelism: Environment.ProcessorCount);

        foreach (var s in snapshots)
        {
            try
            {
                // Must look like an Aspire AppHost (either direct exe OR dotnet-hosted).
                if (!LooksLikeAspireAppHost(s))
                    continue;

                // Must be from the same git worktree root, if we can determine it.
                if (worktreeRoot is not null && !BelongsToWorktree(s, worktreeRoot))
                    continue;

                // Pull memory/start time best-effort via System.Diagnostics.Process.
                double memMb = 0;
                string started = string.Empty;

                try
                {
                    using var p = Process.GetProcessById(s.Pid);
                    memMb = p.WorkingSet64 / (1024.0 * 1024.0);

                    try { started = p.StartTime.ToString("yyyy-MM-dd HH:mm:ss"); }
                    catch { started = string.Empty; }
                }
                catch
                {
                    // Process may have exited or access denied; still keep the record.
                }

                results.Add(new AppHostInfo
                {
                    Pid = s.Pid,
                    Status = "Running",
                    MemoryMB = memMb,
                    Started = string.IsNullOrWhiteSpace(started) ? "<unknown>" : started,
                    WorkingDirectory = s.WorkingDirectory,
                    CommandLine = s.CommandLine,
                    Error = s.Error
                });
            }
            catch (Exception ex)
            {
                WriteWarning($"Could not process PID {s.Pid}: {ex.Message}");
            }
        }
    }
    catch (Exception ex)
    {
        PrintError($"Error querying processes: {ex.Message}");
    }

    return results.OrderBy(x => x.Pid).ToList();
}

static bool LooksLikeAspireAppHost(ProcessSnapshot p)
{
    // Heuristics:
    // 1) Direct execution: executable path contains ".AppHost" and is not dotnet
    // 2) dotnet-hosted: command line references AppHost.dll or AppHost.csproj or --project ...AppHost...
    // 3) Also allow "Aspire.AppHost" naming variants by matching ".AppHost" token.
    var exe = p.ExecutablePath ?? string.Empty;
    var cmd = p.CommandLine ?? string.Empty;
Console.WriteLine($"DEBUG: PID {p.Pid}, EXE='{exe}', CMD='{cmd}'");
    bool exeLooksLikeAppHost =
        (ContainsIgnoreCase(exe, ".apphost")) &&
        !EndsWithFileName(exe, "dotnet.exe") &&
        !EndsWithFileName(exe, "dotnet");

    bool cmdLooksLikeDotnetHostedAppHost =
        ContainsIgnoreCase(cmd, ".apphost.dll") ||
        ContainsIgnoreCase(cmd, "apphost.csproj") ||
        (ContainsIgnoreCase(cmd, "--project") && ContainsIgnoreCase(cmd, ".apphost")) ||
        (ContainsIgnoreCase(cmd, "dotnet") && ContainsIgnoreCase(cmd, ".apphost") && (ContainsIgnoreCase(cmd, ".dll") || ContainsIgnoreCase(cmd, ".csproj")));

    return exeLooksLikeAppHost || cmdLooksLikeDotnetHostedAppHost;
}

static bool BelongsToWorktree(ProcessSnapshot p, string worktreeRoot)
{
    // Prefer working directory (most accurate), then executable path, then command line path checks.
    if (!string.IsNullOrWhiteSpace(p.WorkingDirectory) && IsUnderRoot(worktreeRoot, p.WorkingDirectory!))
        return true;

    if (!string.IsNullOrWhiteSpace(p.ExecutablePath) && IsUnderRoot(worktreeRoot, p.ExecutablePath!))
        return true;

    // Best-effort scan: if the command line contains the worktree root path string, treat it as belonging.
    // (This catches dotnet exec <fullpath>\*.AppHost.dll and dotnet run --project <fullpath>\*.AppHost.csproj)
    if (!string.IsNullOrWhiteSpace(p.CommandLine) && ContainsPathFragment(p.CommandLine!, worktreeRoot))
        return true;

    return false;
}

static string? FindGitWorktreeRoot(string startDirectory)
{
    // For both normal repos and worktrees:
    // - In a normal repo: ".git" is a directory at the root.
    // - In a worktree: ".git" is typically a *file* at the root that points to the common gitdir.
    // Either way, the "worktree root" is where the ".git" marker lives.
    try
    {
        var dir = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (dir is not null)
        {
            var gitDir = Path.Combine(dir.FullName, ".git");
            if (Directory.Exists(gitDir) || File.Exists(gitDir))
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
    // Normalize only the root; command line may contain mixed slashes/quotes.
    // We check a few variants.
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
// Data Classes
// ------------------------

class AppHostInfo
{
    public int Pid { get; set; }
    public string Status { get; set; } = string.Empty;
    public double MemoryMB { get; set; }
    public string Started { get; set; } = string.Empty;

    public string? WorkingDirectory { get; set; }
    public string? CommandLine { get; set; }
    public string? Error { get; set; }
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

        return bag
            .OrderBy(x => x.Pid)
            .ToList();
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

    // ---------------- Linux ----------------

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
            // It's a symlink to a directory; Directory.Exists is the right check.
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

    // ---------------- Windows ----------------

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
            ? ReadPtr32(hProcess, pebAddress + 0x10) // x86 PEB.ProcessParameters
            : ReadPtr64(hProcess, pebAddress + 0x20); // x64 PEB.ProcessParameters

        if (processParameters == IntPtr.Zero)
            return false;

        // RTL_USER_PROCESS_PARAMETERS offsets:
        // CurrentDirectory (CURDIR.DosPath UNICODE_STRING): x64 0x38, x86 0x24
        // CommandLine (UNICODE_STRING):                 x64 0x70, x86 0x40
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

    // -------- Windows interop --------

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
