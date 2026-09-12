namespace SyncTool.Core;

/// <summary>Runs shell-dialog work on a dedicated STA thread so the WinForms UI loop remains responsive.</summary>
public static class StaTask
{
    public static Task<T> RunAsync<T>(Func<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { completion.SetResult(operation()); }
            catch (Exception ex) { completion.SetException(ex); }
        }) { IsBackground = true, Name = "SyncTool.FolderPicker" };
        if (OperatingSystem.IsWindows()) thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
