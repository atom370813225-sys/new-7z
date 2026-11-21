using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using New7z.Core;
using Windows.ApplicationModel.DataTransfer;
using static New7z.Core.Loc;

namespace New7z.App;

public sealed partial class MainWindow
{
    private ArchiveSortColumn sortColumn=ArchiveSortColumn.Name;
    private bool sortDescending,searchComposing;
    private int visibleRowCount;
    private bool updatingRows;
    private bool selectionUpdateQueued;
    private string? cachedFolder;
    private IReadOnlyList<ArchiveEntry>? cachedEntries;
    private IReadOnlyList<ArchiveEntry> cachedFolderRows=[];
    private readonly Dictionary<ArchiveSortColumn,Button> sortHeaders=[];
    private double nameColumnWidth,sizeColumnWidth=100,packedColumnWidth=100,modifiedColumnWidth=145;
    private bool nameColumnFixed;
    private ColumnDefinition? nameColumn,sizeColumn,packedColumn,modifiedColumn;
    // A managed XAML subclass is not marshable in the trimmed build, and WinUI reapplies
    // its own cursor after pointer events, so the resize cursor is assigned through the
    // protected property of the plain element instead.
    private static readonly System.Reflection.PropertyInfo? ProtectedCursorProperty=typeof(UIElement).GetProperty("ProtectedCursor",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public);
    private static readonly Microsoft.UI.Input.InputSystemCursor? ResizeCursor=CreateResizeCursor();
    private static Microsoft.UI.Input.InputSystemCursor? CreateResizeCursor()
    {
        try{return Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeWestEast);}catch{return null;}
    }
    // The cursor is applied only while the pointer is on the divider and cleared on exit,
    // so it cannot linger over the rows after the pointer leaves the header.
    private static void SetHandleCursor(FrameworkElement handle,bool resize)
    {
        try{ProtectedCursorProperty?.SetValue(handle,resize?ResizeCursor:null);}catch{}
    }
    private TextBlock? folderLabel,archiveFileCountText,archiveOriginalSizeText,archivePackedSizeText;
    private Border? archivePropertiesPanel;
    private ScrollViewer? archiveTableScroll;
    private CheckBox? selectAllCheck;
    private bool updatingSelectAll;
    private DispatcherTimer? searchTimer;
    private void SetFolder(string value)
    {
        folder=value;query="";
        if(folderLabel!=null)folderLabel.Text=T("根目录","Root")+(folder.Length>0?" / "+folder:"");
        if(searchBox!=null)searchBox.Text="";
        RefreshRows();
    }
    private void ScheduleSearch()
    {
        searchTimer?.Stop();
        if(searchComposing)return;
        if(searchBox?.Text.Length==0){query="";RefreshRows();return;}
        searchTimer??=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(180)};
        searchTimer.Tick-=_SearchTick;searchTimer.Tick+=_SearchTick;searchTimer.Start();
    }
    private void _SearchTick(object? sender,object e){searchTimer?.Stop();query=searchBox?.Text??"";RefreshRows();}
    private void UpdateSortHeaders()
    {
        foreach(var (column,button) in sortHeaders)
        {
            var label=column switch{ArchiveSortColumn.Size=>T("大小","Size"),ArchiveSortColumn.PackedSize=>T("压缩后","Packed"),ArchiveSortColumn.Modified=>T("修改时间","Modified"),_=>T("名称","Name")};
            button.Content=label+(column==sortColumn?(sortDescending?" ↓":" ↑"):"");
            AutomationProperties.SetName(button,label+(column==sortColumn?(sortDescending?T("，降序"," descending"):T("，升序"," ascending")):T("，排序"," sort")));
        }
    }
    private void UpdateSelectionDetails()
    {
        if(fileList==null||updatingRows)return;
        countLabel!.Text=visibleRowCount==0?T("当前目录没有匹配的项目。","No matching items in this folder."):T($"{visibleRowCount} 个项目 · 已选择 {fileList.SelectedItems.Count} 项",$"{visibleRowCount} items · {fileList.SelectedItems.Count} selected");
        SyncSelectAllCheck();
    }
    private void SetAllRowsSelected(bool selected)
    {
        if(updatingSelectAll||fileList==null)return;
        if(selected)fileList.SelectAll();else fileList.SelectedItems.Clear();
        UpdateSelectionDetails();
    }
    private void SyncSelectAllCheck()
    {
        if(selectAllCheck==null||fileList==null)return;
        var total=fileList.Items.Count;var selected=fileList.SelectedItems.Count;
        bool? state=total==0||selected==0?false:selected>=total?true:null;
        updatingSelectAll=true;
        try{selectAllCheck.IsEnabled=total>0;selectAllCheck.IsChecked=state;}
        finally{updatingSelectAll=false;}
    }
    private Border CreateArchivePropertiesPanel()
    {
        // These totals belong to the archive, independent of the visible folder,
        // search and selection. Recalculate only when the archive view is rebuilt.
        var panel=(Border)XamlReader.Load("""
        <Border xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" BorderBrush="{ThemeResource DividerStrokeColorDefaultBrush}" BorderThickness="1,0,0,0" Padding="20,4,0,0">
          <ScrollViewer HorizontalScrollMode="Disabled" HorizontalScrollBarVisibility="Disabled" VerticalScrollBarVisibility="Auto">
            <StackPanel Spacing="24" Padding="0,0,12,16">
              <StackPanel Spacing="8">
                <TextBlock x:Name="PropertiesHeading" FontSize="16" FontWeight="SemiBold" TextWrapping="Wrap"/>
                <TextBlock x:Name="ArchiveName" FontSize="12" Foreground="{ThemeResource TextFillColorSecondaryBrush}" TextWrapping="Wrap" IsTextSelectionEnabled="True"/>
              </StackPanel>
              <StackPanel Spacing="4">
                <TextBlock x:Name="FileCountLabel" FontSize="12" Foreground="{ThemeResource TextFillColorSecondaryBrush}" TextWrapping="Wrap"/>
                <TextBlock x:Name="FileCount" FontSize="18" FontWeight="SemiBold" TextWrapping="Wrap" IsTextSelectionEnabled="True"/>
              </StackPanel>
              <StackPanel Spacing="4">
                <TextBlock x:Name="OriginalSizeLabel" FontSize="12" Foreground="{ThemeResource TextFillColorSecondaryBrush}" TextWrapping="Wrap"/>
                <TextBlock x:Name="OriginalSize" FontSize="18" FontWeight="SemiBold" TextWrapping="Wrap" IsTextSelectionEnabled="True"/>
              </StackPanel>
              <StackPanel Spacing="4">
                <TextBlock x:Name="PackedSizeLabel" FontSize="12" Foreground="{ThemeResource TextFillColorSecondaryBrush}" TextWrapping="Wrap"/>
                <TextBlock x:Name="PackedSize" FontSize="18" FontWeight="SemiBold" TextWrapping="Wrap" IsTextSelectionEnabled="True"/>
              </StackPanel>
              <StackPanel x:Name="CommentBlock" Spacing="4" Visibility="Collapsed">
                <TextBlock x:Name="CommentLabel" FontSize="12" Foreground="{ThemeResource TextFillColorSecondaryBrush}" TextWrapping="Wrap"/>
                <TextBlock x:Name="CommentText" FontSize="12" TextWrapping="Wrap" IsTextSelectionEnabled="True"/>
              </StackPanel>
            </StackPanel>
          </ScrollViewer>
        </Border>
        """);
        TextBlock Label(string name,string value){var label=(TextBlock)panel.FindName(name);label.Text=value;return label;}
        var heading=Label("PropertiesHeading",T("压缩包属性","Archive properties"));
        AutomationProperties.SetHeadingLevel(heading,Microsoft.UI.Xaml.Automation.Peers.AutomationHeadingLevel.Level2);
        Label("ArchiveName",System.IO.Path.GetFileName(archive)!);
        var countLabel=Label("FileCountLabel",T("文件数量","Files"));
        var originalLabel=Label("OriginalSizeLabel",T("原始大小","Original size"));
        var packedLabel=Label("PackedSizeLabel",T("压缩后","Packed"));
        Label("CommentLabel",T("备注","Comment"));
        BigInteger size=0;var files=0;
        foreach(var entry in entries.Where(e=>!e.IsDirectory)){files++;size+=entry.Size;}
        archiveFileCountText=Label("FileCount",files.ToString("N0"));
        archiveOriginalSizeText=Label("OriginalSize",size<=ulong.MaxValue?ByteSize.Format((ulong)size):size.ToString()+" B");
        // The whole archive size includes headers and works for solid archives,
        // whose individual entries may not have a packed-size value.
        string packedSize;
        try{packedSize=ByteSize.Format((ulong)ArchiveVolumes.Length(archive!));}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){packedSize=T("无法读取","Unavailable");}
        archivePackedSizeText=Label("PackedSize",packedSize);
        AutomationProperties.SetLabeledBy(archiveFileCountText,countLabel);
        AutomationProperties.SetLabeledBy(archiveOriginalSizeText,originalLabel);
        AutomationProperties.SetLabeledBy(archivePackedSizeText,packedLabel);
        return panel;
    }
    // ZIP and RAR archives can carry an archive-level comment; show it with the sizes.
    private async Task LoadArchiveCommentAsync(Border panel)
    {
        var current=archive;
        if(current==null)return;
        string comment;
        try{comment=await engine.CommentAsync(current,password);}catch{return;}
        if(!ReferenceEquals(panel,archivePropertiesPanel)||!string.Equals(archive,current,StringComparison.OrdinalIgnoreCase))return;
        if(string.IsNullOrWhiteSpace(comment))return;
        var block=(StackPanel)panel.FindName("CommentBlock");
        var text=(TextBlock)panel.FindName("CommentText");
        text.Text=comment;
        block.Visibility=Visibility.Visible;
    }
    private void QueueSelectionDetails()
    {
        if(updatingRows||selectionUpdateQueued)return;selectionUpdateQueued=true;
        DispatcherQueue.TryEnqueue(()=>{selectionUpdateQueued=false;UpdateSelectionDetails();});
    }
    private DataTemplate CreateRowTemplate()
    {
        const string markup="""
        <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"><Grid MinHeight="48" ColumnSpacing="12" Padding="4,5"><Grid.ColumnDefinitions><ColumnDefinition Width="26"/><ColumnDefinition Width="__NAME__"/><ColumnDefinition Width="__SIZE__"/><ColumnDefinition Width="__PACKED__"/><ColumnDefinition Width="__MODIFIED__"/></Grid.ColumnDefinitions><FontIcon Glyph="{Binding Glyph}" Visibility="{Binding FolderIconVisibility}" FontSize="20" Foreground="{ThemeResource AccentTextFillColorPrimaryBrush}"/><Image Source="{Binding Icon}" Visibility="{Binding FileIconVisibility}" Width="24" Height="24" Stretch="Uniform"/><TextBlock Grid.Column="1" Text="{Binding Name}" VerticalAlignment="Center" TextTrimming="CharacterEllipsis"/><TextBlock Grid.Column="2" Text="{Binding SizeText}" Margin="8,0,0,0" VerticalAlignment="Center" FontSize="12"/><TextBlock Grid.Column="3" Text="{Binding PackedSizeText}" Margin="8,0,0,0" VerticalAlignment="Center" FontSize="12"/><TextBlock Grid.Column="4" Text="{Binding ModifiedText}" Margin="8,0,0,0" VerticalAlignment="Center" FontSize="12"/></Grid></DataTemplate>
        """;
        static string Width(double value)=>value.ToString("0",System.Globalization.CultureInfo.InvariantCulture);
        var nameWidth=nameColumnFixed?Width(nameColumnWidth):"*";
        return (DataTemplate)XamlReader.Load(markup.Replace("__NAME__",nameWidth).Replace("__SIZE__",Width(sizeColumnWidth)).Replace("__PACKED__",Width(packedColumnWidth)).Replace("__MODIFIED__",Width(modifiedColumnWidth)));
    }
    // Dragging the boundary resizes the column it ends; rows update live through their
    // realized containers, and the template is rebuilt on release so future rows match.
    private void AddColumnResize(Grid header,int columnIndex,ColumnDefinition column,Action<double> store,double min,double max)
    {
        var line=new Microsoft.UI.Xaml.Shapes.Rectangle{Width=1,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Stretch,Fill=(Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],Opacity=0.7,IsHitTestVisible=false};
        var handle=new Grid{Width=16,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Stretch,Margin=new Thickness(0,0,-14,0),Background=new SolidColorBrush(Microsoft.UI.Colors.Transparent)};
        handle.Children.Add(line);
        ToolTipService.SetToolTip(handle,T("拖动调整列宽","Drag to resize the column"));
        AutomationProperties.SetName(handle,T("调整列宽","Resize column"));
        Grid.SetColumn(handle,columnIndex);header.Children.Add(handle);
        var dragging=false;var startX=0d;var startWidth=0d;
        var isName=ReferenceEquals(column,nameColumn);
        handle.PointerEntered+=(_,_)=>{line.Opacity=1;SetHandleCursor(handle,true);};
        handle.PointerExited+=(_,_)=>{if(!dragging){line.Opacity=0.7;SetHandleCursor(handle,false);}};
        handle.PointerPressed+=(_,e)=>{
            // Resizing is a left-button gesture; a right-click here opens no menu but must
            // not capture the pointer or start a drag.
            var pressed=e.GetCurrentPoint(header);
            if(!pressed.Properties.IsLeftButtonPressed)return;
            // A star column is pinned to its current width before the first drag so the
            // boundary can follow the pointer instead of the star absorbing the change.
            if(isName&&!nameColumnFixed){nameColumnFixed=true;nameColumnWidth=column.ActualWidth;column.Width=new GridLength(nameColumnWidth);}
            dragging=true;startX=pressed.Position.X;startWidth=column.ActualWidth;handle.CapturePointer(e.Pointer);line.Opacity=1;e.Handled=true;};
        handle.PointerMoved+=(_,e)=>{if(!dragging)return;var width=Math.Clamp(startWidth+e.GetCurrentPoint(header).Position.X-startX,min,max);column.Width=new GridLength(width);store(width);UpdateVisibleRowColumns();e.Handled=true;};
        handle.PointerReleased+=(_,e)=>{if(!dragging)return;dragging=false;handle.ReleasePointerCapture(e.Pointer);fileList!.ItemTemplate=CreateRowTemplate();line.Opacity=0.7;SetHandleCursor(handle,false);e.Handled=true;};
        handle.PointerCaptureLost+=(_,_)=>{if(!dragging)return;dragging=false;fileList!.ItemTemplate=CreateRowTemplate();line.Opacity=0.7;SetHandleCursor(handle,false);};
    }
    private void UpdateVisibleRowColumns()
    {
        if(fileList?.ItemsPanelRoot is not {} panel)return;
        foreach(var child in panel.Children)
            if(child is ListViewItem item&&FindRowGrid(item) is {} grid)
            {
                grid.ColumnDefinitions[1].Width=nameColumnFixed?new GridLength(nameColumnWidth):new GridLength(1,GridUnitType.Star);
                grid.ColumnDefinitions[2].Width=new GridLength(sizeColumnWidth);
                grid.ColumnDefinitions[3].Width=new GridLength(packedColumnWidth);
                grid.ColumnDefinitions[4].Width=new GridLength(modifiedColumnWidth);
            }
    }
    private static Grid? FindRowGrid(DependencyObject node)
    {
        if(node is Grid grid&&grid.ColumnDefinitions.Count==5)return grid;
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)
            if(FindRowGrid(VisualTreeHelper.GetChild(node,i)) is {} found)return found;
        return null;
    }
    private List<ArchiveRow> SelectedRowsInViewOrder()
    {
        return fileList?.Items.OfType<ArchiveRow>().Where(row=>fileList.SelectedItems.Contains(row)).ToList()??[];
    }
    // Template rows have no hit-testable background between columns and the ListViewItem
    // carries no DataContext, so resolve the row from the realized container bounds.
    private ArchiveRow? RowAt(Windows.Foundation.Point point)
    {
        if(fileList==null)return null;
        if(fileList.ItemsPanelRoot is {} panel)
            foreach(var child in panel.Children)
                if(child is ListViewItem item&&fileList.ItemFromContainer(item) is ArchiveRow row)
                {
                    var bounds=item.TransformToVisual(fileList).TransformBounds(new Windows.Foundation.Rect(0,0,item.ActualWidth,item.ActualHeight));
                    if(bounds.Contains(point))return row;
                }
        foreach(var element in VisualTreeHelper.FindElementsInHostCoordinates(point,fileList).OfType<DependencyObject>())
            for(var node=element;node!=null;node=VisualTreeHelper.GetParent(node))
                if(node is FrameworkElement { DataContext:ArchiveRow dataRow })return dataRow;
        return null;
    }
    private void PopulateRowMenu(MenuFlyout menu)
    {
        menu.Items.Clear();
        var rows=SelectedRowsInViewOrder();
        if(rows.Count==0)return;
        var single=rows.Count==1?rows[0]:null;
        var canModify=archive!=null&&ArchiveEngine.SupportsUpdate(archive);
        MenuFlyoutItem Item(string text,Func<Task> action,string? shortcut=null)
        {
            var item=new MenuFlyoutItem{Text=text};
            if(shortcut!=null)item.KeyboardAcceleratorTextOverride=shortcut;
            item.Click+=async(_,_)=>await action();
            return item;
        }
        var extractLabel=single==null?T($"解压选中的 {rows.Count} 项…",$"Extract {rows.Count} selected items…"):T("解压到…","Extract to…");
        if(single is { Entry.IsDirectory:false })
        {
            var entry=single.Entry;
            if(IsInstallerEntry(entry))menu.Items.Add(Item(T("运行","Run"),()=>RunEntryAsync(entry)));
            else
            {
                var preview=Item(T("预览","Preview"),()=>ActivateRowAsync(entry));
                preview.IsEnabled=!entry.IsLink&&(entry.IsImage||entry.IsText);
                if(!preview.IsEnabled)ToolTipService.SetToolTip(preview,entry.IsLink?T("链接条目暂不支持预览。","Link entries cannot be previewed yet."):T("仅支持图片与纯文本文件；其他类型请先解压，再用对应应用打开。","Only images and plain text can be previewed; extract other files and open them in their app."));
                menu.Items.Add(preview);
            }
        }
        menu.Items.Add(Item(extractLabel,()=>ExtractDialogAsync(SelectedEntries())));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(Item(T("复制","Copy"),CopySelectedAsync,"Ctrl+C"));
        menu.Items.Add(Item(T("复制文件名","Copy file name"),()=>CopySelectedTextAsync(false)));
        menu.Items.Add(Item(T("复制包内路径","Copy archive path"),()=>CopySelectedTextAsync(true)));
        menu.Items.Add(new MenuFlyoutSeparator());
        var delete=Item(T("删除","Delete"),DeleteSelectedAsync,"Del");
        delete.IsEnabled=canModify;
        if(!canModify)ToolTipService.SetToolTip(delete,T("当前格式只能读取；仅 7z 和 ZIP 支持修改。","This format is read-only; only 7z and ZIP can be modified."));
        menu.Items.Add(delete);
        menu.Items.Add(Item(single==null?T("查看所选属性","Properties of selection"):T("属性","Properties"),ShowSelectionPropertiesAsync));
    }
    // The menu popup is larger than the painted menu by its shadow (about 2 DIP above,
    // 10 beside, 18 below). WinUI creates that window at the requested point before it
    // honors ExclusionRect, so the point itself must already leave the pointer outside
    // the shadow. Otherwise the pointer sits on the popup and Windows shows wait or a
    // resize arrow until the mouse moves.
    private const double MenuClearBeside=14,MenuClearAbove=8,MenuClearBelow=24,MenuExclusion=24;
    internal readonly record struct MenuAnchor(double X,double Y,bool Below,bool ToRight,double ExclusionX,double ExclusionY,double ExclusionSize);
    internal static MenuAnchor PlaceMenuClearOfPointer(double x,double y,double width,double height)
    {
        var below=y+MenuClearBelow+48<=height||y<height/2;
        var toRight=x+MenuClearBeside+48<=width||x<width/2;
        return new(toRight?x+MenuClearBeside:x-MenuClearBeside,below?y+MenuClearAbove:y-MenuClearBelow,below,toRight,x-MenuExclusion,y-MenuExclusion,MenuExclusion*2);
    }
    internal static bool MenuPointerStaysClear(double x,double y,double width,double height)
    {
        var place=PlaceMenuClearOfPointer(x,y,width,height);
        var outside=place.ToRight?x<place.X-10:x>place.X+10;
        outside&=place.Below?y<place.Y-2:y>place.Y+18;
        var exclusion=new Windows.Foundation.Rect(place.ExclusionX,place.ExclusionY,place.ExclusionSize,place.ExclusionSize);
        return outside&&exclusion.Contains(new Windows.Foundation.Point(x,y));
    }
    private void ShowRowMenuClearOfPointer(MenuFlyout menu,Windows.Foundation.Point point)
    {
        var width=fileList!.ActualWidth;var height=fileList.ActualHeight;
        if(fileList.XamlRoot is {} root)
        {
            var origin=fileList.TransformToVisual(null).TransformPoint(new Windows.Foundation.Point(0,0));
            width=Math.Max(width,root.Size.Width-origin.X);height=Math.Max(height,root.Size.Height-origin.Y);
        }
        var place=PlaceMenuClearOfPointer(point.X,point.Y,width,height);
        var placement=(place.Below,place.ToRight) switch
        {
            (true,true)=>FlyoutPlacementMode.BottomEdgeAlignedLeft,
            (true,false)=>FlyoutPlacementMode.BottomEdgeAlignedRight,
            (false,true)=>FlyoutPlacementMode.TopEdgeAlignedLeft,
            _=>FlyoutPlacementMode.TopEdgeAlignedRight
        };
        menu.ShowAt(fileList,new FlyoutShowOptions{Position=new Windows.Foundation.Point(place.X,place.Y),Placement=placement,ExclusionRect=new Windows.Foundation.Rect(place.ExclusionX,place.ExclusionY,place.ExclusionSize,place.ExclusionSize)});
    }
    private void ShowRowMenuFromKeyboard(MenuFlyout menu)
    {
        if(fileList?.SelectedItem is not ArchiveRow)return;
        PopulateRowMenu(menu);
        if(menu.Items.Count==0)return;
        if(fileList.ContainerFromItem(fileList.SelectedItem) is not FrameworkElement anchor)return;
        menu.ShowAt(anchor,new FlyoutShowOptions{Placement=FlyoutPlacementMode.BottomEdgeAlignedLeft,ExclusionRect=new Windows.Foundation.Rect(0,0,anchor.ActualWidth,anchor.ActualHeight)});
    }
    // Rows are copied in the displayed order, one line each; the archive path form
    // keeps the archive name so a pasted line points inside the package, not a file on disk.
    private static string TextForRows(IReadOnlyList<ArchiveRow> rows,bool archivePaths,string archiveName)
    {
        return string.Join(Environment.NewLine,rows.Select(row=>archivePaths?archiveName+"!/"+row.FullPath:row.Name));
    }
    private async Task CopySelectedTextAsync(bool archivePaths)
    {
        if(archive==null)return;
        var rows=SelectedRowsInViewOrder();
        if(rows.Count==0)return;
        var package=new DataPackage();
        package.SetText(TextForRows(rows,archivePaths,System.IO.Path.GetFileName(archive)!));
        try
        {
            // Clipboard access can race another app holding it; retry like the file copy path.
            for(var attempt=0;;attempt++)
            {
                try{Clipboard.SetContent(package);Clipboard.Flush();break;}
                catch(COMException) when(attempt<4){await Task.Delay(120);}
            }
            Notify(archivePaths?T($"已复制 {rows.Count} 项包内路径。",$"Copied {rows.Count} archive paths."):T($"已复制 {rows.Count} 项文件名。",$"Copied {rows.Count} file names."),InfoBarSeverity.Success);
        }
        catch(Exception ex){Notify(T("无法写入剪贴板：","Cannot write to the clipboard: ")+ex.Message,InfoBarSeverity.Error);}
    }
    private async Task ShowSelectionPropertiesAsync()
    {
        if(archive==null)return;
        var rows=SelectedRowsInViewOrder();
        if(rows.Count==0)return;
        var panel=Stack(10);
        foreach(var (label,value) in PropertiesLines(rows,entries,System.IO.Path.GetFileName(archive)!))
        {
            var row=new Grid{ColumnSpacing=12};
            row.ColumnDefinitions.Add(new(){Width=new GridLength(110)});
            row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
            row.Children.Add(new TextBlock{Text=label,FontSize=12,Opacity=0.65,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Top});
            var text=new TextBlock{Text=value,FontSize=12,TextWrapping=TextWrapping.Wrap,IsTextSelectionEnabled=true};
            Grid.SetColumn(text,1);row.Children.Add(text);panel.Children.Add(row);
        }
        var dialog=new ContentDialog{Title=rows.Count==1?T("属性","Properties"):T("所选属性","Selected items"),Content=new ScrollViewer{Content=panel,MaxHeight=460,MinWidth=420},CloseButtonText=T("关闭","Close"),XamlRoot=Root.XamlRoot,RequestedTheme=Root.RequestedTheme};
        await ShowDialogAsync(dialog);
    }
    private static IEnumerable<(string Label,string Value)> PropertiesLines(IReadOnlyList<ArchiveRow> rows,IReadOnlyList<ArchiveEntry> allEntries,string archiveName)
    {
        if(rows.Count==1)
        {
            var entry=rows[0].Entry;
            yield return (T("名称","Name"),rows[0].Name);
            yield return (T("包内路径","Archive path"),archiveName+"!/"+rows[0].FullPath);
            yield return (T("类型","Type"),entry.IsLink?T("链接","Link"):entry.IsDirectory?T("文件夹","Folder"):T("文件","File"));
            if(entry.IsDirectory)
            {
                var prefix=entry.NormalizedPath+"/";
                var children=allEntries.Where(item=>item.NormalizedPath!=entry.NormalizedPath&&item.NormalizedPath.StartsWith(prefix,StringComparison.Ordinal)).ToList();
                var files=children.Where(item=>!item.IsDirectory).ToList();
                ulong total=0;foreach(var file in files)total+=file.Size;
                yield return (T("包含","Contains"),T($"{files.Count} 个文件",$"{files.Count} files"));
                yield return (T("原始大小","Original size"),ByteSize.Format(total));
            }
            else
            {
                yield return (T("大小","Size"),ByteSize.Format(entry.Size));
                if(entry.PackedSize is {} packed)yield return (T("压缩后","Packed"),ByteSize.Format(packed));
            }
            yield return (T("修改时间","Modified"),entry.ModifiedText);
            if(entry.Encrypted)yield return (T("加密","Encrypted"),T("是","Yes"));
            yield break;
        }
        var selectedFiles=rows.Where(row=>!row.Entry.IsDirectory&&!row.Entry.IsLink).ToList();
        ulong selectedSize=0;foreach(var row in selectedFiles)selectedSize+=row.Entry.Size;
        ulong? knownPacked=null;foreach(var row in selectedFiles)if(row.Entry.PackedSize is {} packed)knownPacked=(knownPacked??0)+packed;
        yield return (T("已选择","Selected"),T($"{rows.Count} 项",$"{rows.Count} items"));
        yield return (T("文件","Files"),T($"{selectedFiles.Count} 个文件，{rows.Count-selectedFiles.Count} 个文件夹",$"{selectedFiles.Count} files, {rows.Count-selectedFiles.Count} folders"));
        yield return (T("所选文件原始大小","Size of selected files"),ByteSize.Format(selectedSize));
        if(knownPacked is {} packedTotal)yield return (T("压缩后（已知项）","Packed (known items)"),ByteSize.Format(packedTotal));
        var more=rows.Count>10?Environment.NewLine+T($"…共 {rows.Count} 项",$"…{rows.Count} items"):"";
        yield return (T("包内路径","Archive paths"),string.Join(Environment.NewLine,rows.Take(10).Select(row=>row.FullPath))+more);
    }
}
