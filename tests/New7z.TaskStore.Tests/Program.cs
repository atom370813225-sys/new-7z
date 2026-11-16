using System.Text.Json;
using System.Text.Json.Serialization;
using New7z.App;

var root=UserSettings.Folder;
var folder=Path.Combine(root,"Tasks");
var checks=0;
void Assert(bool condition,string name){if(!condition)throw new Exception(name);checks++;}
SharedTask Snapshot(string id,string status,double percent=0)=>new(id,"test archive","test destination",status,percent,"",DateTime.UtcNow);
void Seed(SharedTask task)=>File.WriteAllText(Path.Combine(folder,task.Id+".json"),JsonSerializer.Serialize(task,AppJsonContext.Default.SharedTask));
try
{
    SharedTaskStore.Save(Snapshot("success","Running"));
    await SharedTaskStore.FlushAsync();
    Assert(SharedTaskStore.ReadAll().Any(t=>t.Id=="success"),"Running task must remain visible");
    for(var i=0;i<100;i++)SharedTaskStore.Save(Snapshot("success","Running",i));
    SharedTaskStore.Save(Snapshot("success","Completed",100));
    await SharedTaskStore.FlushAsync();
    Assert(!File.Exists(Path.Combine(folder,"success.json")),"Completion must delete all queued progress snapshots");
    Assert(!File.Exists(Path.Combine(folder,"success.json.tmp")),"Completion must not leave a temporary snapshot");
    SharedTaskStore.Save(Snapshot("zh-success","正在压缩",100));
    SharedTaskStore.Save(Snapshot("zh-success","已完成",100));
    SharedTaskStore.Save(Snapshot("failed","Failed: verification",100));
    SharedTaskStore.Save(Snapshot("cancelled","Cancelled"));
    SharedTaskStore.Save(Snapshot("at-100","Running",100));
    await SharedTaskStore.FlushAsync();
    Assert(!File.Exists(Path.Combine(folder,"zh-success.json")),"Chinese completion must be deleted");
    Seed(Snapshot("legacy-zh","已完成",100));Seed(Snapshot("legacy-en","Completed",100));
    Seed(Snapshot("expired","Running") with{UpdatedUtc=DateTime.UtcNow.AddDays(-8)});
    File.WriteAllText(Path.Combine(folder,"corrupt.json"),"invalid json");
    var tasks=SharedTaskStore.ReadAll();
    Assert(tasks.Select(t=>t.Id).ToHashSet().SetEquals(["failed","cancelled","at-100"]),"Only completed/expired tasks must be removed; 100% alone is not completion");
    Assert(!File.Exists(Path.Combine(folder,"legacy-zh.json"))&&!File.Exists(Path.Combine(folder,"legacy-en.json")),"Legacy completed records must be deleted from disk");
    Console.WriteLine($"PASS task store checks: {checks}");
}
finally{await SharedTaskStore.FlushAsync();if(Directory.Exists(root))Directory.Delete(root,true);}

namespace New7z.App
{
    internal static class UserSettings
    {
        public static string Folder {get;}=Path.Combine(Path.GetTempPath(),"New7z-task-store-tests-"+Guid.NewGuid().ToString("N"));
    }
    [JsonSerializable(typeof(SharedTask))]
    internal partial class AppJsonContext:JsonSerializerContext;
}
