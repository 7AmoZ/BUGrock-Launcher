using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace MCLauncher {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {

        protected override async void OnStartup(StartupEventArgs e) {
            base.OnStartup(e);
            Trace.Listeners.Add(new TextWriterTraceListener("Log.txt"));
            Trace.AutoFlush = true;

            var splash = new SplashScreenWindow();
            splash.Show();

            var main = new MainWindow();

            // Allow smooth animation and initialization
            await Task.Delay(1800);

            main.Show();
            splash.Close();
        }
    }
}
