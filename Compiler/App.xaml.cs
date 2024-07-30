using Microsoft.UI.Xaml;

using WinUIEx;

namespace Compiler
{
    public partial class App : Application
    {
        public static MainWindow Window { get; private set; }

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            Window = new MainWindow();
            Window.CenterOnScreen();
            Window.Activate();
        }
    }
}
