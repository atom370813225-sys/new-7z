using System.Text.Json;
using System.Text.Json.Nodes;
using New7z.Core;

namespace New7z.App;

public sealed class UserSettings
{
    public string Theme {get;set;}="system";
    public string Language {get;set;}="system";
    public string? IconPath {get;set;}
    public string FormatIconTheme {get;set;}="default";
    public string DefaultFormat {get;set;}="7z";
    public string Default7zMethod {get;set;}="LZMA2";
    public string DefaultZipMethod {get;set;}="Deflate";
    public bool UseDefaultExtractFolder {get;set;}
    public string DefaultExtractFolder {get;set;}="";
    public bool ExtractIntoSubfolder {get;set;}=true;
    public bool CloseAfterExtraction {get;set;}
    public Dictionary<string,string> FormatIcons {get;set;}=[];
    public bool AutoCheckUpdates {get;set;}=true;
    public int UpdateCheckIntervalMinutes {get;set;}=1440;
    public DateTime LastUpdateCheckUtc {get;set;}
    public List<string> Recent {get;set;}=[];
    public static string Folder=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"New7z");
    public static readonly int[] UpdateIntervals=[15,60,360,1440,10080];
    private string? storageFolder,baseline;
    private string StorageFolder=>storageFolder??Folder;
    public static string NormalizeFormat(string? value)=>value?.ToLowerInvariant() switch{"zip"=>"zip","tar"=>"tar","zst"=>"zst","lz4"=>"lz4",_=>"7z"};
    // A configured folder must be a rooted path; anything else falls back to the archive's own folder.
    public static string NormalizeExtractFolder(string? value)
    {
        var folder=(value??"").Trim().Trim('"');
        if(folder.Length==0)return "";
        try
        {
            if(!Path.IsPathFullyQualified(folder))return "";
            var full=Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            return full.EndsWith(':')?full+Path.DirectorySeparatorChar:full;
        }
        catch{return "";}
    }

    public static UserSettings Load()=>Load(Folder);
    internal static UserSettings Load(string folder)
    {
        UserSettings value;
        try{value=JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(folder,"settings.json")),AppJsonContext.Default.UserSettings)??new();}
        catch{value=new();}
        value.storageFolder=folder;value.FormatIcons??=[];value.Recent??=[];
        value.FormatIconTheme=value.FormatIconTheme=="animals"?"animals":"default";
        value.DefaultFormat=NormalizeFormat(value.DefaultFormat);
        value.Default7zMethod=CompressionMethods.Normalize("7z",value.Default7zMethod)??CompressionMethods.Default("7z");
        value.DefaultZipMethod=CompressionMethods.Normalize("zip",value.DefaultZipMethod)??CompressionMethods.Default("zip");
        value.DefaultExtractFolder=NormalizeExtractFolder(value.DefaultExtractFolder);
        if(value.DefaultExtractFolder.Length==0)value.UseDefaultExtractFolder=false;
        if(!UpdateIntervals.Contains(value.UpdateCheckIntervalMinutes))value.UpdateCheckIntervalMinutes=1440;
        value.baseline=Serialize(value);return value;
    }
    private static string Serialize(UserSettings value)=>JsonSerializer.Serialize(value,AppJsonContext.Default.UserSettings);
    private static JsonObject Document(UserSettings value)=>JsonNode.Parse(Serialize(value))!.AsObject();

    // OS file-sharing locks coordinate all app processes and are released after a crash.
    private static FileStream Lock(string folder)
    {
        Directory.CreateDirectory(folder);var deadline=Environment.TickCount64+5000;
        while(true)
        {
            try{return new FileStream(Path.Combine(folder,"settings.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
            catch(IOException) when(Environment.TickCount64<deadline){Thread.Sleep(20);}
        }
    }
    public void Save()
    {
        using var gate=Lock(StorageFolder);
        var current=Load(StorageFolder);var latest=Document(current);var local=Document(this);
        var before=JsonNode.Parse(baseline??Serialize(new UserSettings()))!.AsObject();
        foreach(var property in local)
        {
            if(property.Key is nameof(FormatIcons) or nameof(Recent))continue;
            if(!JsonNode.DeepEquals(property.Value,before[property.Key]))latest[property.Key]=property.Value?.DeepClone();
        }
        var icons=latest[nameof(FormatIcons)]!.AsObject();var oldIcons=before[nameof(FormatIcons)]!.AsObject();var newIcons=local[nameof(FormatIcons)]!.AsObject();
        foreach(var key in oldIcons.Select(p=>p.Key).Concat(newIcons.Select(p=>p.Key)).Distinct(StringComparer.Ordinal))
            if(!JsonNode.DeepEquals(oldIcons[key],newIcons[key]))
            {
                if(newIcons.ContainsKey(key))icons[key]=newIcons[key]?.DeepClone();else icons.Remove(key);
            }
        if(!JsonNode.DeepEquals(local[nameof(Recent)],before[nameof(Recent)]))
        {
            var oldRecent=before[nameof(Recent)]!.AsArray().Select(n=>n!.GetValue<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var removed=oldRecent.Except(Recent,StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var merged=Recent.Concat(current.Recent.Where(p=>!oldRecent.Contains(p))).Where(p=>!removed.Contains(p)).Distinct(StringComparer.OrdinalIgnoreCase).Take(20);
            latest[nameof(Recent)]=new JsonArray(merged.Select(p=>(JsonNode?)JsonValue.Create(p)).ToArray());
        }
        Write(StorageFolder,latest);
        Adopt(JsonSerializer.Deserialize(latest.ToJsonString(),AppJsonContext.Default.UserSettings)!);
    }
    private static void Write(string folder,JsonObject document)
    {
        var target=Path.Combine(folder,"settings.json");var temporary=target+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllText(temporary,document.ToJsonString());File.Move(temporary,target,true);}
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    private void Adopt(UserSettings value)
    {
        Theme=value.Theme;Language=value.Language;IconPath=value.IconPath;FormatIconTheme=value.FormatIconTheme;
        DefaultFormat=value.DefaultFormat;Default7zMethod=value.Default7zMethod;DefaultZipMethod=value.DefaultZipMethod;
        UseDefaultExtractFolder=value.UseDefaultExtractFolder;DefaultExtractFolder=value.DefaultExtractFolder;
        ExtractIntoSubfolder=value.ExtractIntoSubfolder;CloseAfterExtraction=value.CloseAfterExtraction;
        FormatIcons=value.FormatIcons;Recent=value.Recent;AutoCheckUpdates=value.AutoCheckUpdates;
        UpdateCheckIntervalMinutes=value.UpdateCheckIntervalMinutes;LastUpdateCheckUtc=value.LastUpdateCheckUtc;
        baseline=Serialize(this);
    }
    internal bool TryReserveUpdateCheck(DateTime now,bool manual)
    {
        using var gate=Lock(StorageFolder);var latest=Load(StorageFolder);
        var due=latest.LastUpdateCheckUtc==default||now<latest.LastUpdateCheckUtc||now-latest.LastUpdateCheckUtc>=TimeSpan.FromMinutes(latest.UpdateCheckIntervalMinutes);
        if(!manual&&(!latest.AutoCheckUpdates||!due)){Adopt(latest);return false;}
        latest.LastUpdateCheckUtc=now;Write(StorageFolder,Document(latest));Adopt(latest);return true;
    }
}
