using System;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace BUGrockLauncher
{
    public partial class InstallLocationDialog : Window
    {
        public string SelectedPath { get; private set; }
        private readonly string _defaultPath;
        private readonly string _versionName;

        public InstallLocationDialog(string versionDisplayName, string defaultPath, string versionName)
        {
            InitializeComponent();
            _defaultPath = defaultPath;
            _versionName = versionName;
            VersionNameText.Text = versionDisplayName;
            DefaultPathText.Text = defaultPath;
            TxtCustomPath.Text = defaultPath;
            SelectedPath = defaultPath;
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void DefaultCard_Click(object sender, MouseButtonEventArgs e)
        {
            RbDefault.IsChecked = true;
        }

        private void CustomCard_Click(object sender, MouseButtonEventArgs e)
        {
            RbCustom.IsChecked = true;
        }

        private void RbDefault_Checked(object sender, RoutedEventArgs e)
        {
            if (TxtCustomPath != null) TxtCustomPath.IsEnabled = false;
            if (BtnBrowse != null) BtnBrowse.IsEnabled = false;
        }

        private void RbCustom_Checked(object sender, RoutedEventArgs e)
        {
            if (TxtCustomPath != null) TxtCustomPath.IsEnabled = true;
            if (BtnBrowse != null) BtnBrowse.IsEnabled = true;
        }

        private void TxtCustomPath_GotFocus(object sender, RoutedEventArgs e)
        {
            RbCustom.IsChecked = true;
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            RbCustom.IsChecked = true;
            try
            {
                var dlg = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "Select Destination Folder for " + _versionName,
                    Multiselect = false
                };
                if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.FolderName))
                {
                    string chosen = dlg.FolderName;
                    string folderName = Path.GetFileName(chosen);
                    if (!folderName.Contains(_versionName, StringComparison.OrdinalIgnoreCase))
                    {
                        chosen = Path.Combine(chosen, "Minecraft-" + _versionName);
                    }
                    TxtCustomPath.Text = chosen;
                }
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show("Failed to open folder picker: " + ex.Message, "Error");
            }
        }

        private void BtnInstall_Click(object sender, RoutedEventArgs e)
        {
            if (RbDefault.IsChecked == true)
            {
                SelectedPath = _defaultPath;
            }
            else
            {
                string custom = TxtCustomPath.Text?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(custom))
                {
                    ThemedMessageBox.Show("Please choose or enter a valid installation folder path.", "Invalid Path");
                    return;
                }
                try
                {
                    SelectedPath = Path.GetFullPath(custom);
                }
                catch (Exception ex)
                {
                    ThemedMessageBox.Show("The specified installation path is invalid:\n" + ex.Message, "Invalid Path");
                    return;
                }
            }

            DialogResult = true;
            Close();
        }
    }
}
