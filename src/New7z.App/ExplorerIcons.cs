using System.Text.Json;
using Microsoft.Win32;
using New7z.Core;

namespace New7z.App;

// Mirrors the selected icon theme onto the New7z file-association ProgIDs.
// Explorer only uses these icons when New7z is the default handler for the format.
internal static class ExplorerIcons
{
    private static readonly string[] Formats=["7z","zip","rar","tar","gz","bz2","xz","iso"];
    private static readonly SemaphoreSlim Gate=new(1,1);

    internal static Task<bool> ApplyAsync(string theme,IReadOnlyDictionary<string,string> customIcons)=>
        ApplyAsync(theme,new Dictionary<string,string>(customIcons,StringComparer.OrdinalIgnoreCase),Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory),Registry.CurrentUser);

    // Explorer draws a file with the icon of its default app, so a synced ProgID icon
    // is only visible when Windows resolves that ProgID for the extension.
    internal static bool IsDefaultHandler(string extension)=>
        IsDefaultHandler(extension,Registry.CurrentUser,Registry.LocalMachine);

    // Registry/runtime boundaries are explicit so integration tests can use isolated keys.
    internal static bool IsDefaultHandler(string extension,RegistryKey currentUser,RegistryKey localMachine)
    {
        extension=extension.TrimStart('.').ToLowerInvariant();
        string? progId=null;
        using(var choice=currentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\."+extension+@"\UserChoice"))
            progId=choice?.GetValue("ProgId") as string;
        // An empty user entry keeps the machine-wide default, matching the shell's merged view.
        if(string.IsNullOrEmpty(progId))
            using(var userClass=currentUser.OpenSubKey(@"Software\Classes\."+extension))
                progId=userClass?.GetValue("") as string;
        if(string.IsNullOrEmpty(progId))
            using(var machineClass=localMachine.OpenSubKey(@"Software\Classes\."+extension))
                progId=machineClass?.GetValue("") as string;
        return string.Equals(progId,"New7z."+extension,StringComparison.OrdinalIgnoreCase);
    }

    // Registry/runtime boundaries are explicit so integration tests can use isolated keys.
    internal static async Task<bool> ApplyAsync(string theme,IReadOnlyDictionary<string,string> customIcons,string appDirectory,RegistryKey currentUser)
    {
        await Gate.WaitAsync();
        try
        {
            var installRoot=InstallRoot(appDirectory);
            if(installRoot==null||!IsRegisteredRuntime(installRoot,appDirectory))return false;
            using var classes=currentUser.OpenSubKey(@"Software\Classes",true);
            if(classes==null)return false;
            var executable=Path.Combine(appDirectory,"New7z.App.exe");
            var applied=false;
            foreach(var ext in Formats)
            {
                using var prog=classes.OpenSubKey("New7z."+ext,true);
                if(prog==null)continue;
                if(!OwnedBy(prog,installRoot))continue;
                var hasCustom=customIcons.TryGetValue(ext,out var custom)&&File.Exists(custom);
                var icon=hasCustom
                    ? await CustomFormatIcon.CreateAsync(custom!)
                    : theme=="animals"
                    ? Path.Combine(appDirectory,"Assets","Formats","Animals",ext+".ico")
                    : Path.Combine(appDirectory,"Assets","Formats",ext+".ico");
                if(!File.Exists(icon))continue;
                using(var defaultIcon=prog.CreateSubKey("DefaultIcon"))defaultIcon.SetValue("",hasCustom?"\""+icon+"\",0":icon);
                // The installer can leave an older runtime path after an upgrade; repair it here too.
                using(var command=prog.CreateSubKey(@"shell\open\command"))command.SetValue("","\""+executable+"\" \"%1\"");
                applied=true;
            }
            using(var application=classes.OpenSubKey(@"Applications\New7z.App.exe",true))
            {
                using var command=application?.OpenSubKey(@"shell\open\command");
                if(command?.GetValue("") is string current&&current.StartsWith("\""+installRoot+"\\",StringComparison.OrdinalIgnoreCase))
                {
                    using var writable=application!.CreateSubKey(@"shell\open\command");
                    writable.SetValue("","\""+executable+"\" \"%1\"");
                    applied=true;
                }
            }
            using(var capabilities=currentUser.OpenSubKey(@"Software\New7z\Capabilities",true))
            {
                if(capabilities?.GetValue("ApplicationIcon") is string current&&current.StartsWith(installRoot,StringComparison.OrdinalIgnoreCase))
                {
                    capabilities.SetValue("ApplicationIcon",executable+",0");
                    applied=true;
                }
            }
            if(applied)ShellIntegration.NotifyAssociationsChanged();
            return applied;
        }
        finally{Gate.Release();}
    }

    private static string? InstallRoot(string appDirectory)
    {
        var runtime=new DirectoryInfo(appDirectory);
        return runtime.Parent?.Name=="Versions"?runtime.Parent.Parent?.FullName:null;
    }

    private static bool IsRegisteredRuntime(string installRoot,string appDirectory)
    {
        var state=Path.Combine(installRoot,"installation.json");
        if(!File.Exists(state))return false;
        using var document=JsonDocument.Parse(File.ReadAllText(state));
        if(!document.RootElement.TryGetProperty("RuntimeDirectory",out var property))return false;
        var runtime=property.GetString();
        if(string.IsNullOrWhiteSpace(runtime))return false;
        return string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(runtime)),appDirectory,StringComparison.OrdinalIgnoreCase);
    }

    // Only touch ProgIDs whose open command was registered by this installation root.
    private static bool OwnedBy(RegistryKey prog,string installRoot)
    {
        using var command=prog.OpenSubKey(@"shell\open\command");
        var value=command?.GetValue("") as string;
        return value!=null&&value.StartsWith("\""+installRoot+"\\",StringComparison.OrdinalIgnoreCase);
    }
}
