namespace New7z.Core;

// Shared readers represent work; an exclusive writer represents installation.
// File handles are released by Windows even if a process terminates unexpectedly.
public static class ArchiveActivity
{
    public static string LockPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"New7z","activity.lock");
    public static IDisposable BeginTask()=>BeginTask(LockPath);
    internal static IDisposable BeginTask(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try{return new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.ReadWrite);}
        catch(IOException ex){throw new IOException(Loc.T("New7z 正在安装更新，请稍后重试任务。","New7z is installing an update. Try the task again afterwards."),ex);}
    }
    public static IDisposable? TryBeginInstall()=>TryBeginInstall(LockPath);
    internal static IDisposable? TryBeginInstall(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try{return new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
        catch(IOException){return null;}
    }
}
