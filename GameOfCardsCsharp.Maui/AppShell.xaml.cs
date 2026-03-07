namespace GameOfCardsCsharp.Maui
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            
            // Register routes for navigation
            Routing.RegisterRoute("tablicgame", typeof(TablicGamePage));
        }
    }
}
