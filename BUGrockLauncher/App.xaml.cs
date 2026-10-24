using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace BUGrockLauncher {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {

        protected override async void OnStartup(StartupEventArgs e) {
            base.OnStartup(e);
            Trace.Listeners.Add(new TextWriterTraceListener("Log.txt"));
            Trace.AutoFlush = true;

            DispatcherUnhandledException += (sender, args) => {
                Trace.WriteLine("Unhandled Dispatcher Exception:\n" + args.Exception);
                try {
                    ThemedMessageBox.Show("An unexpected error occurred:\n" + args.Exception.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                } catch { }
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, args) => {
                Trace.WriteLine("Unhandled Domain Exception:\n" + args.ExceptionObject);
            };

            var splash = new SplashScreenWindow();
            splash.Show();

            var main = new MainWindow();

            // Wait for ServersWebView to load in background before displaying MainWindow
            await main.WaitForServersReadyAsync(splash);

            // Display fully loaded MainWindow and close splash
            main.Show();
            splash.Close();
        }
    }
}
