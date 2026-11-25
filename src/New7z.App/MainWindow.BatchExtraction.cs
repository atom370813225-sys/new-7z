using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using New7z.Core;
using WinRT.Interop;
using Windows.Storage.Pickers;
using static New7z.Core.Loc;

namespace New7z.App;

public sealed partial class MainWindow
{
    private sealed class BatchResult(string name){public string Name {get;}=name;public int State {get;set;}}
    private BatchResult[]? lastBatchResults;
    private TextBlock? batchSummaryText;
    private void RefreshBatchSummary()
    {
        if(batchSummaryText==null||lastBatchResults==null)return;
        var results=lastBatchResults;
        var complete=results.Count(r=>r.State==2);var failed=results.Count(r=>r.State==3);var cancelled=results.Count(r=>r.State==4);
        batchSummaryText.Text=T($"最近一批：{results.Length} 个 · 完成 {complete} · 失败 {failed} · 取消 {cancelled}",$"Latest batch: {results.Length} · completed {complete} · failed {failed} · cancelled {cancelled}")+Environment.NewLine+
            string.Join(Environment.NewLine,results.Select(r=>r.Name+" · "+(r.State switch{1=>T("正在处理","Running"),2=>T("已完成","Completed"),3=>T("失败，可在下方重试","Failed; retry below"),4=>T("已取消","Cancelled"),_=>T("等待中","Waiting")})));
    }
    private static readonly string[] ArchiveExtensions=[".7z",".zip",".rar",".tar",".gz",".xz",".bz2",".iso",".001",".zst",".zstd",".tzst",".lz4"];
    private static bool IsArchivePath(string path)=>File.Exists(path)&&ArchiveExtensions.Contains(Path.GetExtension(path),StringComparer.OrdinalIgnoreCase);
    private async Task BatchExtractDialogAsync(IReadOnlyList<string>? initial=null)
    {
        try
        {
            var paths=initial;
            if(paths==null)
            {
                var picker=new FileOpenPicker();InitializeWithWindow.Initialize(picker,Hwnd);
                foreach(var extension in ArchiveExtensions)picker.FileTypeFilter.Add(extension);
                paths=(await picker.PickMultipleFilesAsync()).Select(file=>file.Path).ToArray();
            }
            var archives=paths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if(archives.Length==0)return;
            if(archives.Length>200)throw new ArgumentException(T("一次最多批量处理 200 个压缩包，请分批选择。","Select up to 200 archives per batch."));
            var panel=Stack(12);
            panel.Children.Add(Text(T($"已选择 {archives.Length} 个压缩包",$"Selected {archives.Length} archives"),16,true));
            panel.Children.Add(new ScrollViewer{Content=Text(string.Join(Environment.NewLine,archives),12),MaxHeight=160});
            var destination=Input(T("目标目录","Destination folder"),ShellPaths.PreferredExtractFolder(settings)??Path.GetDirectoryName(archives[0])!);panel.Children.Add(destination);
            panel.Children.Add(Button(T("选择文件夹","Choose folder"),async(_,_)=>{try{var selected=await PickFolderAsync();if(selected!=null)destination.Text=selected;}catch(Exception ex){Notify(ex.Message,InfoBarSeverity.Error);}}));
            var smart=new CheckBox{Content=T("智能解压（只有一个顶层项目时直接解压到目标目录）","Smart extract (one top-level item goes directly into the destination)"),IsChecked=true};panel.Children.Add(smart);
            var policy=new ComboBox{Header=T("遇到同名文件","On name conflict"),ItemsSource=new[]{T("自动重命名，保留两者","Rename, keep both"),T("跳过已有文件","Skip existing files"),T("替换已有文件","Replace existing files")},SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch};panel.Children.Add(policy);
            var error=Text(T("每个压缩包生成独立任务；失败的任务可单独重试。加密包会在处理时询问密码。","Each archive creates a separate task; failed tasks can be retried individually. Encrypted archives request a password when processed."),12);panel.Children.Add(error);
            var dialog=Dialog(T("批量解压","Batch extract"),new ScrollViewer{Content=panel,MaxHeight=560,MinWidth=480},T("开始解压","Start extraction"));
            dialog.PrimaryButtonClick+=(sender,args)=>{try{if(string.IsNullOrWhiteSpace(destination.Text))throw new ArgumentException(T("请输入目标目录。","Enter a destination folder."));_=Path.GetFullPath(destination.Text);}catch(Exception ex){args.Cancel=true;error.Text=ex.Message;destination.Focus(FocusState.Programmatic);}};
            if(await ShowDialogAsync(dialog)!=ContentDialogResult.Primary)return;
            var root=Path.GetFullPath(destination.Text);var conflict=(ConflictPolicy)policy.SelectedIndex;var useSmart=smart.IsChecked==true;
            if(conflict==ConflictPolicy.Replace)
            {
                var confirm=Dialog(T("替换同名文件？","Replace existing files?"),Text(T($"批量解压可能替换此目录中的同名文件：\n{root}",$"Batch extraction may replace files with the same name in:\n{root}")),T("替换并解压","Replace and extract"));confirm.DefaultButton=ContentDialogButton.Close;
                if(await ShowDialogAsync(confirm)!=ContentDialogResult.Primary)return;
            }
            EnqueueBatchExtraction(archives,root,useSmart,conflict);
        }
        catch(Exception ex){Notify(ex.Message,InfoBarSeverity.Error);}
    }
    private void EnqueueBatchExtraction(IReadOnlyList<string> archives,string root,bool smart,ConflictPolicy policy)
    {
        var reserved=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var queued=false;
        var results=archives.Select(path=>new BatchResult(path)).ToArray();lastBatchResults=results;
        for(var index=0;index<archives.Count;index++)
        {
            var result=results[index];var source=Path.GetFullPath(archives[index]);
            var named=ShellPaths.UniqueOutput(Path.Combine(root,ShellPaths.ArchiveStem(source)),reserved);
            var accepted=QueueJob(T("批量解压 ","Batch extract ")+source,root,async(progress,cancellation)=>
            {
                result.State=1;if(ReferenceEquals(lastBatchResults,results))RefreshBatchSummary();
                string? key=null;
                while(true)
                {
                    cancellation.ThrowIfCancellationRequested();
                    try
                    {
                        var items=await engine.ListAsync(source,key,cancellation);
                        if(items.Any(item=>item.Encrypted)&&string.IsNullOrEmpty(key))throw new ArchivePasswordException();
                        var destination=smart?SmartExtraction.GetDestination(root,named,items):named;
                        await engine.ExtractAsync(source,items,destination,policy,key,progress,cancellation);
                        break;
                    }
                    catch(ArchivePasswordException)
                    {
                        key=await AskPasswordAsync();if(key==null)throw new OperationCanceledException(cancellation);
                    }
                }
            },false,ended:job=>{result.State=job.Succeeded?2:job.Cancelled?4:3;if(ReferenceEquals(lastBatchResults,results))RefreshBatchSummary();},extract:true);
            if(!accepted){for(var pending=index;pending<results.Length;pending++)results[pending].State=4;break;}
            queued=true;
        }
        if(queued)SelectPage("tasks");
    }
}
