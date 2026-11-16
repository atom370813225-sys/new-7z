using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
using New7z.App;
using Windows.Graphics.Imaging;

var checks=0;
void Assert(bool value,string message){if(!value)throw new Exception(message);checks++;}
var root=UserSettings.Folder;
var registryPath=@"Software\New7z-IconTests-"+Guid.NewGuid().ToString("N");
Directory.CreateDirectory(root);
async Task WritePng(string path,byte red)
{
    using var stream=File.Create(path);using var output=stream.AsRandomAccessStream();
    var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,output);
    var pixels=new byte[80*40*4];
    for(var i=0;i<pixels.Length;i+=4){pixels[i+2]=red;pixels[i+3]=128;}
    encoder.SetPixelData(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Straight,80,40,96,96,pixels);
    await encoder.FlushAsync();
}
try
{
    var png=Path.Combine(root,"自定义, 图标.png");await WritePng(png,240);
    var ico=await CustomFormatIcon.CreateAsync(png);
    Assert(File.Exists(ico),"PNG must produce a persistent ICO");
    var modified=File.GetLastWriteTimeUtc(ico);
    Assert(await CustomFormatIcon.CreateAsync(png)==ico&&File.GetLastWriteTimeUtc(ico)==modified,"Identical content must reuse the cached ICO");
    using(var reader=new BinaryReader(File.OpenRead(ico)))
    {
        Assert(reader.ReadUInt16()==0&&reader.ReadUInt16()==1&&reader.ReadUInt16()==7,"ICO header and seven frames");
        foreach(var size in new[]{16,24,32,48,64,128,256})
        {
            var width=reader.ReadByte();var height=reader.ReadByte();reader.ReadBytes(6);
            var length=reader.ReadInt32();var offset=reader.ReadInt32();var next=reader.BaseStream.Position;
            Assert((width==0?256:width)==size&&(height==0?256:height)==size,"ICO directory dimensions");
            reader.BaseStream.Position=offset;
            using var frame=new MemoryStream(reader.ReadBytes(length));using var source=frame.AsRandomAccessStream();
            var decoder=await BitmapDecoder.CreateAsync(source);
            var pixels=(await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Straight,new(),ExifOrientationMode.IgnoreExifOrientation,ColorManagementMode.DoNotColorManage)).DetachPixelData();
            Assert(decoder.PixelWidth==size&&decoder.PixelHeight==size,"Every frame must be a decodable square PNG");
            Assert(pixels[3]==0&&pixels[((size/2)*size+size/2)*4+3]==128,"Preserve alpha and center non-square images without stretching");
            var handle=Native.LoadImage(IntPtr.Zero,ico,1,size,size,0x10);
            Assert(handle!=IntPtr.Zero,"Windows must load the ICO at each shell size");Native.DestroyIcon(handle);
            reader.BaseStream.Position=next;
        }
    }
    await WritePng(png,60);
    var changed=await CustomFormatIcon.CreateAsync(png);
    Assert(changed!=ico,"Changed content must use a fresh path to invalidate Explorer's cache");
    var bad=Path.Combine(root,"invalid.png");await File.WriteAllTextAsync(bad,"invalid");
    var rejected=false;try{await CustomFormatIcon.CreateAsync(bad);}catch{rejected=true;}
    Assert(rejected&&!Directory.EnumerateFiles(root,"*.tmp",SearchOption.AllDirectories).Any(),"Invalid PNG must not leave a partial ICO");

    using var sandbox=Registry.CurrentUser.CreateSubKey(registryPath);
    var install=Path.Combine(root,"安装目录");var runtime=Path.Combine(install,"Versions","1.0.9");
    Directory.CreateDirectory(runtime);
    await File.WriteAllTextAsync(Path.Combine(install,"installation.json"),JsonSerializer.Serialize(new Dictionary<string,string>{{"RuntimeDirectory",runtime}},TestJsonContext.Default.DictionaryStringString));
    foreach(var ext in new[]{"zip","7z","rar"})
    {
        var iconFolder=Path.Combine(runtime,"Assets","Formats");Directory.CreateDirectory(Path.Combine(iconFolder,"Animals"));
        File.Copy(ico,Path.Combine(iconFolder,ext+".ico"));File.Copy(ico,Path.Combine(iconFolder,"Animals",ext+".ico"));
        using var prog=sandbox.CreateSubKey(@"Software\Classes\New7z."+ext);
        using(var command=prog.CreateSubKey(@"shell\open\command"))command.SetValue("","\""+(ext=="rar"?Path.Combine(root,"AnotherInstall"):install)+"\\Versions\\old\\New7z.App.exe\" \"%1\"");
        using(var iconKey=prog.CreateSubKey("DefaultIcon"))iconKey.SetValue("","original "+ext);
    }
    using(var choice=sandbox.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.zip\UserChoice"))choice.SetValue("ProgId","Other.Zip");
    string Icon(string ext){using var key=sandbox.OpenSubKey(@"Software\Classes\New7z."+ext+@"\DefaultIcon");return key?.GetValue("") as string??"";}
    var overrides=new Dictionary<string,string>{{"zip",png}};
    Assert(await ExplorerIcons.ApplyAsync("animals",overrides,runtime,sandbox),"Registered installation must apply icons");
    Assert(Icon("zip")=="\""+changed+"\",0","Per-format PNG must override the theme, including paths with commas");
    Assert(Icon("7z")==Path.Combine(runtime,"Assets","Formats","Animals","7z.ico"),"Other formats must use the chosen theme");
    Assert(Icon("rar")=="original rar","Do not modify ProgIDs owned by a different installation");
    using(var command=sandbox.OpenSubKey(@"Software\Classes\New7z.zip\shell\open\command"))Assert(command?.GetValue("") as string=="\""+Path.Combine(runtime,"New7z.App.exe")+"\" \"%1\"","Repair old runtime commands");
    await ExplorerIcons.ApplyAsync("default",overrides,runtime,sandbox);
    Assert(Icon("zip")=="\""+changed+"\",0"&&Icon("7z")==Path.Combine(runtime,"Assets","Formats","7z.ico"),"Switching themes must retain custom overrides");
    await ExplorerIcons.ApplyAsync("animals",new Dictionary<string,string>(),runtime,sandbox);
    Assert(Icon("zip")==Path.Combine(runtime,"Assets","Formats","Animals","zip.ico"),"Reset must restore the current theme");
    await ExplorerIcons.ApplyAsync("default",new Dictionary<string,string>{{"zip",Path.Combine(root,"missing.png")}},runtime,sandbox);
    Assert(Icon("zip")==Path.Combine(runtime,"Assets","Formats","zip.ico"),"Missing custom PNG must fall back to the theme");
    using(var choice=sandbox.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.zip\UserChoice"))Assert(choice?.GetValue("ProgId") as string=="Other.Zip","Never replace Windows default-app choices");
    Assert(!await ExplorerIcons.ApplyAsync("animals",overrides,Path.Combine(install,"Versions","old"),sandbox),"Old or unregistered runtime must not take over associations");
    Assert(!await ExplorerIcons.ApplyAsync("animals",overrides,root,sandbox),"Portable runtime must not change associations");
    Assert(New7z.Core.ShellIntegration.Notifications==4,"Notify the shell after each successful association update");
    Console.WriteLine($"PASS icon checks: {checks}");
}
finally
{
    Registry.CurrentUser.DeleteSubKeyTree(registryPath,false);
    if(Directory.Exists(root))Directory.Delete(root,true);
}

internal static class Native
{
    [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="LoadImageW")]internal static extern IntPtr LoadImage(IntPtr instance,string path,uint type,int width,int height,uint flags);
    [DllImport("user32.dll")]internal static extern bool DestroyIcon(IntPtr icon);
}
[JsonSerializable(typeof(Dictionary<string,string>))]
internal partial class TestJsonContext:JsonSerializerContext;
namespace New7z.App
{
    internal static class UserSettings
    {
        public static string Folder {get;}=Path.Combine(Path.GetTempPath(),"New7z-icon-tests, 中文-"+Guid.NewGuid().ToString("N"));
    }
}
namespace New7z.Core
{
    internal static class ShellIntegration
    {
        internal static int Notifications;
        internal static void NotifyAssociationsChanged()=>Notifications++;
    }
}
