using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace BUGrockLauncher {

    struct MinecraftPackageFamilies {
        public const string MINECRAFT = "Microsoft.MinecraftUWP_8wekyb3d8bbwe";
        public const string MINECRAFT_PREVIEW = "Microsoft.MinecraftWindowsBeta_8wekyb3d8bbwe";
    }

    namespace WPFDataTypes {

        public class NotifyPropertyChangedBase : INotifyPropertyChanged {

            public event PropertyChangedEventHandler? PropertyChanged;

            protected void OnPropertyChanged(string name) {
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs(name));
            }

        }

        public interface ICommonVersionCommands {

            ICommand LaunchCommand { get; }

            ICommand DownloadCommand { get; }

            ICommand RemoveCommand { get; }

        }

        public enum VersionType : int {
            Release = 0,
            Beta = 1,
            Preview = 2,
            Imported = 100
        }

        public enum PackageType {
            UWP,
            GDK
        }

        public class Version : NotifyPropertyChangedBase {
            public static readonly string UNKNOWN_UUID = "UNKNOWN";

            public Version(string uuid, string name, VersionType versionType, bool isNew, ICommonVersionCommands commands, PackageType packageType, List<string>? downloadUrls) {
                this.UUID = uuid;
                this.Name = name;
                this.VersionType = versionType;
                this.IsNew = isNew;
                this.DownloadCommand = commands.DownloadCommand;
                this.LaunchCommand = commands.LaunchCommand;
                this.RemoveCommand = commands.RemoveCommand;
                this.GameDirectory = (versionType == VersionType.Preview ? "Minecraft-Preview-" : "Minecraft-") + Name;
                this.PackageType = packageType;
                this.DownloadURLs = downloadUrls ?? new List<string>();
            }
            public Version(string name, string directory, ICommonVersionCommands commands, PackageType packageType) {
                this.UUID = UNKNOWN_UUID;
                this.Name = name;
                this.VersionType = VersionType.Imported;
                this.DownloadCommand = commands.DownloadCommand;
                this.LaunchCommand = commands.LaunchCommand;
                this.RemoveCommand = commands.RemoveCommand;
                this.GameDirectory = directory;
                this.PackageType = packageType;
                this.DownloadURLs = new List<string>();
            }

            public string UUID { get; set; }
            public string Name { get; set; }
            public VersionType VersionType { get; set; }
            public bool IsNew {
                get { return _isNew; }
                set {
                    _isNew = value;
                    OnPropertyChanged("IsNew");
                }
            }
            public bool IsImported {
                get => VersionType == VersionType.Imported;
            }

            public string DisplayVersionType => VersionType switch {
                VersionType.Preview => "PREVIEW",
                VersionType.Beta => "BETA",
                VersionType.Imported => "IMPORTED",
                _ => "RELEASE"
            };

            public string GameDirectory { get; set; }

            public PackageType PackageType { get; set; }

            public List<string> DownloadURLs { get; set; }

            public string GamePackageFamily {
                get => VersionType == VersionType.Preview ? MinecraftPackageFamilies.MINECRAFT_PREVIEW : MinecraftPackageFamilies.MINECRAFT;
            }

            public bool IsInstalled => Directory.Exists(GameDirectory);

            public string DisplayName {
                get {
                    string typeTag = "";
                    if (VersionType == VersionType.Beta)
                        typeTag = "(beta)";
                    else if (VersionType == VersionType.Preview)
                        typeTag = "(preview)";
                    string packageTypeTag = "";
                    if (PackageType == PackageType.GDK) {
                        packageTypeTag += "GDK";
                    } else if (PackageType == PackageType.UWP) {
                        packageTypeTag += "UWP";
                    }

                    return Name + " - " + packageTypeTag + (typeTag.Length > 0 ? " " + typeTag : "") + (IsNew ? " (NEW!)" : "");
                }
            }
            public string DisplayInstallStatus {
                get {
                    return IsInstalled ? "Installed" : "Not installed";
                }
            }

            public ICommand LaunchCommand { get; set; }
            public ICommand DownloadCommand { get; set; }
            public ICommand RemoveCommand { get; set; }

            private VersionStateChangeInfo? _stateChangeInfo;
            private bool _isNew = false;
            public VersionStateChangeInfo? StateChangeInfo {
                get { return _stateChangeInfo; }
                set { _stateChangeInfo = value; OnPropertyChanged("StateChangeInfo"); OnPropertyChanged("IsStateChanging"); }
            }

            public bool IsStateChanging => StateChangeInfo != null;

            public void UpdateInstallStatus() {
                OnPropertyChanged("IsInstalled");
            }

        }

        public enum VersionState {
            Initializing,
            Downloading,
            Extracting,
            Registering,
            Launching,
            Unregistering,
            CleaningUp,
            Staging,
            Decrypting,
            Moving,
            MovingData
        };

        public class VersionStateChangeInfo : NotifyPropertyChangedBase {

            private VersionState _versionState;

            private long _progress = 0;
            private long _maxProgress = 0;
            private readonly ChromeSpeedCalculator _speedCalculator = new(windowSizeSeconds: 5);
            private string _downloadSpeedText = "";

            private static readonly SolidColorBrush _greenBrush;
            private static readonly SolidColorBrush _greenBadgeBrush;
            private static readonly SolidColorBrush _magentaBrush;
            private static readonly SolidColorBrush _magentaBadgeBrush;

            static VersionStateChangeInfo() {
                _greenBrush = new SolidColorBrush(Color.FromRgb(0x38, 0xA1, 0x69));
                _greenBrush.Freeze();
                _greenBadgeBrush = new SolidColorBrush(Color.FromRgb(0x1C, 0x2E, 0x22));
                _greenBadgeBrush.Freeze();
                _magentaBrush = new SolidColorBrush(Color.FromRgb(0xD0, 0x5C, 0xE3));
                _magentaBrush.Freeze();
                _magentaBadgeBrush = new SolidColorBrush(Color.FromRgb(0x2B, 0x15, 0x30));
                _magentaBadgeBrush.Freeze();
            }

            public VersionStateChangeInfo(VersionState versionState) {
                _versionState = versionState;
                if (_versionState == VersionState.Downloading) {
                    _speedCalculator.Start();
                }
            }

            public VersionState VersionState {
                get { return _versionState; }
                set {
                    _versionState = value;
                    _progress = 0;
                    _maxProgress = 0;
                    if (_versionState == VersionState.Downloading) {
                        _speedCalculator.Start();
                    } else {
                        _speedCalculator.Reset();
                    }
                    _downloadSpeedText = "";
                    OnPropertyChanged("Progress");
                    OnPropertyChanged("MaxProgress");
                    OnPropertyChanged("DownloadSpeedText");
                    OnPropertyChanged("SpeedVisibility");
                    OnPropertyChanged("IsProgressIndeterminate");
                    OnPropertyChanged("DisplayStatus");
                    OnPropertyChanged("StateAccentBrush");
                    OnPropertyChanged("StateBadgeBackgroundBrush");
                }
            }

            public bool IsProgressIndeterminate {
                get {
                    return _maxProgress == 0;
                }
            }

            public string DownloadSpeedText {
                get => _downloadSpeedText;
                private set {
                    if (_downloadSpeedText != value) {
                        _downloadSpeedText = value;
                        OnPropertyChanged("DownloadSpeedText");
                        OnPropertyChanged("SpeedVisibility");
                    }
                }
            }

            public Visibility SpeedVisibility =>
                (_versionState == VersionState.Downloading && !string.IsNullOrEmpty(_downloadSpeedText))
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            public long Progress {
                get { return _progress; }
                set {
                    _progress = value;
                    if (_versionState == VersionState.Downloading) {
                        DownloadSpeedText = _speedCalculator.UpdateProgress(value);
                    }
                    OnPropertyChanged("Progress");
                    OnPropertyChanged("DisplayStatus");
                }
            }

            public long MaxProgress {
                get { return _maxProgress; }
                set {
                    _maxProgress = value;
                    OnPropertyChanged("MaxProgress");
                    OnPropertyChanged("DisplayStatus");
                    OnPropertyChanged("IsProgressIndeterminate");
                }
            }

            public string DisplayStatus {
                get {
                    switch (_versionState) {
                        case VersionState.Initializing: return "Preparing...";
                        case VersionState.Downloading:
                            if (MaxProgress > 0) {
                                double progMB = (double)Progress / (1024.0 * 1024.0);
                                double maxMB = (double)MaxProgress / (1024.0 * 1024.0);
                                int percent = (int)Math.Clamp((Progress * 100.0) / MaxProgress, 0, 100);
                                return $"Downloading... {progMB:0.0} MB / {maxMB:0.0} MB ({percent}%)";
                            }
                            return $"Downloading... {((double)Progress / (1024.0 * 1024.0)):0.0} MB";
                        case VersionState.Extracting:
                            if (MaxProgress > 0) {
                                int percent = (int)Math.Clamp((Progress * 100.0) / MaxProgress, 0, 100);
                                int remaining = 100 - percent;
                                return $"Extracting... {percent}% ({remaining}% remaining)";
                            }
                            return "Extracting...";
                        case VersionState.Registering: return "Registering package...";
                        case VersionState.Launching: return "Launching...";
                        case VersionState.Unregistering: return "Unregistering package...";
                        case VersionState.CleaningUp: return "Cleaning up...";
                        case VersionState.Staging:
                            if (MaxProgress > 0) {
                                int percent = (int)Math.Clamp((Progress * 100.0) / MaxProgress, 0, 100);
                                int remaining = 100 - percent;
                                return $"Staging package... {percent}% ({remaining}% remaining)";
                            }
                            return "Staging package... (this might take a few minutes)";
                        case VersionState.Decrypting: return "Copying decrypted Minecraft.Windows.exe...";
                        case VersionState.Moving: return "Copying other game files...";
                        case VersionState.MovingData: return "Restoring Minecraft worlds...";
                        default: return "Processing...";
                    }
                }
            }

            public Brush StateAccentBrush {
                get {
                    switch (_versionState) {
                        case VersionState.Extracting:
                        case VersionState.Staging:
                        case VersionState.Decrypting:
                        case VersionState.Moving:
                        case VersionState.Registering:
                        case VersionState.Launching:
                            return _greenBrush;
                        default:
                            return _magentaBrush;
                    }
                }
            }

            public Brush StateBadgeBackgroundBrush {
                get {
                    switch (_versionState) {
                        case VersionState.Extracting:
                        case VersionState.Staging:
                        case VersionState.Decrypting:
                        case VersionState.Moving:
                        case VersionState.Registering:
                        case VersionState.Launching:
                            return _greenBadgeBrush;
                        default:
                            return _magentaBadgeBrush;
                    }
                }
            }

            private ICommand? _cancelCommand = null;

            public ICommand? CancelCommand {
                get => _cancelCommand;
                set {
                    _cancelCommand = value;
                    OnPropertyChanged("CancelCommand");
                    OnPropertyChanged("HasCancelCommand");
                }
            }

            public bool HasCancelCommand {
                get => CancelCommand != null;
            }

        }

    }
}
