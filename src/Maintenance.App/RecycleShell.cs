using System.Runtime.InteropServices;
namespace Maintenance.App;
static class RecycleShell
{
    [ComImport,Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItem
    {
        void BindToHandler(IntPtr context,in Guid handler,in Guid iid,out IntPtr result);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint format,out IntPtr name);
        void GetAttributes(uint mask,out uint attributes);
        void Compare(IShellItem other,uint hint,out int order);
    }
    [ComImport,Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IFileOperation
    {
        void Advise(IntPtr sink,out uint cookie);void Unadvise(uint cookie);void SetOperationFlags(uint flags);
        void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)]string message);void SetProgressDialog(IntPtr dialog);void SetProperties(IntPtr changes);void SetOwnerWindow(IntPtr window);
        void ApplyPropertiesToItem(IntPtr item);void ApplyPropertiesToItems(IntPtr items);
        void RenameItem(IntPtr item,IntPtr name,IntPtr sink);void RenameItems(IntPtr items,IntPtr name);
        void MoveItem(IntPtr item,IntPtr folder,IntPtr name,IntPtr sink);void MoveItems(IntPtr items,IntPtr folder);
        void CopyItem(IntPtr item,IntPtr folder,IntPtr name,IntPtr sink);void CopyItems(IntPtr items,IntPtr folder);
        void DeleteItem(IShellItem item,IntPtr sink);void DeleteItems(IntPtr items);
        void NewItem(IntPtr folder,uint attributes,IntPtr name,IntPtr template,IntPtr sink);
        void PerformOperations();void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)]out bool aborted);
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode,PreserveSig=false)]
    static extern void SHCreateItemFromParsingName(string path,IntPtr context,in Guid iid,out IShellItem item);
    public static void Move(string path)
    {
        var operation=(IFileOperation)Activator.CreateInstance(Type.GetTypeFromCLSID(new("3AD05575-8857-4850-9277-11B85BDB8E09"))!)!;
        IShellItem? item=null;
        try {
            var iid=new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");SHCreateItemFromParsingName(path,IntPtr.Zero,in iid,out item);
            // Windows 8+ recycle-on-delete, stop on errors, no connected HTML folders.
            operation.SetOperationFlags(0x80000|0x100000|0x400|0x4|0x10|0x2000);
            operation.DeleteItem(item,IntPtr.Zero);operation.PerformOperations();operation.GetAnyOperationsAborted(out bool aborted);
            if(aborted||File.Exists(path))throw new IOException("Windows could not recycle this file; no permanent-delete fallback requested");
        }finally{if(item!=null)Marshal.ReleaseComObject(item);Marshal.ReleaseComObject(operation);}
    }
    public static Task<T> OnSta<T>(Func<T> work)
    {
        var completion=new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>{try{completion.SetResult(work());}catch(Exception e){completion.SetException(e);}}){IsBackground=true};
        thread.SetApartmentState(ApartmentState.STA);thread.Start();return completion.Task;
    }
}
