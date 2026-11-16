using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
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
            fileList.SelectAll();UpdateSelectionDetails();
            if(!ArchiveProperties().SequenceEqual(archiveProperties))throw new IOException("Selection changed the whole-archive properties.");
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
            if(archivePropertiesPanel?.ActualWidth!=240||archivePropertiesPanel.BorderThickness.Left!=1)throw new IOException("The archive sidebar or divider did not render.");
            var windowSize=AppWindow.Size;var scale=Root.XamlRoot.RasterizationScale;
            AppWindow.Resize(new Windows.Graphics.SizeInt32((int)(900*scale),(int)(680*scale)));
            await Task.Delay(250);Root.UpdateLayout();
            if(archivePropertiesPanel.ActualWidth!=240||archivePropertiesPanel.Visibility!=Visibility.Visible||archiveTableScroll!.ViewportWidth<=0||archiveTableScroll.ScrollableWidth<=0)throw new IOException("Narrow layout hid the archive sidebar or lost horizontal table scrolling.");
            AppWindow.Resize(windowSize);
            report["ArchiveView"]="sort preserves selection; folder navigation preserves ListView; empty search; decoded file type icons";
            report["ArchiveProperties"]="whole-archive totals remain stable during selection, navigation and search; actual archive file size; no Properties button; 240 DIP sidebar with a vertical divider remains visible at 900 DIP, with horizontal table scrolling";
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
                if(host?.Children.Any(child=>entry.IsText?child is TextBox:child is ScrollViewer)==true)break;
            }
            if(host?.Children.Count!=1||!(entry.IsText?host.Children[0] is TextBox:host.Children[0] is ScrollViewer))throw new IOException("Preview did not load its content.");
            var content=host.Children[0];
            await ShowPreviewAsync(entry);
            if(!ReferenceEquals(window,previewWindowPointer))throw new IOException("Repeated activation created a second preview window.");
            if(entry.IsText)
            {
                if(content is not TextBox text||text.Text.Length==0)throw new IOException("Text preview is empty.");
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
