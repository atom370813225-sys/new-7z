using System.Globalization;

namespace New7z.Core;

public static class ArchiveVolumes
{
    public const long MinimumSize=1024*1024;
    public static bool Supports(string format)=>format is "7z" or "zip" or "tar";
    public static string FirstPath(string output,long volumeSize)=>volumeSize>0?output+".001":output;
    public static long Length(string path)=>path.EndsWith(".001",StringComparison.OrdinalIgnoreCase)?Parts(path[..^4]).Sum(part=>new FileInfo(part).Length):new FileInfo(path).Length;
    internal static void ValidateInput(string path)
    {
        if(!path.EndsWith(".001",StringComparison.OrdinalIgnoreCase))return;
        var expected=1L;
        foreach(var part in Parts(path[..^4]).OrderBy(p=>p.Length).ThenBy(p=>p,StringComparer.OrdinalIgnoreCase))
        {
            if(!long.TryParse(Path.GetExtension(part).AsSpan(1),out var index)||index!=expected)
                throw new IOException(Loc.T($"缺少分卷 .{expected:D3}，请将所有分卷放在同一文件夹。",$"Volume .{expected:D3} is missing. Keep all volumes in the same folder."));
            expected++;
        }
    }
    public static long ParseSize(string? text)
    {
        text=text?.Trim()??"";
        if(text.Length==0)return 0;
        long factor=1;
        foreach(var unit in new[]{("GiB",1L<<30),("MiB",1L<<20),("KiB",1L<<10),("GB",1L<<30),("MB",1L<<20),("KB",1L<<10),("G",1L<<30),("M",1L<<20),("K",1L<<10),("B",1L)})
            if(text.EndsWith(unit.Item1,StringComparison.OrdinalIgnoreCase)){factor=unit.Item2;text=text[..^unit.Item1.Length].Trim();break;}
        if(!decimal.TryParse(text,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var number)||number<=0||number>(decimal)long.MaxValue/factor||number*factor<MinimumSize||decimal.Truncate(number*factor)!=number*factor)
            throw new ArgumentException(Loc.T("分卷大小至少为 1 MiB，例如 100 MiB、1 GiB；留空表示不分卷。","Volume size must be at least 1 MiB, e.g. 100 MiB or 1 GiB; leave blank for no split."));
        return (long)(number*factor);
    }
    public static void Validate(string output,string format,long volumeSize)
    {
        if(volumeSize<0||volumeSize>0&&volumeSize<MinimumSize)throw new ArgumentOutOfRangeException(nameof(volumeSize));
        if(volumeSize>0&&!Supports(format))throw new ArgumentException(Loc.T("分卷压缩支持 7z、ZIP 和 TAR。","Split volumes support 7z, ZIP and TAR."));
        if(File.Exists(output)||Directory.Exists(output)||volumeSize>0&&Parts(output).Any())
            throw new IOException(Loc.T("输出文件或同名分卷已存在，请更换名称，避免覆盖。","The output or a volume with this name already exists. Choose another name."));
    }
    internal static IEnumerable<string> Parts(string output)
    {
        output=Path.GetFullPath(output);var folder=Path.GetDirectoryName(output)!;
        if(!Directory.Exists(folder))yield break;
        var prefix=Path.GetFileName(output)+".";
        // No glob interpolation: names containing '[' or other pattern characters are literal.
        foreach(var path in Directory.EnumerateFileSystemEntries(folder))
        {
            var name=Path.GetFileName(path);
            if(name.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)&&name.Length>=prefix.Length+3&&name.AsSpan(prefix.Length).IndexOfAnyExceptInRange('0','9')<0)yield return path;
        }
    }
    internal static void Commit(string temp,string output,CancellationToken cancellation)
    {
        var moved=new List<string>();
        try
        {
            // Publish the entry point last, after all subsequent volumes exist.
            foreach(var part in Parts(temp).OrderBy(part=>part.EndsWith(".001",StringComparison.Ordinal)?1:0))
            {
                cancellation.ThrowIfCancellationRequested();
                var target=output+part[temp.Length..];File.Move(part,target,false);moved.Add(target);
            }
            cancellation.ThrowIfCancellationRequested();
        }
        catch{foreach(var path in moved)try{File.Delete(path);}catch{}throw;}
    }
}
