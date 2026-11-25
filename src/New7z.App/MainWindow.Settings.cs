using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using New7z.Core;
using static New7z.Core.Loc;
namespace New7z.App;

public sealed partial class MainWindow
{
    private void AddCompressionSettings(StackPanel panel)
    {
        var formats=new[]{"7z","zip","tar","zst","lz4"};
        var section=Stack(12);
        section.Children.Add(Text(T("默认压缩格式","Default archive format"),18,true));
        var choice=new ComboBox{Header=T("新建压缩包时默认使用","Default for new archives"),ItemsSource=new[]{T("7z（LZMA2，压缩率最高）","7z (LZMA2, smallest)"),T("ZIP（兼容性最好）","ZIP (most compatible)"),T("TAR（不压缩，仅打包）","TAR (uncompressed archive)"),T("Zstandard（单文件）","Zstandard (single file)"),T("LZ4（单文件）","LZ4 (single file)")},SelectedIndex=Math.Max(0,Array.IndexOf(formats,UserSettings.NormalizeFormat(settings.DefaultFormat))),HorizontalAlignment=HorizontalAlignment.Stretch};
        choice.SelectionChanged+=(_,_)=>
        {
            var value=formats[choice.SelectedIndex];
            if(value==settings.DefaultFormat)return;
            settings.DefaultFormat=value;settings.Save();
        };
        section.Children.Add(choice);
        section.Children.Add(Text(T("用于主界面「新建压缩」和右键「新建压缩包…」的默认格式。快捷命令「压缩为 7Z / ZIP / TAR」始终按所选命令执行。","Used by New archive in the app and New archive… in Explorer. The Compress to 7Z / ZIP / TAR shortcuts always follow the chosen command."),12));
        section.Children.Add(Text(T("默认压缩方法","Default compression method"),18,true));
        section.Children.Add(MethodChoice("7z",settings.Default7zMethod,value=>{settings.Default7zMethod=value;settings.Save();}));
        section.Children.Add(MethodChoice("zip",settings.DefaultZipMethod,value=>{settings.DefaultZipMethod=value;settings.Save();}));
        section.Children.Add(Text(T("7z 和 ZIP 可分别选择新建时使用的方法；TAR 仅打包，不使用压缩方法。存储（不压缩）速度最快但体积最大；部分 ZIP 方法可能不被其他解压软件支持。","7z and ZIP can use different default methods. TAR only packs files and does not use a compression method. Store is fastest but largest; some ZIP methods may not open in other archivers."),12));
        panel.Children.Add(Card(section));
    }
    private static ComboBox MethodChoice(string format,string current,Action<string> save)
    {
        var choices=CompressionOptions.For(format);
        var normalized=CompressionMethods.Normalize(format,current)??CompressionMethods.Default(format);
        var combo=new ComboBox{Header=format=="7z"?T("7z 压缩方法","7z compression method"):T("ZIP 压缩方法","ZIP compression method"),ItemsSource=choices,SelectedIndex=Math.Max(0,choices.ToList().FindIndex(item=>item.Value==normalized)),HorizontalAlignment=HorizontalAlignment.Stretch};
        combo.SelectionChanged+=(_,_)=>{if(combo.SelectedItem is CompressionChoice value)save(value.Value);};
        return combo;
    }
    private void AddExtractionSettings(StackPanel panel)
    {
        var section=Stack(12);
        section.Children.Add(Text(T("解压位置","Extraction location"),18,true));
        var enabled=new ToggleSwitch{Header=T("解压到固定目录","Extract to a fixed folder"),IsOn=settings.UseDefaultExtractFolder,OnContent=T("使用","On"),OffContent=T("不使用","Off")};
        var path=Input(T("默认解压目录","Default extraction folder"),settings.DefaultExtractFolder);
        path.PlaceholderText=T("完整路径，例如 D:\\解压","Full path, for example D:\\Extracted");
        var browse=new Button{Content=T("选择文件夹","Choose folder"),Padding=new Thickness(14,7,14,7),HorizontalAlignment=HorizontalAlignment.Left};
        var subfolder=new CheckBox{Content=T("在该目录下新建与压缩包同名的子文件夹","Create a subfolder named after the archive"),IsChecked=settings.ExtractIntoSubfolder};
        var feedback=new InfoBar{IsOpen=false,IsClosable=false};
        section.Children.Add(enabled);section.Children.Add(path);section.Children.Add(browse);section.Children.Add(subfolder);
        section.Children.Add(Text(T("开启后，「解压到」、批量解压和右键「解压到…」「智能解压」会预先填入该目录，每次解压前仍可修改。关闭时使用压缩包所在的文件夹。","When on, the Extract to dialog, batch extraction and the Explorer Extract to… / Smart Extract commands start with this folder; it can still be changed for each extraction. When off, the archive's own folder is used."),12));
        section.Children.Add(feedback);
        panel.Children.Add(Card(section));
        var updating=false;
        void UpdateState(){path.IsEnabled=browse.IsEnabled=enabled.IsOn;subfolder.IsEnabled=enabled.IsOn;}
        void Report(string title,string message,InfoBarSeverity severity){feedback.Title=title;feedback.Message=message;feedback.Severity=severity;feedback.IsOpen=true;}
        // Only a rooted path is accepted; anything else would silently fall back at extraction time.
        bool SaveFolder(bool announce)
        {
            var text=path.Text?.Trim()??"";
            var normalized=UserSettings.NormalizeExtractFolder(text);
            if(text.Length>0&&normalized.Length==0)
            {
                path.Text=settings.DefaultExtractFolder;
                Report(T("默认解压目录无效","Invalid default extraction folder"),T("请填写完整路径，例如 D:\\解压。","Enter a full path, for example D:\\Extracted."),InfoBarSeverity.Error);
                return false;
            }
            var changed=normalized!=settings.DefaultExtractFolder;
            path.Text=normalized;settings.DefaultExtractFolder=normalized;settings.Save();
            if(announce||changed)Report(T("已保存","Saved"),normalized.Length==0?T("未填写目录时仍使用压缩包所在的文件夹。","Without a folder, the archive's own folder is used."):T("下次解压会预先填入该目录。","Extractions will start with this folder."),InfoBarSeverity.Success);
            return true;
        }
        enabled.Toggled+=(_,_)=>
        {
            if(updating)return;
            settings.UseDefaultExtractFolder=enabled.IsOn;settings.Save();
            if(enabled.IsOn&&settings.DefaultExtractFolder.Length==0)
                Report(T("尚未选择目录","No folder selected yet"),T("请选择或填写默认解压目录；在此之前仍按压缩包所在的文件夹解压。","Choose or enter the default folder; until then the archive's own folder is used."),InfoBarSeverity.Warning);
        };
        browse.Click+=async(_,_)=>
        {
            try
            {
                var selected=await PickFolderAsync();if(selected==null)return;
                var normalized=UserSettings.NormalizeExtractFolder(selected);
                if(normalized.Length==0){Report(T("无法使用该目录","That folder cannot be used"),selected,InfoBarSeverity.Error);return;}
                path.Text=normalized;settings.DefaultExtractFolder=normalized;settings.UseDefaultExtractFolder=true;settings.Save();
                updating=true;enabled.IsOn=true;updating=false;UpdateState();
                Report(T("已保存","Saved"),T("下次解压会预先填入该目录。","Extractions will start with this folder."),InfoBarSeverity.Success);
            }
            catch(Exception ex){Report(T("无法选择文件夹","Cannot choose folder"),ex.Message,InfoBarSeverity.Error);}
        };
        path.LostFocus+=(_,_)=>{if(enabled.IsOn)SaveFolder(false);};
        path.KeyDown+=(_,e)=>{if(e.Key==Windows.System.VirtualKey.Enter){e.Handled=true;if(enabled.IsOn)SaveFolder(true);}};
        subfolder.Click+=(_,_)=>{settings.ExtractIntoSubfolder=subfolder.IsChecked==true;settings.Save();};
        UpdateState();
    }
    private void AddExplorerSettings(StackPanel panel)
    {
        var store=new ExplorerPreferencesStore();ExplorerPreferences saved;
        try{saved=store.Read();}
        catch(Exception ex){panel.Children.Add(Card(Text(T("无法读取右键菜单设置：","Cannot read the Explorer menu settings: ")+ex.Message)));return;}
        var section=Stack(14);section.Children.Add(Text(T("资源管理器右键菜单","Explorer context menu"),18,true));
        var enabled=new ToggleSwitch{Header=T("显示 New7z 右键菜单","Show the New7z context menu"),IsOn=saved.MenuEnabled,OnContent=T("显示","Show"),OffContent=T("不显示","Hidden")};section.Children.Add(enabled);
        section.Children.Add(Text(T("选择命令后点击「应用菜单设置」。菜单会根据文件类型自动筛选。","Choose commands, then click Apply. Commands are filtered by file type."),12));
        var commands=Stack(6);section.Children.Add(commands);
        var choices=new List<(ExplorerCommands Flag,CheckBox Box)>();
        foreach(var (flag,label) in new[]{
            (ExplorerCommands.Open,T("用 New7z 打开","Open with New7z")),(ExplorerCommands.ExtractNamed,T("智能解压","Smart Extract")),
            (ExplorerCommands.ExtractTo,T("解压到…","Extract to…")),(ExplorerCommands.ExtractHere,T("解压到当前文件夹","Extract here")),(ExplorerCommands.Test,T("测试完整性","Test archive")),
            (ExplorerCommands.Create,T("新建压缩包…","New archive…")),(ExplorerCommands.Create7z,T("压缩为 7Z","Compress to 7Z")),(ExplorerCommands.CreateZip,T("压缩为 ZIP","Compress to ZIP")),(ExplorerCommands.CreateTar,T("压缩为 TAR","Compress to TAR"))})
        {
            var box=new CheckBox{Content=label,IsChecked=saved.Commands.HasFlag(flag)};commands.Children.Add(box);choices.Add((flag,box));
        }
        var save=Button(T("应用菜单设置","Apply menu settings"),(_,_)=>{} ,true);section.Children.Add(save);
        var repair=Button(T("修复右键菜单注册","Repair menu registration"),(_,_)=>{});if(!AppEnvironment.IsPackaged)section.Children.Add(repair);
        var feedback=new InfoBar{IsOpen=false,IsClosable=false};section.Children.Add(feedback);
        var popup=Stack(14);popup.Children.Add(Text(T("任务弹出窗口","Task popup window"),18,true));
        var closeMode=new ComboBox{Header=T("任务成功后","After a successful task"),ItemsSource=new[]{T("手动关闭（默认）","Close manually (default)"),T("自动关闭","Close automatically")},SelectedIndex=saved.AutoCloseOnSuccess?1:0,HorizontalAlignment=HorizontalAlignment.Stretch};popup.Children.Add(closeMode);
        popup.Children.Add(Text(T("自动关闭仅用于成功完成的任务。失败或取消时保留窗口，便于查看原因。","Automatic close only applies to successful tasks. Failed or cancelled tasks keep the window so you can see why."),12));
        var popupFeedback=new InfoBar{IsOpen=false,IsClosable=false};popup.Children.Add(popupFeedback);
        bool applying=false;
        repair.Click+=async(_,_)=>
        {
            repair.IsEnabled=false;feedback.IsOpen=false;
            try{await RunIntegrationAsync("Repair");feedback.Severity=InfoBarSeverity.Success;feedback.Title=T("兼容菜单已注册","Menu registration repaired");feedback.Message=T("右键文件或文件夹 → 显示更多选项 → New7z。已通知资源管理器刷新。","Right-click a file or folder → Show more options → New7z. Explorer has been asked to refresh.");}
            catch(Exception ex){feedback.Severity=InfoBarSeverity.Error;feedback.Title=T("无法修复菜单","Cannot repair the menu");feedback.Message=ex.Message;}
            finally{repair.IsEnabled=true;feedback.IsOpen=true;}
        };
        void UpdateDirty()
        {
            foreach(var item in choices)item.Box.IsEnabled=enabled.IsOn;
            var mask=choices.Aggregate(ExplorerCommands.None,(value,item)=>item.Box.IsChecked==true?value|item.Flag:value);
            save.IsEnabled=!applying&&(enabled.IsOn!=saved.MenuEnabled||mask!=saved.Commands);
        }
        enabled.Toggled+=(_,_)=>UpdateDirty();foreach(var item in choices){item.Box.Checked+=(_,_)=>UpdateDirty();item.Box.Unchecked+=(_,_)=>UpdateDirty();}
        save.Click+=(_,_)=>
        {
            if(applying)return;applying=true;save.IsEnabled=false;feedback.IsOpen=false;
            try
            {
                var mask=choices.Aggregate(ExplorerCommands.None,(value,item)=>item.Box.IsChecked==true?value|item.Flag:value);
                if(enabled.IsOn&&mask==ExplorerCommands.None)throw new IOException(T("请至少勾选一个命令，或关闭 New7z 右键菜单。","Select at least one command, or turn the New7z menu off."));
                // Preserve a popup policy that may have been changed in another window.
                var updated=store.Read() with{MenuEnabled=enabled.IsOn,Commands=mask};store.Save(updated);saved=updated;
                feedback.Severity=InfoBarSeverity.Success;feedback.Title=T("菜单设置已保存","Menu settings saved");feedback.Message=T("下次打开右键菜单时生效，部分窗口可能需要重新打开。","Takes effect the next time the menu opens; some windows may need to be reopened.");
            }
            catch(Exception ex){feedback.Severity=InfoBarSeverity.Error;feedback.Title=T("未能应用菜单设置","Could not apply the menu settings");feedback.Message=ex.Message;}
            finally{applying=false;feedback.IsOpen=true;UpdateDirty();}
        };
        bool restoring=false;
        closeMode.SelectionChanged+=(_,_)=>
        {
            if(restoring)return;
            try
            {
                var current=store.Read() with{AutoCloseOnSuccess=closeMode.SelectedIndex==1};store.Save(current);saved=saved with{AutoCloseOnSuccess=current.AutoCloseOnSuccess};
                popupFeedback.Severity=InfoBarSeverity.Success;popupFeedback.Title=T("关闭方式已保存","Close behavior saved");popupFeedback.Message=current.AutoCloseOnSuccess?T("成功后自动关闭；失败和取消时保留。","Closes after success; failed and cancelled tasks stay open."):T("任务结束后保留窗口，由你手动关闭。","The window stays open after a task so you can close it yourself.");
            }
            catch(Exception ex)
            {
                restoring=true;closeMode.SelectedIndex=saved.AutoCloseOnSuccess?1:0;restoring=false;
                popupFeedback.Severity=InfoBarSeverity.Error;popupFeedback.Title=T("未能保存关闭方式","Could not save the close behavior");popupFeedback.Message=ex.Message;
            }
            popupFeedback.IsOpen=true;
        };
        UpdateDirty();panel.Children.Add(Card(section));panel.Children.Add(Card(popup));
        if(!AppEnvironment.IsPackaged)
        {
        var system=Stack(14);system.Children.Add(Text(T("Windows 自带压缩菜单","Windows built-in compress menu"),18,true));
        var systemStore=new WindowsCompressMenuSettingsStore();
        var hideSystemMenu=new ToggleSwitch{Header=T("隐藏 Windows「压缩到」菜单","Hide the Windows \"Compress to\" menu"),IsOn=saved.HideWindowsCompressMenu,OnContent=T("隐藏","Hidden"),OffContent=T("显示","Visible")};system.Children.Add(hideSystemMenu);
        var systemMenuSupported=OperatingSystem.IsWindowsVersionAtLeast(10,0,26100);
        hideSystemMenu.IsEnabled=systemMenuSupported;
        system.Children.Add(Text(systemMenuSupported
            ?T("适用于 Windows 11 24H2 及更新版本，仅影响当前用户。保存后会通知资源管理器刷新；如菜单仍显示，请重新启动资源管理器。","For Windows 11 24H2 and later; affects the current user only. Explorer is asked to refresh; restart Explorer if the menu still appears.")
            :T("此选项需要 Windows 11 24H2 或更新版本。","This option requires Windows 11 24H2 or later."),12));
        var systemFeedback=new InfoBar{IsOpen=false,IsClosable=false};system.Children.Add(systemFeedback);
        bool changingSystemMenu=false,restoringSystemMenu=false;
        hideSystemMenu.Toggled+=(_,_)=>
        {
            if(restoringSystemMenu||changingSystemMenu)return;
            changingSystemMenu=true;hideSystemMenu.IsEnabled=false;systemFeedback.IsOpen=false;
            try
            {
                var restored=systemStore.SetHidden(hideSystemMenu.IsOn);saved=saved with{HideWindowsCompressMenu=hideSystemMenu.IsOn};
                if(restored)
                {
                    systemFeedback.Severity=InfoBarSeverity.Success;systemFeedback.Title=hideSystemMenu.IsOn?T("Windows 压缩菜单已隐藏","The Windows compress menu is hidden"):T("New7z 已恢复修改前的 Windows 菜单设置","New7z restored the previous Windows menu setting");
                    systemFeedback.Message=T("已通知资源管理器刷新。重新打开右键菜单；如仍未更新，请重新启动资源管理器。","Explorer has been asked to refresh. Reopen the menu; restart Explorer if it still looks the same.");
                }
                else
                {
                    systemFeedback.Severity=InfoBarSeverity.Warning;systemFeedback.Title=T("已保留其他程序的设置","Another program's setting was kept");
                    systemFeedback.Message=T("检测到 Windows 菜单设置已被其他程序修改，New7z 保留了该值；菜单最终显示状态可能受其他设置影响。","The Windows menu setting was changed by another program, so New7z kept it; the final menu state may depend on other settings.");
                }
            }
            catch(Exception ex)
            {
                restoringSystemMenu=true;hideSystemMenu.IsOn=saved.HideWindowsCompressMenu;restoringSystemMenu=false;
                systemFeedback.Severity=InfoBarSeverity.Error;systemFeedback.Title=T("未能更新 Windows 压缩菜单设置","Could not update the Windows compress menu setting");systemFeedback.Message=ex.Message;
            }
            finally{changingSystemMenu=false;hideSystemMenu.IsEnabled=true;systemFeedback.IsOpen=true;}
        };
        panel.Children.Add(Card(system));
        }
    }
}
