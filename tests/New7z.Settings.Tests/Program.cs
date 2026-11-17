using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json.Serialization;
using New7z.App;
using New7z.Core;

var root=Path.Combine(Path.GetTempPath(),"New7z-settings-tests-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);var checks=0;
void Assert(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
try
{
    var a=UserSettings.Load(root);var b=UserSettings.Load(root);
    Assert(a.UpdateCheckIntervalMinutes==1440,"Default interval is daily");
    a.Theme="dark";a.Save();b.Language="en";b.Save();
    Assert(UserSettings.Load(root) is {Theme:"dark",Language:"en"},"Stale windows merge independent settings");
    a=UserSettings.Load(root);b=UserSettings.Load(root);a.FormatIcons["zip"]="zip.png";a.Save();b.FormatIcons["rar"]="rar.png";b.Save();
    Assert(UserSettings.Load(root).FormatIcons.Count==2,"Per-format changes merge by key");
    a=UserSettings.Load(root);b=UserSettings.Load(root);a.FormatIcons.Remove("zip");a.Save();b.FormatIcons["7z"]="7z.png";b.Save();
    Assert(!UserSettings.Load(root).FormatIcons.ContainsKey("zip")&&UserSettings.Load(root).FormatIcons.ContainsKey("7z"),"Restoring one format preserves another window's update");
    a=UserSettings.Load(root);b=UserSettings.Load(root);a.Recent.Insert(0,"a.zip");a.Save();b.Recent.Insert(0,"b.zip");b.Save();
    Assert(UserSettings.Load(root).Recent.SequenceEqual(new[]{"b.zip","a.zip"}),"Recent additions merge without losing another window's file");
    var writers=Enumerable.Range(0,20).Select(_=>UserSettings.Load(root)).ToArray();
    await Task.WhenAll(writers.Select((settings,index)=>Task.Run(()=>{settings.FormatIcons["parallel-"+index]="icon.png";settings.Save();})));
    Assert(UserSettings.Load(root).FormatIcons.Keys.Count(k=>k.StartsWith("parallel-"))==20,"Concurrent stale writes preserve all keys");
    Assert(!Directory.EnumerateFiles(root,"*.tmp").Any(),"Atomic saves leave no temporary files");
    a=UserSettings.Load(root);a.UpdateCheckIntervalMinutes=60;a.Save();
    var now=new DateTime(2026,11,17,1,0,0,DateTimeKind.Utc);
    Assert(a.TryReserveUpdateCheck(now,false),"First automatic check is due");
    Assert(!UserSettings.Load(root).TryReserveUpdateCheck(now,false),"Another window does not duplicate the automatic check");
    Assert(!a.TryReserveUpdateCheck(now.AddMinutes(59),false),"No early automatic check");
    Assert(a.TryReserveUpdateCheck(now.AddHours(1),false),"Check is due at the interval boundary");
    Assert(a.TryReserveUpdateCheck(now.AddHours(1).AddMinutes(1),true),"Manual check bypasses the interval");
    a.AutoCheckUpdates=false;a.Save();
    Assert(!a.TryReserveUpdateCheck(now.AddDays(2),false),"Disabled automatic checks stay disabled");
    Assert(a.TryReserveUpdateCheck(now.AddDays(2),true),"Manual check works while automatic checks are disabled");
    a.AutoCheckUpdates=true;a.Save();Assert(a.TryReserveUpdateCheck(now,false),"Clock rollback does not suppress checks forever");
    var old=Path.Combine(root,"migration");Directory.CreateDirectory(old);await File.WriteAllTextAsync(Path.Combine(old,"settings.json"),"{\"Theme\":\"dark\"}");
    Assert(UserSettings.Load(old) is {Theme:"dark",UpdateCheckIntervalMinutes:1440},"Existing settings migrate to the daily interval");
    var activity=Path.Combine(root,"activity.lock");
    using(var first=ArchiveActivity.BeginTask(activity))
    using(var second=ArchiveActivity.BeginTask(activity))Assert(ArchiveActivity.TryBeginInstall(activity)==null,"Installation cannot interrupt concurrent tasks");
    using(var install=ArchiveActivity.TryBeginInstall(activity))
    {
        Assert(install!=null,"Installation can start when all tasks finish");
        var blocked=false;try{using var task=ArchiveActivity.BeginTask(activity);}catch(IOException){blocked=true;}
        Assert(blocked,"New tasks cannot start while installation holds the lease");
    }
    using(var task=ArchiveActivity.BeginTask(activity))Assert(true,"Tasks resume after the install lease closes");
    var tracker=new TransferTracker();tracker.Update(new(100,0),TimeSpan.Zero);tracker.Update(new(100,50),TimeSpan.FromSeconds(10));
    Assert(tracker.BytesPerSecond==5&&tracker.Remaining==TimeSpan.FromSeconds(10),"Byte speed and remaining time use processing time");
    tracker.Update(new(10,2,null,false),TimeSpan.FromSeconds(11));Assert(tracker.BytesPerSecond==0&&tracker.Remaining==null,"File counts are not shown as bytes per second");
    await CheckTimeoutFallback();
    Console.WriteLine($"PASS settings, update schedule and activity checks: {checks}");
}
finally{if(Directory.Exists(root))Directory.Delete(root,true);}

async Task CheckTimeoutFallback()
{
    var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
    var previousProxy=HttpClient.DefaultProxy;HttpClient.DefaultProxy=new WebProxy("http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port);
    var client=(HttpClient)typeof(UpdateChecker).GetField("Client",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
    client.Timeout=TimeSpan.FromMilliseconds(500);
    using var stop=new CancellationTokenSource();var requests=new List<string>();var connections=new List<TcpClient>();
    var server=Task.Run(async()=>
    {
        try{while(!stop.IsCancellationRequested){var socket=await listener.AcceptTcpClientAsync(stop.Token);connections.Add(socket);using var reader=new StreamReader(socket.GetStream(),leaveOpen:true);var target=await reader.ReadLineAsync(stop.Token);lock(requests)requests.Add(target??"");}}
        catch(OperationCanceledException){}
        catch(SocketException) when(stop.IsCancellationRequested){}
    });
    try
    {
        var failed=false;try{await UpdateChecker.CheckAsync(new Version(0,0));}catch(IOException){failed=true;}
        lock(requests)Assert(failed&&requests.Count==3&&requests.Any(r=>r.Contains("gitee.com")),"Timeout falls back through GitHub feed to Gitee");
        lock(requests)requests.Clear();using var cancel=new CancellationTokenSource(50);
        var cancelled=false;try{await UpdateChecker.CheckAsync(new Version(0,0),cancel.Token);}catch(OperationCanceledException){cancelled=true;}
        lock(requests)Assert(cancelled&&requests.Count<=1,"User cancellation does not start mirror requests");
    }
    finally{stop.Cancel();listener.Stop();await server;foreach(var socket in connections)socket.Dispose();HttpClient.DefaultProxy=previousProxy;}
}

namespace New7z.App
{
    [JsonSerializable(typeof(UserSettings))]
    internal partial class AppJsonContext:JsonSerializerContext;
}
