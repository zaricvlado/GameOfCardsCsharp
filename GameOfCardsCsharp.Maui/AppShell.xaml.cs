namespace GameOfCardsCsharp.Maui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

#if WINDOWS
        // Windows uses the desktop UI
        TablicGameShellContent.ContentTemplate = new DataTemplate(typeof(TablicGamePage));
#else
        // Android and iOS use the mobile UI
        TablicGameShellContent.ContentTemplate = new DataTemplate(typeof(TablicGamePageMobile));
#endif
    }
}
