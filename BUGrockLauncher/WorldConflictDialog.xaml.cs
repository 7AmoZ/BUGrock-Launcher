using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace BUGrockLauncher {
    public partial class WorldConflictDialog : Window {
        private readonly string _targetFolder;
        private readonly Dictionary<string, int> _locations;

        public class LocationViewModel {
            public string Icon { get; set; } = "📁";
            public string FriendlyName { get; set; } = "";
            public string PathDisplay { get; set; } = "";
            public string FullPath { get; set; } = "";
            public string WorldCountText { get; set; } = "";
            public Brush BadgeBackground { get; set; } = Brushes.Transparent;
            public Brush BadgeBorder { get; set; } = Brushes.Transparent;
            public Brush BadgeForeground { get; set; } = Brushes.White;
        }

        public WorldConflictDialog(Dictionary<string, int> locations, string targetFolder) {
            InitializeComponent();
            _locations = locations;
            _targetFolder = targetFolder;

            PopulateLocations();
        }

        private void PopulateLocations() {
            var viewModels = new List<LocationViewModel>();

            var bc = new BrushConverter();
            Brush greenBg = (Brush)bc.ConvertFromString("#1C2E22")!;
            Brush greenBorder = (Brush)bc.ConvertFromString("#38A169")!;
            Brush greenFg = (Brush)bc.ConvertFromString("#38A169")!;

            Brush magentaBg = (Brush)bc.ConvertFromString("#2B1530")!;
            Brush magentaBorder = (Brush)bc.ConvertFromString("#D05CE3")!;
            Brush magentaFg = (Brush)bc.ConvertFromString("#D05CE3")!;

            Brush darkBg = (Brush)bc.ConvertFromString("#1A1B24")!;
            Brush darkBorder = (Brush)bc.ConvertFromString("#2E3040")!;
            Brush darkFg = (Brush)bc.ConvertFromString("#A0AEC0")!;

            var sorted = _locations.OrderByDescending(x => x.Value).ToList();

            foreach (var kvp in sorted) {
                string path = kvp.Key;
                int worlds = kvp.Value;

                var vm = new LocationViewModel {
                    FullPath = path,
                    WorldCountText = worlds == 1 ? "1 World" : $"{worlds} Worlds"
                };

                bool isTarget = string.Equals(path, _targetFolder, StringComparison.OrdinalIgnoreCase);

                if (isTarget) {
                    vm.Icon = "🎯";
                    vm.FriendlyName = "Current Target Storage (Active)";
                    vm.BadgeBackground = magentaBg;
                    vm.BadgeBorder = magentaBorder;
                    vm.BadgeForeground = magentaFg;
                } else if (worlds >= 10) {
                    vm.Icon = "⭐";
                    vm.FriendlyName = GetProfileName(path) + " (Main Save)";
                    vm.BadgeBackground = greenBg;
                    vm.BadgeBorder = greenBorder;
                    vm.BadgeForeground = greenFg;
                } else {
                    vm.Icon = "🎮";
                    vm.FriendlyName = GetProfileName(path);
                    vm.BadgeBackground = darkBg;
                    vm.BadgeBorder = darkBorder;
                    vm.BadgeForeground = darkFg;
                }

                vm.PathDisplay = ShortenPath(path);
                viewModels.Add(vm);
            }

            LocationsList.ItemsSource = viewModels;
        }

        private string GetProfileName(string path) {
            if (path.IndexOf("Packages\\Microsoft.MinecraftUWP", StringComparison.OrdinalIgnoreCase) >= 0) {
                return "Legacy UWP Package Folder";
            }
            if (path.IndexOf("TmpMinecraftLocalState", StringComparison.OrdinalIgnoreCase) >= 0) {
                return "Temporary Backup Folder";
            }
            var parts = path.TrimEnd('\\', '/').Split('\\', '/');
            if (parts.Length > 0) {
                string last = parts[parts.Length - 1];
                if (long.TryParse(last, out _)) {
                    string shortId = last.Length > 8 ? "..." + last.Substring(last.Length - 8) : last;
                    return $"Bedrock Profile ({shortId})";
                }
            }
            return "Minecraft Storage Folder";
        }

        private string ShortenPath(string path) {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (path.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase)) {
                return "~" + path.Substring(userProfile.Length);
            }
            return path;
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left) {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }

        private void PlayAnywayButton_Click(object sender, RoutedEventArgs e) {
            DialogResult = true;
            Close();
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e) {
            try {
                string folderToOpen = _targetFolder;
                if (!Directory.Exists(folderToOpen)) {
                    var best = _locations.OrderByDescending(x => x.Value).FirstOrDefault().Key;
                    if (!string.IsNullOrEmpty(best) && Directory.Exists(best)) {
                        folderToOpen = best;
                    }
                }
                if (Directory.Exists(folderToOpen)) {
                    Process.Start(new ProcessStartInfo("explorer.exe", folderToOpen) { UseShellExecute = true });
                } else {
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    Process.Start(new ProcessStartInfo("explorer.exe", appData) { UseShellExecute = true });
                }
            } catch (Exception ex) {
                Trace.WriteLine("Failed to open folder: " + ex.Message);
            }
        }
    }
}
