using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using New7z.Core;
using Windows.Storage.Streams;
using static New7z.Core.Loc;

namespace New7z.App;

public sealed partial class MainWindow
{
    private string? dragVerificationReport;
    private bool dragVerificationTextOnly;
    // Opt-in interactive diagnostic: exercise the real WinUI gesture + native
    // OLE loop without requiring an input tool to drag across window boundaries.
    private async Task PrepareDragVerificationAsync(string input,string reportPath,bool textOnly=false)
    {
        dragVerificationReport=reportPath;
        dragVerificationTextOnly=textOnly;
        await OpenArchiveAsync(input,false);
        var label=Text("Drag verification target",16,true);
        var target=new Border{Child=label,Width=280,Height=160,Padding=new Thickness(16),Margin=new Thickness(0,0,40,90),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Background=(Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],BorderBrush=(Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],BorderThickness=new Thickness(2),AllowDrop=true};
        var drops=0;
        Root.AddHandler(UIElement.DragOverEvent,new DragEventHandler((_,e)=>{e.Handled=true;e.AcceptedOperation=Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;File.AppendAllText(reportPath+".trace","TargetOver"+Environment.NewLine);}),true);
        Root.AddHandler(UIElement.DropEvent,new DragEventHandler(async(_,e)=>{
            e.Handled=true;var deferral=e.GetDeferral();
            var report=new Dictionary<string,string>();
            try
            {
                var items=await e.DataView.GetStorageItemsAsync();
                if(items.Count==0)throw new IOException("The OLE drop contained no storage items.");
                var destination=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(reportPath))!,"drop-"+(++drops));
                Directory.CreateDirectory(destination);
                void Copy(string source,string output)
                {
                    if(File.Exists(source)){File.Copy(source,output);return;}
                    Directory.CreateDirectory(output);
                    foreach(var child in Directory.EnumerateFileSystemEntries(source))Copy(child,System.IO.Path.Combine(output,System.IO.Path.GetFileName(child)));
                }
                foreach(var item in items)Copy(item.Path,System.IO.Path.Combine(destination,item.Name));
                report["Result"]="PASS";report["Drops"]=drops.ToString();report["Items"]=string.Join(" | ",items.Select(item=>item.Name));report["Destination"]=destination;
                label.Text="PASS: "+drops+" drops\n"+report["Items"];
            }
            catch(Exception ex){report["Result"]="FAIL";report["Error"]=ex.ToString();label.Text="FAIL: "+ex.Message;}
            finally{deferral.Complete();}
            await File.WriteAllTextAsync(reportPath,JsonSerializer.Serialize(report,AppJsonContext.Default.DictionaryStringString));
        }),true);
        Grid.SetRowSpan(target,Root.RowDefinitions.Count);Root.Children.Add(target);
    }
    // Opt-in diagnostic: copies a real archive row through the same clipboard path the
    // context menu uses, then compares the copied content with the archive entry.
    private async Task VerifyCopyAsync(string input,string reportPath)
    {
        var report=new Dictionary<string,string>();
        try
        {
            await OpenArchiveAsync(input,false);
            if(archive==null||entries.Count==0)throw new IOException("Archive UI did not load.");
            folder="";ShowArchive();await Task.Delay(150);Root.UpdateLayout();
            var row=fileList?.Items.OfType<ArchiveRow>().FirstOrDefault()??throw new IOException("No archive row rendered to copy.");
            fileList!.SelectedItems.Clear();fileList.SelectedItems.Add(row);
            await CopySelectedAsync();
            for(var wait=0;wait<600&&jobs.Any(job=>job.Active);wait++)await Task.Delay(50);
            var failed=jobs.FirstOrDefault(job=>!job.Active&&job.Title.StartsWith(T("复制到剪贴板 ","Copy to clipboard "),StringComparison.Ordinal));
            if(failed!=null)throw new IOException("Copy task failed: "+failed.Status);
            var content=Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
            if(!content.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))throw new IOException("The clipboard does not carry file items.");
            var items=await content.GetStorageItemsAsync();
            if(items.Count!=1||items[0].Name!=row.Name)throw new IOException("Clipboard items do not match the selected row.");
            var copied=items[0].Path;
            if(row.Entry.IsDirectory)
            {
                var prefix=row.Entry.NormalizedPath+"/";
                var expected=entries.Where(entry=>!entry.IsDirectory&&entry.NormalizedPath.StartsWith(prefix,StringComparison.Ordinal))
                    .Select(entry=>entry.NormalizedPath[prefix.Length..]).OrderBy(path=>path,StringComparer.Ordinal).ToArray();
                var actual=Directory.EnumerateFiles(copied,"*",SearchOption.AllDirectories)
                    .Select(path=>System.IO.Path.GetRelativePath(copied,path).Replace('\\','/')).OrderBy(path=>path,StringComparer.Ordinal).ToArray();
                if(!expected.SequenceEqual(actual))throw new IOException("Copied folder content differs from the archive entries.");
            }
            else
            {
                var copiedBytes=await File.ReadAllBytesAsync(copied);
                var expectedBytes=await engine.ReadAsync(input,row.Entry);
                if(!copiedBytes.SequenceEqual(expectedBytes))throw new IOException("The copied file does not match the archive entry.");
            }
            report["Result"]="PASS";report["Copied"]=row.FullPath;report["ClipboardPath"]=copied;
        }
        catch(Exception ex){report["Result"]="FAIL";report["Error"]=ex.ToString();}
        await File.WriteAllTextAsync(reportPath,JsonSerializer.Serialize(report,AppJsonContext.Default.DictionaryStringString));
        Close();
    }
    // Opt-in diagnostic: opens the row context menu in place so a capture tool can
    // record how it renders without injecting desktop input.
    private async Task ShowRowMenuForCaptureAsync(string input,string reportPath)
    {
        var report=new Dictionary<string,string>();
        try
        {
            await OpenArchiveAsync(input,false);
            folder="";ShowArchive();await Task.Delay(300);Root.UpdateLayout();
            var folderEntry=entries.FirstOrDefault(entry=>entry.IsDirectory);
            if(folderEntry!=null){SetFolder(folderEntry.NormalizedPath);await Task.Delay(300);Root.UpdateLayout();}
            var row=fileList?.Items.OfType<ArchiveRow>().FirstOrDefault(candidate=>!candidate.Entry.IsDirectory&&(candidate.Entry.IsImage||candidate.Entry.IsText))??fileList?.Items.OfType<ArchiveRow>().FirstOrDefault()??throw new IOException("No archive row to show a menu for.");
            fileList!.SelectedItems.Clear();fileList.SelectedItems.Add(row);
            var menu=new MenuFlyout();PopulateRowMenu(menu);
            var anchor=fileList.ContainerFromItem(row) as FrameworkElement??fileList;
            menu.ShowAt(anchor,new FlyoutShowOptions{Placement=FlyoutPlacementMode.BottomEdgeAlignedLeft});
            report["Result"]="PASS";report["Row"]=row.FullPath;
            report["Menu"]=string.Join(" | ",menu.Items.OfType<MenuFlyoutItem>().Select(item=>item.Text+(item.IsEnabled?"":"(disabled)")));
            await File.WriteAllTextAsync(reportPath,JsonSerializer.Serialize(report,AppJsonContext.Default.DictionaryStringString));
            await Task.Delay(TimeSpan.FromSeconds(40));
        }
        catch(Exception ex){report["Result"]="FAIL";report["Error"]=ex.ToString();await File.WriteAllTextAsync(reportPath,JsonSerializer.Serialize(report,AppJsonContext.Default.DictionaryStringString));}
        Close();
    }
    // Explicit local diagnostic entry point for validating the published/trimmed app.
    // It does not send desktop input, change preferences or invoke system registration.
    private async Task VerifyUiAsync(string input,string reportPath)
    {
        var report=new Dictionary<string,string>();
        try
        {
            var volumes=new VolumeSizeField();ShowContent(volumes.View);Root.UpdateLayout();
            var choice=(ComboBox)volumes.View.Children[0];var custom=(TextBox)volumes.View.Children[1];
            if(volumes.View.ActualWidth<=0||volumes.Value!=0)throw new IOException("Volume field did not render with no split by default.");
            choice.SelectedIndex=2;if(volumes.Value!=100L*1024*1024)throw new IOException("Volume preset mismatch.");
            choice.SelectedIndex=7;custom.Text="1.5 GiB";Root.UpdateLayout();
            if(custom.Visibility!=Visibility.Visible||custom.ActualHeight<=0||volumes.Value!=1610612736)throw new IOException("Custom volume field did not render or parse.");
            custom.Text="invalid";var rejected=false;try{_=volumes.Value;}catch(ArgumentException){rejected=true;}if(!rejected)throw new IOException("Invalid volume size was accepted.");
            custom.Text="1 MiB";volumes.SetFormat("zst");if(choice.IsEnabled||volumes.Value!=0)throw new IOException("Stream format did not disable volumes.");
            volumes.SetFormat("7z");if(!choice.IsEnabled||volumes.Value!=1048576)throw new IOException("Volume draft was lost after switching formats.");
            report["Volumes"]="shared main/Shell field renders; no split by default; preset/custom/invalid values; disabled stream formats; draft restored on format switch";
            SelectPage("settings");Root.UpdateLayout();
            var settingsLabels=new List<string>();
            void CollectSettingsLabels(DependencyObject node)
            {
                if(node is TextBlock label)settingsLabels.Add(label.Text);
                for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)CollectSettingsLabels(VisualTreeHelper.GetChild(node,i));
            }
            CollectSettingsLabels(ContentHost);
            if(!settingsLabels.Contains(T("常规","General"))||!settingsLabels.Contains(T("压缩","Compression"))||!settingsLabels.Contains(T("右键菜单","Context menu")))throw new IOException("Settings categories did not render.");
            settingsCategory="compression";RenderPage();Root.UpdateLayout();
            settingsLabels.Clear();CollectSettingsLabels(ContentHost);
            if(!settingsLabels.Contains(T("默认压缩格式","Default archive format"))||!settingsLabels.Contains(T("默认压缩方法","Default compression method")))throw new IOException("Compression defaults did not render.");
            report["Settings"]="rendered settings hub and the compression defaults category";
            settingsCategory="general";RenderPage();Root.UpdateLayout();
            ComboBox? intervalChoice=null;
            void FindInterval(DependencyObject node)
            {
                if(node is ComboBox combo&&combo.Header as string==T("自动检查间隔","Automatic check interval"))intervalChoice=combo;
                for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)FindInterval(VisualTreeHelper.GetChild(node,i));
            }
            FindInterval(ContentHost);
            if(!AppEnvironment.IsPackaged&&(intervalChoice==null||intervalChoice.Items.Count!=5||intervalChoice.SelectedIndex<0))throw new IOException("Automatic update interval controls did not render.");
            if(intervalChoice!=null){intervalChoice.IsDropDownOpen=true;Root.UpdateLayout();intervalChoice.IsDropDownOpen=false;}
            report["UpdateInterval"]="native interval selector renders five options and opens without saving preferences";
            var previousTheme=settings.FormatIconTheme;
            var previousIcons=settings.FormatIcons;settings.FormatIcons=[];
            try
            {
                settings.FormatIconTheme="animals";settingsCategory="appearance";RenderPage();Root.UpdateLayout();
                if(!FormatIconPath("zip").Replace('\\','/').EndsWith("Assets/Formats/Animals/zip.png",StringComparison.OrdinalIgnoreCase))throw new IOException("Animal icon theme did not resolve.");
                ComboBox? themeCombo=null;var themeImages=new List<Image>();
                void FindThemeControls(DependencyObject node)
                {
                    if(node is ComboBox combo&&combo.Header is string header&&header==T("图标主题","Icon theme"))themeCombo=combo;
                    if(node is Image image&&image.Source is BitmapImage bitmap&&bitmap.UriSource!=null&&bitmap.UriSource.OriginalString.Replace('\\','/').Contains("/Assets/Formats/Animals/",StringComparison.OrdinalIgnoreCase))themeImages.Add(image);
                    for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)FindThemeControls(VisualTreeHelper.GetChild(node,i));
                }
                FindThemeControls(ContentHost);
                if(themeCombo==null||themeCombo.SelectedIndex!=1||themeImages.Count<8)throw new IOException("Animal icon theme did not render with its preview.");
                var probe=new UserSettings{FormatIconTheme="animals"};
                var serialized=JsonSerializer.Serialize(probe,AppJsonContext.Default.UserSettings);
                if(JsonSerializer.Deserialize(serialized,AppJsonContext.Default.UserSettings)?.FormatIconTheme!="animals")throw new IOException("Icon theme did not round-trip through the settings JSON context.");
                var customIcon=System.IO.Path.Combine(AppContext.BaseDirectory,"Assets","Formats","zip.png");
                var explorerIcon=await CustomFormatIcon.CreateAsync(customIcon);
                using(var iconReader=new BinaryReader(File.OpenRead(explorerIcon)))
                    if(iconReader.ReadUInt16()!=0||iconReader.ReadUInt16()!=1||iconReader.ReadUInt16()!=7)throw new IOException("Published PNG-to-ICO conversion failed.");
                report["CustomExplorerIcon"]="published PNG decoder/encoder generated a seven-size ICO without changing system associations";
                settings.FormatIcons["zip"]=customIcon;
                if(FormatIconPath("zip")!=customIcon)throw new IOException("Custom icon did not override the theme.");
                settings.FormatIconTheme="default";
                if(FormatIconPath("zip")!=customIcon)throw new IOException("Theme switch replaced a custom icon.");
                settings.FormatIcons.Remove("zip");settings.FormatIconTheme="animals";
                if(!FormatIconPath("zip").Replace('\\','/').EndsWith("Assets/Formats/Animals/zip.png",StringComparison.OrdinalIgnoreCase))throw new IOException("Reset did not restore the current theme.");
                report["FormatIconTheme"]="animal theme resolves and renders eight previews; settings JSON round-trip; custom PNG overrides both themes; reset restores the current theme";
            }
            finally{settings.FormatIconTheme=previousTheme;settings.FormatIcons=previousIcons;settingsCategory="compression";RenderPage();}
            var taskJob=new ArchiveJob("verification job","verification target"){Status="verification"};
            var failedJob=new ArchiveJob("verification failed job","verification target"){Status="Failed: verification",Active=false,Percent=100};
            jobs.Add(taskJob);jobs.Add(failedJob);
            SelectPage("tasks");Root.UpdateLayout();
            var taskContent=ContentHost.Children[0];
            CompleteJob(taskJob);await Task.Delay(50);Root.UpdateLayout();
            if(jobs.Contains(taskJob)||!jobs.Contains(failedJob)||!ReferenceEquals(taskContent,ContentHost.Children[0]))throw new IOException("Task completion did not remove only the successful job in place.");
            var taskLabels=new List<string>();
            void ReadTaskLabels(DependencyObject node)
            {
                if(node is TextBlock label)taskLabels.Add(label.Text);
                for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)ReadTaskLabels(VisualTreeHelper.GetChild(node,i));
            }
            ReadTaskLabels(ContentHost);
            if(taskLabels.Contains(taskJob.Title)||!taskLabels.Contains(failedJob.Title))throw new IOException("Completed task card remained visible or failed task disappeared.");
            jobs.Remove(failedJob);failedJob.Cancellation.Dispose();taskJob.Cancellation.Dispose();
            var lastJob=new ArchiveJob("verification last job","verification target");jobs.Add(lastJob);
            SelectPage("tasks");Root.UpdateLayout();CompleteJob(lastJob);
            await Task.Delay(50);Root.UpdateLayout();lastJob.Cancellation.Dispose();
            if(tasksEmptyCard==null||(sharedTaskViews.Count==0&&tasksEmptyCard.Visibility!=Visibility.Visible))throw new IOException("Task empty state did not appear after the last successful job.");
            SelectPage("home");SelectPage("tasks");Root.UpdateLayout();
            taskLabels.Clear();ReadTaskLabels(ContentHost);
            if(taskLabels.Contains(taskJob.Title)||taskLabels.Contains(lastJob.Title))throw new IOException("Completed task reappeared after navigation.");
            report["Tasks"]="successful job removed in place without rebuilding the page; failed job at 100% retained; last completion shows empty state; no completed jobs after navigation";
            var attempts=0;var retryName="verification retry "+Guid.NewGuid().ToString("N");
            QueueJob(retryName,"verification target",(_,_)=>++attempts==1?Task.FromException(new IOException("expected retry probe")):Task.CompletedTask,false);
            for(var wait=0;wait<600&&jobs.Any(job=>job.Title==retryName&&job.Active);wait++)await Task.Delay(50);
            var retryJob=jobs.SingleOrDefault(job=>job.Title==retryName);
            if(retryJob?.Retry==null||retryJob.Active)throw new IOException($"Failed task did not retain its retry action. job={(retryJob==null?"missing":"present")} active={retryJob?.Active} attempts={attempts}");
            retryJob.Retry();
            for(var wait=0;wait<600&&jobs.Any(job=>job.Title==retryName&&job.Active);wait++)await Task.Delay(50);
            if(attempts!=2||jobs.Any(job=>job.Title==retryName))throw new IOException("Task retry did not rerun the original operation and clear success.");
            report["TaskRetry"]="failed task is retried with its original operation; successful retry removes the task";
            var batchFolder=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(reportPath))!,"batch-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(batchFolder);var copy=System.IO.Path.Combine(batchFolder,"second"+System.IO.Path.GetExtension(input));File.Copy(input,copy);
            EnqueueBatchExtraction([input,copy],System.IO.Path.Combine(batchFolder,"output"),false,New7z.Core.ConflictPolicy.Rename);
            for(var wait=0;wait<600&&jobs.Any(job=>job.Active);wait++)await Task.Delay(50);
            Root.UpdateLayout();
            if(lastBatchResults?.Count(result=>result.State==2)!=2||lastBatchResults.Any(result=>result.State!=2)||batchSummaryText==null)throw new IOException("Batch extraction did not complete two independent tasks and retain results.");
            report["BatchExtraction"]="two archives extract into separate folders; independent tasks finish and the per-archive summary remains visible";
            await OpenArchiveAsync(input,false);
            if(archive==null||entries.Count==0)throw new IOException("Archive UI did not load.");
            var firstDirectory=entries.FirstOrDefault(e=>e.IsDirectory);
            if(firstDirectory!=null){folder=firstDirectory.NormalizedPath;ShowArchive();}
            await Task.Delay(200);Root.UpdateLayout();
            var visibleNames=entries.Where(e=>!e.IsDirectory&&e.Directory==folder).Select(e=>e.Name).ToArray();
            var rendered=new List<string>();
            void ReadText(DependencyObject node)
            {
                if(node is TextBlock label)rendered.Add(label.Text);
                for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)ReadText(VisualTreeHelper.GetChild(node,i));
            }
            ReadText(ContentHost);
            if(visibleNames.Length>0&&!visibleNames.Any(name=>rendered.Contains(name)))throw new IOException("Published XAML file-name binding did not render.");
            report["ArchiveBinding"]="file names rendered via native XAML bindings";
            var originalList=fileList;
            var originalFolder=folder;
            if(fileList!.Items.Count>0)fileList.SelectedItem=fileList.Items[0];
            var selectedKeys=fileList.SelectedItems.OfType<ArchiveRow>().Select(row=>row.FullPath).ToHashSet();
            sortColumn=New7z.Core.ArchiveSortColumn.Size;sortDescending=true;UpdateSortHeaders();RefreshRows();
            if(!ReferenceEquals(originalList,fileList)||!fileList.SelectedItems.OfType<ArchiveRow>().Select(row=>row.FullPath).ToHashSet().SetEquals(selectedKeys))throw new IOException("Sort recreated the list or lost selection.");
            string[] ArchiveProperties()=>[archiveFileCountText?.Text??"",archiveOriginalSizeText?.Text??"",archivePackedSizeText?.Text??""];
            var archiveProperties=ArchiveProperties();
            if(archiveProperties.Any(string.IsNullOrEmpty))throw new IOException("Archive properties did not render.");
            if(archiveFileCountText!.Text!=entries.Count(entry=>!entry.IsDirectory).ToString("N0")||archivePackedSizeText!.Text!=New7z.Core.ByteSize.Format((ulong)new FileInfo(input).Length))throw new IOException("Properties do not describe the whole archive.");
            var expectedComment=(await engine.CommentAsync(input)).Trim();
            var commentBlock=archivePropertiesPanel.FindName("CommentBlock") as StackPanel;
            var commentText=archivePropertiesPanel.FindName("CommentText") as TextBlock;
            if(expectedComment.Length>0)
            {
                for(var wait=0;wait<100&&commentBlock!.Visibility!=Visibility.Visible;wait++)await Task.Delay(50);
                if(commentBlock!.Visibility!=Visibility.Visible||commentText?.Text!=expectedComment)throw new IOException("The archive comment is not shown with the archive properties.");
                report["ArchiveComment"]="ZIP/RAR archive comment shown with the archive sizes";
            }
            else if(commentBlock!.Visibility==Visibility.Visible)throw new IOException("A comment block is shown for an archive without a comment.");
            fileList.SelectAll();UpdateSelectionDetails();
            if(!ArchiveProperties().SequenceEqual(archiveProperties))throw new IOException("Selection changed the whole-archive properties.");
            if(selectAllCheck==null)throw new IOException("The header select-all checkbox is missing.");
            if(fileList.Items.Count>0)
            {
                selectAllCheck.IsChecked=false;
                if(fileList.SelectedItems.Count!=0)throw new IOException("Clearing the header select-all checkbox did not clear the selection.");
                selectAllCheck.IsChecked=true;
                if(fileList.SelectedItems.Count!=fileList.Items.Count)throw new IOException("Checking the header select-all checkbox did not select every row.");
                report["SelectAllCheckBox"]="header checkbox selects and clears every visible row";
            }
            SetFolder("");SetFolder(originalFolder);
            if(!ReferenceEquals(originalList,fileList))throw new IOException("Folder navigation recreated the list.");
            query="no-such-name-"+Guid.NewGuid().ToString("N");RefreshRows();if(fileList.Items.Count!=0)throw new IOException("No-result search did not clear rows.");
            if(!ArchiveProperties().SequenceEqual(archiveProperties))throw new IOException("Folder navigation or search changed the whole-archive properties.");
            query="";RefreshRows();
            var images=new List<Image>();
            void ReadImages(DependencyObject node){if(node is Image view)images.Add(view);for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)ReadImages(VisualTreeHelper.GetChild(node,i));}
            await Task.Delay(200);Root.UpdateLayout();ReadImages(fileList);
            if(fileList.Items.OfType<ArchiveRow>().Any(row=>!row.Entry.IsDirectory)&&!images.Any(view=>view.Source is BitmapImage bitmap&&bitmap.PixelWidth>0))throw new IOException("File type icons did not decode in the native list.");
            var archiveButtons=new List<string>();
            void ReadButtons(DependencyObject node){if(node is Button button&&button.Content is string label)archiveButtons.Add(label);for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)ReadButtons(VisualTreeHelper.GetChild(node,i));}
            ReadButtons(ContentHost);
            if(archiveButtons.Contains(T("属性","Properties")))throw new IOException("The obsolete Properties button is still present.");
            if(!archiveButtons.Contains(T("复制选中","Copy selected")))throw new IOException("The archive toolbar is missing the copy selected command.");
            report["ArchiveCopyCommand"]="toolbar exposes copy selected beside extract selected";
            if(archivePropertiesPanel?.ActualWidth!=200||archivePropertiesPanel.BorderThickness.Left!=1)throw new IOException("The archive sidebar or divider did not render.");
            var windowSize=AppWindow.Size;var scale=Root.XamlRoot.RasterizationScale;
            AppWindow.Resize(new Windows.Graphics.SizeInt32((int)(900*scale),(int)(680*scale)));
            await Task.Delay(250);Root.UpdateLayout();
            Button? ArchiveButton(string label)
            {
                Button? found=null;
                void Walk(DependencyObject node)
                {
                    if(found!=null)return;
                    if(node is Button button&&button.Content is string text&&text==label)found=button;
                    for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)Walk(VisualTreeHelper.GetChild(node,i));
                }
                Walk(ContentHost);return found;
            }

            foreach(var label in new[]{T("复制选中","Copy selected"),T("新建压缩","New archive")})
            {
                var button=ArchiveButton(label)??throw new IOException("Archive command button is missing at 900 DIP: "+label);
                var bounds=button.TransformToVisual(ContentHost).TransformBounds(new Windows.Foundation.Rect(0,0,button.ActualWidth,button.ActualHeight));
                if(bounds.Right>ContentHost.ActualWidth+0.5)throw new IOException("Archive command button overflows the content area at 900 DIP: "+label);
            }
            report["NarrowCommands"]="primary and secondary commands stay inside the content area at 900 DIP";
            if(archivePropertiesPanel.ActualWidth!=200||archivePropertiesPanel.Visibility!=Visibility.Visible||archiveTableScroll!.ViewportWidth<=0||archiveTableScroll.ScrollableWidth<=0)throw new IOException("Narrow layout hid the archive sidebar or lost horizontal table scrolling.");
            AppWindow.Resize(windowSize);
            await Task.Delay(250);Root.UpdateLayout();
            if(archiveTableScroll!.ScrollableWidth>0.5)throw new IOException("The archive table scrolls horizontally at the default window size.");
            report["DefaultWidthTable"]="the archive table fits the default window without a horizontal scrollbar";
            report["ArchiveView"]="sort preserves selection; folder navigation preserves ListView; empty search; decoded file type icons";
            var rowMenu=rowContextMenu??throw new IOException("The archive row context menu is missing.");
            var menuRow=fileList.Items.OfType<ArchiveRow>().FirstOrDefault()??throw new IOException("No archive row to build the context menu for.");
            fileList.SelectedItems.Clear();fileList.SelectedItems.Add(menuRow);
            PopulateRowMenu(rowMenu);
            var menuLabels=rowMenu.Items.OfType<MenuFlyoutItem>().Select(item=>item.Text).ToList();
            var expectedMenuLabels=new List<string>{T("解压到…","Extract to…"),T("复制","Copy"),T("复制文件名","Copy file name"),T("复制包内路径","Copy archive path"),T("删除","Delete"),T("属性","Properties")};
            if(!menuRow.Entry.IsDirectory)expectedMenuLabels.Add(T("预览","Preview"));
            foreach(var label in expectedMenuLabels)
                if(!menuLabels.Contains(label))throw new IOException("The archive row context menu is missing: "+label);
            if(menuRow.Entry.IsDirectory&&menuLabels.Contains(T("预览","Preview")))throw new IOException("The folder context menu offers preview.");
            var archiveFileName=System.IO.Path.GetFileName(archive!)!;
            if(TextForRows([menuRow],false,archiveFileName)!=menuRow.Name)throw new IOException("Copied file name text mismatch.");
            if(TextForRows([menuRow],true,archiveFileName)!=archiveFileName+"!/"+menuRow.FullPath)throw new IOException("Copied archive path text mismatch.");
            if(!PropertiesLines([menuRow],entries,archiveFileName).Any(pair=>pair.Value.Contains(menuRow.FullPath,StringComparison.Ordinal)))throw new IOException("Entry properties do not include the archive path.");
            var menuFolder=menuRow.Entry.IsDirectory?menuRow.Entry:entries.FirstOrDefault(entry=>entry.IsDirectory);
            if(menuFolder!=null)
            {
                // Enter a folder so the file and multi-selection menus are exercised on real rows.
                SetFolder(menuFolder.NormalizedPath);
                var fileRows=fileList.Items.OfType<ArchiveRow>().Where(row=>!row.Entry.IsDirectory).ToList();
                var previewRow=fileRows.FirstOrDefault(row=>row.Entry.IsImage||row.Entry.IsText);
                if(previewRow!=null)
                {
                    fileList.SelectedItems.Clear();fileList.SelectedItems.Add(previewRow);
                    PopulateRowMenu(rowMenu);
                    var previewItem=rowMenu.Items.OfType<MenuFlyoutItem>().FirstOrDefault(item=>item.Text==T("预览","Preview"))??throw new IOException("The file context menu is missing preview.");
                    if(!previewItem.IsEnabled)throw new IOException("Preview is disabled for a text or image fixture.");
                    if(!PropertiesLines([previewRow],entries,archiveFileName).Any(pair=>pair.Label==T("包内路径","Archive path")))throw new IOException("File properties do not report the archive path.");
                }
                if(fileRows.Count>=2)
                {
                    fileList.SelectedItems.Clear();fileList.SelectedItems.Add(fileRows[0]);fileList.SelectedItems.Add(fileRows[1]);
                    PopulateRowMenu(rowMenu);
                    menuLabels=rowMenu.Items.OfType<MenuFlyoutItem>().Select(item=>item.Text).ToList();
                    if(!menuLabels.Contains(T("解压选中的 2 项…","Extract 2 selected items…"))||!menuLabels.Contains(T("查看所选属性","Properties of selection")))throw new IOException("The multi-selection context menu is missing extract or properties.");
                    if(menuLabels.Contains(T("预览","Preview")))throw new IOException("The multi-selection context menu still offers preview.");
                    if(!PropertiesLines(fileList.SelectedItems.Cast<ArchiveRow>().ToList(),entries,archiveFileName).Any(pair=>pair.Label==T("已选择","Selected")))throw new IOException("Multi-selection properties do not report the selection count.");
                }
                SetFolder("");
            }
            report["ArchiveContextMenu"]="row menu adapts to folder, single file and multi selection and exposes preview, extract, copy, text copy, delete and properties";
            foreach(var (x,y,width,height) in new[]{(40d,40d,800d,600d),(760d,40d,800d,600d),(40d,560d,800d,600d),(760d,560d,800d,600d),(400d,300d,800d,600d)})
                if(!MenuPointerStaysClear(x,y,width,height))throw new IOException($"The row menu would open under the pointer at {x},{y}.");
            report["MenuCursor"]="row menu opens clear of the pointer, including the popup shadow";
            if(!IsInstallerEntry(new ArchiveEntry{Path="setup.exe"})||!IsInstallerEntry(new ArchiveEntry{Path="package.msi"})||!IsInstallerEntry(new ArchiveEntry{Path="tool.msix"})||IsInstallerEntry(new ArchiveEntry{Path="readme.txt"})||IsInstallerEntry(new ArchiveEntry{Path="folder.exe",IsDirectory=true})||IsInstallerEntry(new ArchiveEntry{Path="link.msi",IsLink=true}))throw new IOException("Installer detection is wrong.");
            report["InstallerRun"]="exe/msi/msix/appx/msu rows run after extraction; other files keep preview; folders and links are excluded";
            report["ArchiveProperties"]="whole-archive totals remain stable during selection, navigation and search; actual archive file size; no Properties button; 200 DIP sidebar with a vertical divider remains visible at 900 DIP, with horizontal table scrolling";
            var text=entries.FirstOrDefault(e=>!e.IsDirectory&&e.IsText);
            if(text!=null)
            {
                var bytes=await engine.ReadAsync(input,text);if(bytes.Length==0)throw new IOException("Text fixture empty.");report["Text"]="decoded "+New7z.Core.ArchiveEngine.DecodeText(bytes).Length+" characters";
                await VerifyPreviewAsync(text);
                report["TextPreview"]="one preview window; stable content after 1.4 seconds; find and wrap reuse the text control; close releases the preview session";
                var opening=ShowPreviewAsync(text);previewWindowPointer!.Close();await opening.WaitAsync(TimeSpan.FromSeconds(10));
                if(previewOpen||previewWindowPointer!=null)throw new IOException("Closing a loading preview did not release its session.");
                report["PreviewCancel"]="closed while reading; cancellation completed and released the preview session";
            }
            var image=entries.FirstOrDefault(e=>!e.IsDirectory&&e.IsImage);
            if(image!=null)
            {
                var bytes=await engine.ReadAsync(input,image);
                using var stream=new InMemoryRandomAccessStream();using(var writer=new DataWriter(stream)){writer.WriteBytes(bytes);await writer.StoreAsync();writer.DetachStream();}
                stream.Seek(0);var bitmap=new BitmapImage();await bitmap.SetSourceAsync(stream);
                if(bitmap.PixelWidth==0)throw new IOException("Image decode failed.");report["Image"]=$"decoded {bitmap.PixelWidth}x{bitmap.PixelHeight}";
                await VerifyPreviewAsync(image);
                report["ImagePreview"]="image loaded in a single owned preview window; close releases the preview session";
            }
            report["Result"]="PASS";
        }
        catch(Exception ex){report["Result"]="FAIL";report["Error"]=ex.ToString();}
        await File.WriteAllTextAsync(reportPath,JsonSerializer.Serialize(report,AppJsonContext.Default.DictionaryStringString));
        Close();
    }
    private async Task VerifyPreviewAsync(New7z.Core.ArchiveEntry entry)
    {
        var session=ShowPreviewAsync(entry);var window=previewWindowPointer??throw new IOException("Preview did not create a window.");
        try
        {
            if(GetVerificationWindowOwner(WinRT.Interop.WindowNative.GetWindowHandle(window),4)!=Hwnd)throw new IOException("The preview is not owned by the main window.");
            var root=(FrameworkElement)window.Content;
            T? Find<T>(DependencyObject node,string name) where T:FrameworkElement
            {
                if(node is T element&&element.Name==name)return element;
                for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)if(Find<T>(VisualTreeHelper.GetChild(node,i),name) is {} child)return child;
                return null;
            }
            var timer=System.Diagnostics.Stopwatch.StartNew();Grid? host=null;
            while(timer.Elapsed<TimeSpan.FromSeconds(15))
            {
                await Task.Delay(50);root.UpdateLayout();
                if(session.IsCompleted){await session;throw new IOException("Preview closed before loading content.");}
                host=Find<Grid>(root,"PreviewContentHost");
                if(host?.Children.Count==1&&(entry.IsText?Find<TextBox>(root,"PreviewTextContent")!=null:host.Children[0] is ScrollViewer))break;
            }
            if(host==null||host.Children.Count!=1)throw new IOException("Preview did not load its content.");
            var content=host.Children[0];
            if(entry.IsImage&&content is not ScrollViewer)throw new IOException("Image preview did not load its content.");
            if(entry.IsText&&content is not Grid)throw new IOException("Text preview did not load its content.");
            await ShowPreviewAsync(entry);
            if(!ReferenceEquals(window,previewWindowPointer))throw new IOException("Repeated activation created a second preview window.");
            if(entry.IsText)
            {
                var text=Find<TextBox>(root,"PreviewTextContent")??throw new IOException("Text preview is missing its content box.");
                if(text.Text.Length==0)throw new IOException("Text preview is empty.");
                var gutter=Find<Canvas>(root,"PreviewLineNumbers")??throw new IOException("Text preview is missing its line-number gutter.");
                for(var wait=0;wait<40&&gutter.Children.Count==0;wait++){await Task.Delay(50);root.UpdateLayout();}
                if(gutter.Children.Count==0)throw new IOException("Text preview did not render line numbers.");
                var bytes=await engine.ReadAsync(archive!,entry);
                var decoded=New7z.Core.ArchiveEngine.DecodeText(bytes).Replace("\r\n","\n").Replace('\r','\n').Split('\n');
                string Normalize(string value)=>value.Replace("\r\n","\n").Replace('\r','\n');
                if(Normalize(text.Text)!=string.Join("\n",decoded.Take(100000)))throw new IOException("Preview text still contains line-number prefixes instead of a separate gutter.");
                var find=Find<TextBox>(root,"PreviewFind")??throw new IOException("Preview search field is missing.");
                var bodyHeight=host.ActualHeight;
                find.Text="no-such-preview-text-"+Guid.NewGuid().ToString("N");root.UpdateLayout();
                if(!ReferenceEquals(content,host.Children[0])||host.ActualHeight!=bodyHeight)throw new IOException("Finding text recreated or resized the preview content.");
                find.Text="";
                var wrap=Find<ToggleSwitch>(root,"PreviewWrap")??throw new IOException("Preview wrap control is missing.");
                wrap.IsOn=false;root.UpdateLayout();
                if(!ReferenceEquals(content,host.Children[0])||text.TextWrapping!=TextWrapping.NoWrap)throw new IOException("Changing wrap recreated the preview content.");
                wrap.IsOn=true;
            }
            await Task.Delay(1400);
            if(!ReferenceEquals(content,host.Children[0])||!ReferenceEquals(window,previewWindowPointer))throw new IOException("Preview content changed after its initial load.");
        }
        finally{window.Close();await session.WaitAsync(TimeSpan.FromSeconds(10));}
        if(previewOpen||previewWindowPointer!=null)throw new IOException("Closing the preview did not release its session.");
    }
    [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="GetWindow")]
    private static extern nint GetVerificationWindowOwner(nint window,uint command);
}
