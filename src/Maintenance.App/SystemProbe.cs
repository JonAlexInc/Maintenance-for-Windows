using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Maintenance.App;

sealed record SystemFact(string Section, string Component, string Property, string Value);
static class SystemProbe
{
    public static async Task<List<SystemFact>> Read(CancellationToken token)
    {
        using var operation=AppOperations.Begin(token);token=operation.Token;token.ThrowIfCancellationRequested();
        var script = Path.Combine(AppContext.BaseDirectory, "SystemInventory.ps1");
        using var p = new Process { StartInfo = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = System.Text.Encoding.UTF8 } };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script }) p.StartInfo.ArgumentList.Add(arg);
        p.Start();
        var output = p.StandardOutput.ReadToEndAsync(token); var error = p.StandardError.ReadToEndAsync(token);
        try { await p.WaitForExitAsync(token); }
        catch { if (!p.HasExited) p.Kill(true); throw; }
        var json = await output;
        if (p.ExitCode != 0) throw new IOException("Windows inventory could not be collected. " + await error);
        return JsonSerializer.Deserialize<List<SystemFact>>(json) ?? [];
    }
    [StructLayout(LayoutKind.Sequential)] public struct Memory
    {
        public uint Length, Load; public ulong Total, Available, PageTotal, PageAvailable, VirtualTotal, VirtualAvailable, Extended;
    }
    [DllImport("kernel32.dll")] public static extern bool GlobalMemoryStatusEx(ref Memory memory);
    [DllImport("kernel32.dll")] public static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
}
