using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using New7z.Core;
using static New7z.Core.Loc;

namespace New7z.App;

// One field and validation contract for the main and Explorer create flows.
internal sealed class VolumeSizeField
{
    public StackPanel View {get;}=new(){Spacing=6};
    private readonly ComboBox choice;
    private readonly TextBox custom;
    private readonly TextBlock hint;
    private static readonly string[] Sizes=["", "10 MiB", "100 MiB", "500 MiB", "1 GiB", "2 GiB", "4 GiB"];
    public VolumeSizeField()
    {
        choice=new(){Header=T("分卷大小","Split into volumes"),ItemsSource=new[]{T("不分卷","No split"),"10 MiB","100 MiB","500 MiB","1 GiB","2 GiB","4 GiB",T("自定义…","Custom…")},SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch};
        custom=new(){PlaceholderText=T("例如 250 MiB 或 1.5 GiB","e.g. 250 MiB or 1.5 GiB"),Visibility=Visibility.Collapsed};
        AutomationProperties.SetName(custom,T("自定义分卷大小","Custom volume size"));
        hint=new(){Text=T("每卷最大大小；自定义至少 1 MiB。所有分卷放在同一文件夹，从 .001 打开。","Maximum size per volume; custom sizes start at 1 MiB. Keep all volumes together and open .001."),TextWrapping=TextWrapping.Wrap,FontSize=12};
        choice.SelectionChanged+=(_,_)=>{custom.Visibility=choice.SelectedIndex==Sizes.Length?Visibility.Visible:Visibility.Collapsed;};
        View.Children.Add(choice);View.Children.Add(custom);View.Children.Add(hint);
    }
    public void SetFormat(string format)
    {
        choice.IsEnabled=custom.IsEnabled=ArchiveVolumes.Supports(format);
        hint.Text=choice.IsEnabled?T("每卷最大大小；自定义至少 1 MiB。所有分卷放在同一文件夹，从 .001 打开。","Maximum size per volume; custom sizes start at 1 MiB. Keep all volumes together and open .001."):T("分卷压缩支持 7z、ZIP 和 TAR。","Split volumes support 7z, ZIP and TAR.");
    }
    public long Value
    {
        get
        {
            if(!choice.IsEnabled)return 0;
            try{if(choice.SelectedIndex==Sizes.Length&&string.IsNullOrWhiteSpace(custom.Text))throw new ArgumentException(T("请输入自定义分卷大小，例如 100 MiB。","Enter a custom volume size, e.g. 100 MiB."));return ArchiveVolumes.ParseSize(choice.SelectedIndex==Sizes.Length?custom.Text:Sizes[Math.Max(0,choice.SelectedIndex)]);}
            catch{(choice.SelectedIndex==Sizes.Length?(Control)custom:choice).Focus(FocusState.Programmatic);throw;}
        }
    }
}
