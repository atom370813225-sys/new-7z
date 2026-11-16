namespace New7z.Core;

// Public shell-refresh entry point for app-side icon changes.
public static class ShellIntegration
{
    public static void NotifyAssociationsChanged()=>NativeMethods.NotifyShellAssociationsChanged();
}
