using Microsoft.Maui.Platform;

namespace Simple_Player
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new MainPage());

            window.Width = 850;
            window.Height = 600;
            window.Title = "Simple Player";

#if WINDOWS
            window.Created += (s, e) =>
            {
                var nativeWindow = window.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
                if (nativeWindow != null)
                {
                    var appWindow = nativeWindow.GetAppWindow();
                    if (appWindow != null)
                    {
                        // Залишаємо стандартну поведінку Title Bar
                        appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                    }
                }
            };
#endif

            return window;
        }
    }
}