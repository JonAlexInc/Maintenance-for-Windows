using System.Collections.Concurrent;
namespace Maintenance.App;

// Each registration owns its cancellation source. Stop all only reaches helpers
// started by this application; it never enumerates or terminates unrelated processes.
static class AppOperations
{
    static readonly ConcurrentDictionary<Guid,CancellationTokenSource> active=new();
    internal static int Count=>active.Count;
    internal static Operation Begin(CancellationToken token=default)=>new(token);
    internal static void StopAll(){foreach(var source in active.Values)try{source.Cancel();}catch(ObjectDisposedException){}}
    internal sealed class Operation:IDisposable
    {
        readonly Guid id=Guid.NewGuid();readonly CancellationTokenSource source;
        internal CancellationToken Token=>source.Token;
        internal Operation(CancellationToken token){source=CancellationTokenSource.CreateLinkedTokenSource(token);active[id]=source;}
        public void Dispose(){active.TryRemove(id,out _);source.Dispose();}
    }
}

sealed partial class MainWindow
{
    readonly AppearanceButton emergencyStop=new(){Text="■  Stop all",Tag=CancelRole,Dock=DockStyle.Right,Width=158,Enabled=false,AccessibleName="Stop all app operations"};
    bool emergencyStopping;int stopGeneration;
    bool OperationsRunning=>busy||speedCancel!=null||storageBusy||inventoryBusy||startupWorking||restoreBusy||renderCancel!=null||AppOperations.Count>0;
    void StopAllOperations()
    {
        if(!OperationsRunning&&usageObserver==null)return;
        emergencyStopping=true;stopGeneration++;
        foreach(var source in new[]{cancel,speedCancel,storageRun,systemSearchCancel,renderCancel})try{source?.Cancel();}catch(ObjectDisposedException){}
        AppOperations.StopAll();
        if(usageObserver!=null)observeUsage.Checked=false;
        filterDelay.Stop();
        if(renderCancel==null&&pendingFilter){pendingFilter=false;files.Enabled=!busy;UpdateActions();}
        status.Text="Stop requested · Keeping partial results and waiting for current Windows operations to settle.";
        UpdateStatusStrip();
    }
}
