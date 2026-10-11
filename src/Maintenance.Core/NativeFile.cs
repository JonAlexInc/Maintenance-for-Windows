using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Maintenance.Core;

// Keep directory handles open without write/delete sharing while resolving a path.
// Delete the inspected file by handle, never by a second path lookup.
public static class NativeFile
{
    const uint ReadAttributes = 0x80, Delete = 0x10000, Backup = 0x02000000, OpenReparse = 0x00200000;
    [StructLayout(LayoutKind.Sequential)]
    public struct Info
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Created, Accessed, Written;
        public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
        public readonly long Size => ((long)SizeHigh << 32) | SizeLow;
        public readonly long Creation => Time(Created);
        public readonly long Write => Time(Written);
        static long Time(System.Runtime.InteropServices.ComTypes.FILETIME t) => ((long)t.dwHighDateTime << 32) | (uint)t.dwLowDateTime;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct Basic { public long Creation, Access, Write, Change; public uint Attributes; }
    [StructLayout(LayoutKind.Sequential)]
    struct Disposition { public byte Delete; }
    [StructLayout(LayoutKind.Sequential)]
    struct AttributeTag { public uint Attributes, Tag; }
    [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "GetFileInformationByHandleEx")]
    static extern bool GetAttributeTag(SafeFileHandle h, int kind, out AttributeTag info, uint size);
    public static bool NeedsRecall(FileAttributes attributes) => ((uint)attributes & (0x1000 | 0x40000 | 0x400000)) != 0;
    public static bool CloudTag(uint tag) => (tag & 0xFFFF0FFF) == 0x9000001A;
    public static bool ResidentCloud(SafeFileHandle h)
    {
        if (!GetAttributeTag(h, 9, out var info, 8)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return CloudTag(info.Tag) && !NeedsRecall((FileAttributes)info.Attributes);
    }
    public static bool ResidentCloud(string path) { using var h = Open(path); return ResidentCloud(h); }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint mode, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetFileInformationByHandle(SafeFileHandle h, out Info info);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetFileInformationByHandleEx(SafeFileHandle h, int kind, out Basic info, uint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetFileInformationByHandle(SafeFileHandle h, int kind, ref Disposition info, uint size);

    public static SafeFileHandle Open(string path, bool delete = false)
    {
        var h = CreateFileW(path, ReadAttributes | (delete ? Delete : 0), delete ? 0u : 1u,
            IntPtr.Zero, 3, Backup | OpenReparse, IntPtr.Zero);
        if (h.IsInvalid) { h.Dispose(); throw new Win32Exception(Marshal.GetLastWin32Error()); }
        return h;
    }
    public static SafeFileHandle OpenData(string path)
    {
        var h = CreateFileW(path, 0x80000000 | ReadAttributes, 1, IntPtr.Zero, 3, OpenReparse | 0x08000000, IntPtr.Zero);
        if (h.IsInvalid) { int error = Marshal.GetLastWin32Error(); h.Dispose(); throw new Win32Exception(error); }
        return h;
    }
    public static Info Read(SafeFileHandle h)
    {
        if (!GetFileInformationByHandle(h, out var i)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return i;
    }
    public static long Change(SafeFileHandle h)
    {
        if (!GetFileInformationByHandleEx(h, 0, out var b, (uint)Marshal.SizeOf<Basic>())) throw new Win32Exception(Marshal.GetLastWin32Error());
        return b.Change;
    }
    public static void DeleteOpened(SafeFileHandle h)
    {
        var d = new Disposition { Delete = 1 };
        if (!SetFileInformationByHandle(h, 4, ref d, 1)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    public static DirectoryGuard Guard(string directory, bool allowResidentCloud = false) => new(directory, allowResidentCloud);
    public sealed class DirectoryGuard : IDisposable
    {
        readonly List<SafeFileHandle> handles = [];
        readonly bool allowResidentCloud;
        public DirectoryGuard(string directory, bool allowResidentCloud = false)
        {
            this.allowResidentCloud = allowResidentCloud;
            try
            {
                var full = Path.GetFullPath(directory);
                if (full.StartsWith(@"\\") || full.Length < 3 || full[1] != ':') throw new IOException("Only local drive paths are supported.");
                var root = Path.GetPathRoot(full)!;
                var current = root;
                Add(current);
                foreach (var part in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
                { current = Path.Combine(current, part); Add(current); }
            }
            catch { Dispose(); throw; }
        }
        void Add(string path)
        {
            var h = Open(path); handles.Add(h);
            var a = (FileAttributes)Read(h).Attributes;
            if (!a.HasFlag(FileAttributes.Directory) || (allowResidentCloud && NeedsRecall(a)) ||
                (a.HasFlag(FileAttributes.ReparsePoint) && !(allowResidentCloud && ResidentCloud(h))))
                throw new IOException("Linked or redirected folders are not supported.");
        }
        public void Dispose() { for (var i = handles.Count - 1; i >= 0; i--) handles[i].Dispose(); handles.Clear(); }
    }
}
