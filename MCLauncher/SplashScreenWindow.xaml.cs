using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace MCLauncher {
    public partial class SplashScreenWindow : Window {
        public SplashScreenWindow() {
            InitializeComponent();
            Loaded += SplashScreenWindow_Loaded;
        }

        private void SplashScreenWindow_Loaded(object sender, RoutedEventArgs e) {
            var storyboard = TryFindResource("PulseStoryBoard") as Storyboard;
            storyboard?.Begin();
        }

        public void SetStatus(string message) {
            Dispatcher.Invoke(() => {
                StatusText.Text = message;
            });
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.ButtonState == MouseButtonState.Pressed) {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) {
            Application.Current.Shutdown();
        }
    }
}
