using System.Text.Json;

namespace New7z.App;

internal sealed record SharedTask(string Id,string Title,string Destination,string Status,double Percent,string CurrentItem,DateTime UpdatedUtc);

internal static class SharedTaskStore
{
    private static readonly object WriteLock=new();
    private static Task pendingWrite=Task.CompletedTask;
    private static string Folder=>Path.Combine(UserSettings.Folder,"Tasks");
    public static void Save(SharedTask task)
    {
        lock(WriteLock)pendingWrite=pendingWrite.ContinueWith(_=>Write(task),CancellationToken.None,TaskContinuationOptions.None,TaskScheduler.Default);
    }
    public static Task FlushAsync(){lock(WriteLock)return pendingWrite;}
    private static bool IsCompleted(SharedTask task)=>task.Status is "已完成" or "Completed";
    private static void Write(SharedTask task)
    {
        try
        {
            var path=Path.Combine(Folder,task.Id+".json");var temp=path+".tmp";
            // Completion is queued after progress writes so an older snapshot cannot survive it.
            if(IsCompleted(task)){File.Delete(path);File.Delete(temp);return;}
            Directory.CreateDirectory(Folder);
            File.WriteAllText(temp,JsonSerializer.Serialize(task,AppJsonContext.Default.SharedTask));File.Move(temp,path,true);
        }
        catch{}
    }
    public static IReadOnlyList<SharedTask> ReadAll()
    {
        try
        {
            if(!Directory.Exists(Folder))return [];
            var cutoff=DateTime.UtcNow.AddDays(-7);var result=new List<SharedTask>();
            foreach(var path in Directory.EnumerateFiles(Folder,"*.json"))
            {
                try
                {
                    var task=JsonSerializer.Deserialize(File.ReadAllText(path),AppJsonContext.Default.SharedTask);
                    if(task==null)continue;
                    if(IsCompleted(task)||task.UpdatedUtc<cutoff){File.Delete(path);continue;}
                    result.Add(task);
                }
                catch{}
            }
            return result.OrderByDescending(t=>t.UpdatedUtc).ToArray();
        }
        catch{return [];}
    }
}
