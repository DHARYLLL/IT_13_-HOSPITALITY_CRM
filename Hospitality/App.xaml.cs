namespace Hospitality
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            MainPage = new MainPage();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = base.CreateWindow(activationState);

            // Set window to maximized/fullscreen
            window.Title = "Hospitality";

#if WINDOWS
            window.Width = 1920;
            window.Height = 1080;
            window.X = 0;
            window.Y = 0;

            // Maximize the window on Windows
            window.Created += (s, e) =>
            {
                var platformWindow = window.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
                if (platformWindow != null)
                {
                    var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(
                        Microsoft.UI.Win32Interop.GetWindowIdFromWindow(
                            WinRT.Interop.WindowNative.GetWindowHandle(platformWindow)));

                    if (appWindow != null)
                    {
                        var displayArea = Microsoft.UI.Windowing.DisplayArea.Primary;
                        appWindow.MoveAndResize(displayArea.WorkArea);
                    }
                }
            };
#endif

            return window;
        }
    }
}
