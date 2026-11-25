using New7z.Core;

namespace New7z.App;
public sealed record ArchiveRow(ArchiveEntry Entry)
{
    public string Name=>Entry.Name;
    public string FullPath=>Entry.NormalizedPath;
    public string SizeText=>Entry.SizeText;
    public string PackedSizeText=>Entry.PackedSizeText;
    public string ModifiedText=>Entry.ModifiedText;
    public Microsoft.UI.Xaml.Media.ImageSource? Icon=>Entry.IsDirectory?null:FileTypeIcons.For(Entry);
    public Microsoft.UI.Xaml.Visibility FileIconVisibility=>Entry.IsDirectory?Microsoft.UI.Xaml.Visibility.Collapsed:Microsoft.UI.Xaml.Visibility.Visible;
    public Microsoft.UI.Xaml.Visibility FolderIconVisibility=>Entry.IsDirectory?Microsoft.UI.Xaml.Visibility.Visible:Microsoft.UI.Xaml.Visibility.Collapsed;
    public string Glyph=>Entry.IsDirectory?"\uE8B7":Entry.IsImage?"\uEB9F":Entry.IsText?"\uE8A5":"\uE7C3";
}
public sealed class ArchiveJob(string title,string destination)
{
    public string Title {get;}=title;
    public string Destination {get;}=destination;
    public CancellationTokenSource Cancellation {get;}=new();
    private string status="";
    private double percent;
    private bool active=true;
    private bool cancelable=true;
    private string currentItem="";
    public string Status {get=>status;set{status=value;Changed();}}
    public double Percent {get=>percent;set{percent=value;Changed();}}
    public string PercentText=>$"{percent:0}%";
    public string CurrentItem {get=>currentItem;set{currentItem=value;Changed();}}
    public bool Active {get=>active;set{active=value;Changed();}}
    public bool CanCancel=>active&&cancelable;
    public bool Cancelable {get=>cancelable;set{cancelable=value;Changed();}}
    public Action? Retry {get;set;}
    public bool Succeeded {get;set;}
    public bool Cancelled {get;set;}
    // Extraction jobs keep a completed card so the destination can be reopened; drag jobs do not.
    public bool Extract {get;set;}
    public bool KeepCompleted {get;set;}
    public bool Indeterminate {get;private set;}=true;
    private readonly TransferTracker transfer=new();
    public string TransferText
    {
        get
        {
            if(transfer.BytesPerSecond<=0)return "";
            var speed=ByteSize.Format((ulong)transfer.BytesPerSecond)+"/s";
            if(transfer.Remaining is not { } remaining)return speed;
            return speed+Loc.T(" · 剩余 "," · remaining ")+$"{(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
        }
    }
    public void Report(EngineProgress progress,TimeSpan elapsed)
    {
        if(progress.CurrentItem is {Length:>0})currentItem=progress.CurrentItem;
        Indeterminate=progress.Total==0;
        if(progress.Total>0)percent=Math.Clamp(progress.Completed*100.0/progress.Total,0,100);
        transfer.Update(progress,elapsed);Changed();
    }
    public event Action<ArchiveJob>? Updated;
    private void Changed()=>Updated?.Invoke(this);
}
public sealed record CompressionChoice(string Value,string Display){public override string ToString()=>Display;}
public static class CompressionOptions
{
    public static IReadOnlyList<CompressionChoice> For(string format)=>CompressionMethods.For(format).Select(value=>new CompressionChoice(value,Label(value))).ToArray();
    public static string Label(string value)=>value switch
    {
        "LZMA2"=>Loc.T("LZMA2（推荐）","LZMA2 (recommended)"),
        "Deflate"=>Loc.T("Deflate（推荐）","Deflate (recommended)"),
        "Copy"=>Loc.T("存储（不压缩）","Store (no compression)"),
        _=>value
    };
    public static string Default(UserSettings settings,string format)=>format switch
    {
        "zip"=>CompressionMethods.Normalize("zip",settings.DefaultZipMethod)??CompressionMethods.Default("zip"),
        "7z"=>CompressionMethods.Normalize("7z",settings.Default7zMethod)??CompressionMethods.Default("7z"),
        _=>""
    };
}
