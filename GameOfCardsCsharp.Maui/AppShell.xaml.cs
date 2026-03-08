namespace GameOfCardsCsharp.Maui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        
        // Set platform-specific content template
#if ANDROID
        TablicGameShellContent.ContentTemplate = new DataTemplate(typeof(TablicGamePageAndroid));
#else
        TablicGameShellContent.ContentTemplate = new DataTemplate(typeof(TablicGamePage));
#endif
    }
}
