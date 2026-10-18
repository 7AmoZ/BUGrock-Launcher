using System.Windows;
using System.Windows.Input;

namespace MCLauncher
{
    public partial class VersionListEndpointDialog : Window
    {
        public event SetEndpointHandler? OnEndpointChanged;

        public delegate void SetEndpointHandler(object sender, string newUwpIdsEndpoint, string newGdkPackageUrlsEndpoint);

        public VersionListEndpointDialog(string currentEndpoint) {
            InitializeComponent();
            EndpointTextBox.Text = currentEndpoint;
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void okButton_Click(object sender, RoutedEventArgs e) {
            OnEndpointChanged?.Invoke(this, EndpointTextBox.Text, GDKUrlsEndpointTextBox.Text);
            Close();
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e) {
            Close();
        }
    }
}
