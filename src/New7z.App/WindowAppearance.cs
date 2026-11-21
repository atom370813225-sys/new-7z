using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.UI;
namespace New7z.App;

internal static class WindowAppearance
{
    public static void ApplyIcon(Window window)=>window.AppWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory,"Assets","New7z.ico"));
    public static void ApplyTheme(FrameworkElement root)=>root.RequestedTheme=UserSettings.Load().Theme switch
    {"浅色" or "light"=>ElementTheme.Light,"深色" or "dark"=>ElementTheme.Dark,_=>ElementTheme.Default};
    // Caption button colors come from the system app theme, not from the window's XAML theme.
    // A light window on a dark system otherwise gets near-white glyphs on a light title bar.
    public static void ApplyCaptionButtons(AppWindow window,ElementTheme theme)
    {
        if(!AppWindowTitleBar.IsCustomizationSupported())return;
        var dark=theme==ElementTheme.Dark;
        var foreground=dark?Colors.White:Colors.Black;
        var inactive=dark?Color.FromArgb(0x66,0xFF,0xFF,0xFF):Color.FromArgb(0x66,0,0,0);
        var hover=dark?Color.FromArgb(0x1A,0xFF,0xFF,0xFF):Color.FromArgb(0x1A,0,0,0);
        var pressed=dark?Color.FromArgb(0x33,0xFF,0xFF,0xFF):Color.FromArgb(0x33,0,0,0);
        var titleBar=window.TitleBar;
        titleBar.ButtonBackgroundColor=Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor=Colors.Transparent;
        titleBar.ButtonForegroundColor=foreground;
        titleBar.ButtonInactiveForegroundColor=inactive;
        titleBar.ButtonHoverBackgroundColor=hover;
        titleBar.ButtonHoverForegroundColor=foreground;
        titleBar.ButtonPressedBackgroundColor=pressed;
        titleBar.ButtonPressedForegroundColor=foreground;
    }
}
