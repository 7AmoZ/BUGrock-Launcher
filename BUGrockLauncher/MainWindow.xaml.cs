using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;

namespace BUGrockLauncher {
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Windows.Data;
    using System.Xml;
    using System.Xml.Linq;
    using Windows.ApplicationModel;
    using Windows.Foundation;
    using Windows.Management.Core;
    using Windows.Management.Deployment;
    using Windows.Storage;
    using Windows.System;
    using System.Text.RegularExpressions;
    using System.Windows.Controls;
    using WPFDataTypes;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, ICommonVersionCommands {
        private const string GDK_SHIM_NAME = @"GDKLaunchShim.exe";
        private const string GDK_DECRYPT_HELPER_NAME = @"GDKDecryptHelper.exe";
        private static readonly string PREFS_PATH = @"preferences.json";
        private static readonly string IMPORTED_VERSIONS_PATH = @"imported_versions";
        private static readonly string VERSIONS_API_UWP = "https://raw.githubusercontent.com/7AmoZ/BUGrock-Launcher/master/uwp_versions.json";
        private static readonly string VERSIONS_API_GDK = "https://raw.githubusercontent.com/MinecraftBedrockArchiver/GdkLinks/refs/heads/master/urls.min.json";

        private VersionList _versions;
        public Preferences UserPrefs { get; }

        private HashSet<CollectionViewSource> _versionListViews = new HashSet<CollectionViewSource>();

        private readonly VersionDownloader _anonVersionDownloader = new VersionDownloader();
        private readonly VersionDownloader _userVersionDownloader = new VersionDownloader();
        private readonly Task _userVersionDownloaderLoginTask;
        private volatile int _userVersionDownloaderLoginTaskStarted;
        private volatile bool _hasLaunchTask = false;
        private volatile bool _hasGdkExtractTask = false;
        private bool _isLoadingVersionList = false;

        public MainWindow() {
            Preferences? userPrefs = null;
            if (File.Exists(PREFS_PATH)) {
                try {
                    userPrefs = JsonConvert.DeserializeObject<Preferences>(File.ReadAllText(PREFS_PATH));
                } catch (Exception ex) {
                    Trace.WriteLine("Failed to parse preferences.json: " + ex.Message);
                }
            }
            if (userPrefs == null) {
                UserPrefs = new Preferences();
                RewritePrefs();
            } else {
                UserPrefs = userPrefs;
            }
            if (UserPrefs.CustomInstallDirectories == null) {
                UserPrefs.CustomInstallDirectories = new Dictionary<string, string>();
            }

            var versionsApiUWP = UserPrefs.VersionsApiUWP != "" ? UserPrefs.VersionsApiUWP : VERSIONS_API_UWP;
            var versionsApiGDK = UserPrefs.VersionsApiGDK != "" ? UserPrefs.VersionsApiGDK : VERSIONS_API_GDK;
            _versions = new VersionList("versions_uwp.json", IMPORTED_VERSIONS_PATH, versionsApiUWP, this, VersionEntryPropertyChanged, "versions_gdk.json", versionsApiGDK);

            InitializeComponent();
            DeleteAppxAfterDownloadOption.DataContext = this;
            AutoFetchDecryptionKeysOption.DataContext = this;
            ShowInstalledVersionsOnlyCheckbox.DataContext = this;

            var versionListViewRelease = Resources["versionListViewRelease"] as CollectionViewSource;
            versionListViewRelease.Filter += new FilterEventHandler((object sender, FilterEventArgs e) => {
                var v = e.Item as Version;
                e.Accepted = v.VersionType == VersionType.Release && (v.IsInstalled || v.IsStateChanging || !(ShowInstalledVersionsOnlyCheckbox.IsChecked ?? false));
            });
            versionListViewRelease.Source = _versions;
            ReleaseVersionList.DataContext = versionListViewRelease;
            _versionListViews.Add(versionListViewRelease);

            var versionListViewBeta = Resources["versionListViewBeta"] as CollectionViewSource;
            versionListViewBeta.Filter += new FilterEventHandler((object sender, FilterEventArgs e) => {
                var v = e.Item as Version;
                e.Accepted = v.VersionType == VersionType.Beta && (v.IsInstalled || v.IsStateChanging || !(ShowInstalledVersionsOnlyCheckbox.IsChecked ?? false));
            });
            versionListViewBeta.Source = _versions;
            BetaVersionList.DataContext = versionListViewBeta;
            _versionListViews.Add(versionListViewBeta);

            var versionListViewPreview = Resources["versionListViewPreview"] as CollectionViewSource;
            versionListViewPreview.Filter += new FilterEventHandler((object sender, FilterEventArgs e) => {
                var v = e.Item as Version;
                e.Accepted = v.VersionType == VersionType.Preview && (v.IsInstalled || v.IsStateChanging || !(ShowInstalledVersionsOnlyCheckbox.IsChecked ?? false));
            });
            versionListViewPreview.Source = _versions;
            PreviewVersionList.DataContext = versionListViewPreview;
            _versionListViews.Add(versionListViewPreview);

            var versionListViewImported = Resources["versionListViewImported"] as CollectionViewSource;
            versionListViewImported.Filter += new FilterEventHandler((object sender, FilterEventArgs e) => {
                var v = e.Item as Version;
                e.Accepted = v.VersionType == VersionType.Imported;
            });

            versionListViewImported.Source = _versions;
            ImportedVersionList.DataContext = versionListViewImported;
            _versionListViews.Add(versionListViewImported);

            _userVersionDownloaderLoginTask = new Task(() => {
                _userVersionDownloader.EnableUserAuthorization();
            });
            Dispatcher.Invoke(LoadVersionList);
            Loaded += (s, e) => {
                InitSteveHead();
                InitializeWebViews();
            };
        }

        #region Community Hub & DDFW Downloads
        private const string ANTI_COOKIE_AND_POPUP_SCRIPT = @"
            (function() {
                // 1. Enforce Strict Privacy & Do Not Track
                try {
                    Object.defineProperty(navigator, 'doNotTrack', { get: function() { return '1'; }, configurable: true });
                    Object.defineProperty(navigator, 'globalPrivacyControl', { get: function() { return true; }, configurable: true });
                } catch(e) {}

                // 2. Neutralize Analytics & Tracking Handlers to dummy functions (no data sent)
                try {
                    window.ga = function() {};
                    window.gtag = function() {};
                    window.dataLayer = { push: function() {} };
                    window.fbq = function() {};
                } catch(e) {}

                // 3. Set Strict Cookie Rejection / Opt-Out Flags
                try {
                    localStorage.setItem('cookie_consent', 'false');
                    localStorage.setItem('gdpr_consent', 'false');
                    localStorage.setItem('tracking_consent', 'denied');
                    localStorage.setItem('OptanonAlertBoxClosed', new Date().toISOString());
                    document.cookie = 'OptanonAlertBoxClosed=' + new Date().toISOString() + '; path=/; max-age=31536000';
                    document.cookie = 'cookie_consent=false; path=/; max-age=31536000';
                    document.cookie = 'cookieconsent_status=deny; path=/; max-age=31536000';
                    document.cookie = 'gdpr_consent=0; path=/; max-age=31536000';
                } catch(e) {}

                // 4. Actively find and trigger any 'Reject All' / 'Decline' buttons & purge banners
                function rejectAllAndPurge() {
                    // Specific known Reject buttons
                    var rejectSelectors = [
                        '#onetrust-reject-all-handler',
                        'button#didomi-notice-disagree-button',
                        'button[aria-label=""Reject all""]',
                        'button[aria-label=""Decline all""]',
                        '.qc-cmp2-buttons-desktop button:last-child',
                        'button.cmp-button-reject'
                    ];
                    for (var k = 0; k < rejectSelectors.length; k++) {
                        var rBtn = document.querySelector(rejectSelectors[k]);
                        if (rBtn && typeof rBtn.click === 'function') {
                            try { rBtn.click(); } catch(e) {}
                        }
                    }

                    // Scan general buttons for reject/decline text
                    var btns = document.querySelectorAll('button, a[role=""button""], input[type=""button""]');
                    for (var b = 0; b < btns.length; b++) {
                        var text = (btns[b].innerText || btns[b].value || '').trim().toLowerCase();
                        if (text === 'reject all' || text === 'decline all' || text === 'reject' || text === 'decline' || text === 'disagree' || text === 'رفض الكل' || text === 'رفض') {
                            try { btns[b].click(); } catch(e) {}
                        }
                    }

                    // Purge banner containers and overlays
                    var selectors = [
                        '#onetrust-consent-sdk',
                        '#onetrust-banner-sdk',
                        '.qc-cmp2-container',
                        '#cmpbox',
                        '#cmpbox2',
                        '.cookie-banner',
                        '.cookie-notice',
                        '.cookieConsent',
                        'div[class*=""cookie""]',
                        'div[id*=""cookie""]',
                        'div[class*=""consent""]',
                        'div[id*=""consent""]',
                        'div[class*=""didomi""]',
                        'div[class*=""sp_message_container""]',
                        '.fc-consent-root',
                        '#sp_message_container'
                    ];
                    for (var i = 0; i < selectors.length; i++) {
                        var els = document.querySelectorAll(selectors[i]);
                        for (var j = 0; j < els.length; j++) {
                            try {
                                els[j].style.setProperty('display', 'none', 'important');
                                els[j].style.setProperty('pointer-events', 'none', 'important');
                                els[j].remove();
                            } catch(e) {}
                        }
                    }

                    // Ensure document scrolling is not locked by consent overlays
                    if (document.body) {
                        document.body.style.setProperty('overflow', 'auto', 'important');
                        document.body.style.setProperty('pointer-events', 'auto', 'important');
                    }
                    if (document.documentElement) {
                        document.documentElement.style.setProperty('overflow', 'auto', 'important');
                    }
                }

                if (document.readyState === 'loading') {
                    document.addEventListener('DOMContentLoaded', rejectAllAndPurge);
                } else {
                    rejectAllAndPurge();
                }
                setInterval(rejectAllAndPurge, 400);
            })();
        ";

        private const string SERVERS_VIEW_POLISH_SCRIPT = @"
            (function() {
                try {
                    localStorage.setItem('cookie_consent', 'true');
                    localStorage.setItem('gdpr_consent', 'true');
                } catch(e) {}

                var style = document.createElement('style');
                style.innerHTML = `
                    header, footer, nav, .navbar, h1, .card.bg-light, .alert, .adsbygoogle, .banner-ads, #navbar,
                    #onetrust-consent-sdk, .qc-cmp2-container, div[class*='cookie'], div[id*='cookie'], div[class*='consent'] {
                        display: none !important;
                    }
                    html, body {
                        background-color: #12131A !important;
                        color: #E2E8F0 !important;
                        margin: 0 !important;
                        padding: 0 !important;
                        overflow-x: hidden !important;
                    }
                    .container, .container-fluid, .row, .col-12 {
                        padding-left: 2px !important;
                        padding-right: 2px !important;
                        margin-left: 0 !important;
                        margin-right: 0 !important;
                        max-width: 100% !important;
                    }
                    .table, .servers-table, table {
                        background-color: #151720 !important;
                        color: #E2E8F0 !important;
                        border-color: #232530 !important;
                        margin-top: 0 !important;
                    }
                    .table td, .table th, table td, table th {
                        background-color: #151720 !important;
                        color: #E2E8F0 !important;
                        border-color: #232530 !important;
                        padding: 4px 6px !important;
                    }
                    .table-striped tbody tr:nth-of-type(odd) {
                        background-color: #191B26 !important;
                    }
                    a {
                        color: #48BB78 !important;
                    }
                    .text-dark {
                        color: #E2E8F0 !important;
                    }
                    .text-muted {
                        color: #8E92A4 !important;
                    }
                    .btn-light2 {
                        background-color: #212433 !important;
                        color: #E2E8F0 !important;
                        border: 1px solid #2B2F42 !important;
                    }
                `;
                document.head.appendChild(style);

                function scrollToFirstServer() {
                    var serverTable = document.querySelector('.servers-table');
                    if (serverTable) {
                        serverTable.scrollIntoView({ behavior: 'instant', block: 'start' });
                    }
                }

                if (document.readyState === 'complete') {
                    scrollToFirstServer();
                } else {
                    window.addEventListener('load', scrollToFirstServer);
                }
                setTimeout(scrollToFirstServer, 200);
                setTimeout(scrollToFirstServer, 800);
                setTimeout(scrollToFirstServer, 2000);
            })();
        ";

        private static bool IsTrackerOrTelemetry(string uri) {
            if (string.IsNullOrEmpty(uri)) return false;
            string lower = uri.ToLowerInvariant();
            return lower.Contains("google-analytics.com") ||
                   lower.Contains("googletagmanager.com") ||
                   lower.Contains("doubleclick.net") ||
                   lower.Contains("adservice.google.") ||
                   lower.Contains("pagead2.googlesyndication.com") ||
                   lower.Contains("facebook.net") ||
                   lower.Contains("connect.facebook.net") ||
                   lower.Contains("criteo.com") ||
                   lower.Contains("outbrain.com") ||
                   lower.Contains("taboola.com") ||
                   lower.Contains("scorecardresearch.com") ||
                   lower.Contains("hotjar.com") ||
                   lower.Contains("clarity.ms") ||
                   lower.Contains("quantserve.com") ||
                   lower.Contains("moatads.com");
        }

        private string? _lastDownloadedFilePath;
        private string? _lastDownloadedFolder;

        private async void InitializeWebViews() {
            try {
                DDFWManager.EnsureDirectories();

                // 1. Initialize Servers WebView
                if (ServersWebView != null) {
                    await ServersWebView.EnsureCoreWebView2Async();
                    if (ServersWebView.CoreWebView2 != null) {
                        ServersWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                        ServersWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                        ServersWebView.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
                        ServersWebView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
                        ServersWebView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                        ServersWebView.CoreWebView2.WebResourceRequested += (s, args) => {
                            if (IsTrackerOrTelemetry(args.Request.Uri)) {
                                args.Response = ServersWebView.CoreWebView2.Environment.CreateWebResourceResponse(new MemoryStream(), 403, "Blocked by BUGrock Privacy Shield", "");
                            }
                        };
                        await ServersWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(SERVERS_VIEW_POLISH_SCRIPT);
                        ServersWebView.NavigationCompleted += (s, e) => {
                            try {
                                ServersWebView.CoreWebView2?.ExecuteScriptAsync(SERVERS_VIEW_POLISH_SCRIPT);
                            } catch { }
                        };
                    }
                }

                // 2. Initialize In-App Browser WebView
                if (BrowserWebView != null) {
                    await BrowserWebView.EnsureCoreWebView2Async();
                    if (BrowserWebView.CoreWebView2 != null) {
                        BrowserWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                        BrowserWebView.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
                        BrowserWebView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
                        BrowserWebView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                        BrowserWebView.CoreWebView2.WebResourceRequested += (s, args) => {
                            if (IsTrackerOrTelemetry(args.Request.Uri)) {
                                args.Response = BrowserWebView.CoreWebView2.Environment.CreateWebResourceResponse(new MemoryStream(), 403, "Blocked by BUGrock Privacy Shield", "");
                            }
                        };
                        await BrowserWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(ANTI_COOKIE_AND_POPUP_SCRIPT);
                        BrowserWebView.CoreWebView2.SourceChanged += (s, e) => {
                            if (BrowserUrlText != null) {
                                BrowserUrlText.Text = BrowserWebView.Source?.ToString() ?? "";
                            }
                        };
                        BrowserWebView.CoreWebView2.NavigationCompleted += (s, e) => {
                            try {
                                BrowserWebView.CoreWebView2?.ExecuteScriptAsync(ANTI_COOKIE_AND_POPUP_SCRIPT);
                            } catch { }
                        };
                        BrowserWebView.CoreWebView2.NewWindowRequested += (s, e) => {
                            e.Handled = true;
                            BrowserWebView.CoreWebView2.Navigate(e.Uri);
                        };
                        BrowserWebView.CoreWebView2.DownloadStarting += Browser_DownloadStarting;
                    }
                }
            } catch (Exception ex) {
                Trace.WriteLine("Failed to initialize WebView2: " + ex.Message);
            }
        }

        private void Browser_DownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e) {
            try {
                string fileName = Path.GetFileName(e.ResultFilePath);
                string sourceUri = e.DownloadOperation.Uri ?? "";

                string targetDir = DDFWManager.DetermineTargetFolder(sourceUri, fileName);
                string finalPath = Path.Combine(targetDir, fileName);

                e.ResultFilePath = finalPath;
                e.Handled = true;

                e.DownloadOperation.StateChanged += (s, ev) => {
                    if (e.DownloadOperation.State == CoreWebView2DownloadState.Completed) {
                        Dispatcher.Invoke(() => {
                            try {
                                string recheckDir = DDFWManager.DetermineTargetFolder(sourceUri, fileName, finalPath);
                                if (!string.Equals(targetDir, recheckDir, StringComparison.OrdinalIgnoreCase)) {
                                    string newFinalPath = Path.Combine(recheckDir, fileName);
                                    if (File.Exists(finalPath)) {
                                        File.Move(finalPath, newFinalPath, true);
                                        finalPath = newFinalPath;
                                        targetDir = recheckDir;
                                    }
                                }
                            } catch { }

                            _lastDownloadedFilePath = finalPath;
                            _lastDownloadedFolder = targetDir;
                            string catName = DDFWManager.GetCategoryDisplayName(targetDir);
                            ShowDownloadToast($"✅ {fileName} saved to DDFW/{Path.GetFileName(targetDir)} ({catName})");
                        });
                    }
                };
            } catch (Exception ex) {
                Trace.WriteLine("Download starting exception: " + ex.Message);
            }
        }

        private void ShowDownloadToast(string message) {
            if (DownloadToastBar == null || DownloadToastText == null) return;
            DownloadToastText.Text = message;
            DownloadToastBar.Visibility = Visibility.Visible;
        }

        private void ToastDismiss_Click(object sender, RoutedEventArgs e) {
            if (DownloadToastBar != null) DownloadToastBar.Visibility = Visibility.Collapsed;
        }

        private void ToastShowInFolder_Click(object sender, RoutedEventArgs e) {
            try {
                string folder = _lastDownloadedFolder ?? DDFWManager.RootFolder;
                if (!string.IsNullOrEmpty(_lastDownloadedFilePath) && File.Exists(_lastDownloadedFilePath)) {
                    Process.Start("explorer.exe", $"/select,\"{_lastDownloadedFilePath}\"");
                } else if (Directory.Exists(folder)) {
                    Process.Start("explorer.exe", $"\"{folder}\"");
                }
            } catch (Exception ex) {
                ThemedMessageBox.Show($"Could not open folder: {ex.Message}", "Error");
            }
        }

        private void OpenInAppBrowser(string url) {
            if (InAppBrowserOverlay == null || BrowserWebView == null) return;
            if (PlayViewGrid != null) PlayViewGrid.Visibility = Visibility.Collapsed;
            if (ServerCardBorder != null) ServerCardBorder.Visibility = Visibility.Collapsed;
            if (ServersWebView != null) ServersWebView.Visibility = Visibility.Collapsed;

            InAppBrowserOverlay.Visibility = Visibility.Visible;
            if (BrowserWebView.CoreWebView2 != null) {
                BrowserWebView.CoreWebView2.Navigate(url);
            } else {
                BrowserWebView.Source = new Uri(url);
            }
        }

        private void CardModsSection_Click(object sender, MouseButtonEventArgs e) {
            OpenInAppBrowser("https://mcpedl.com/category/mods/");
        }

        private void CardTexturesSection_Click(object sender, MouseButtonEventArgs e) {
            OpenInAppBrowser("https://mcpedl.com/category/texture-packs/");
        }

        private void CardMapsSection_Click(object sender, MouseButtonEventArgs e) {
            OpenInAppBrowser("https://mcpedl.com/category/maps/");
        }

        private void CardSkinsSection_Click(object sender, MouseButtonEventArgs e) {
            OpenInAppBrowser("https://mcpedl.com/skins/skin-packs/");
        }

        private void RefreshServersBtn_Click(object sender, RoutedEventArgs e) {
            try {
                ServersWebView?.Reload();
            } catch { }
        }

        private void ExpandServersBtn_Click(object sender, RoutedEventArgs e) {
            OpenInAppBrowser("https://minecraftpocket-servers.com/");
        }

        private void BrowserBackBtn_Click(object sender, RoutedEventArgs e) {
            try { if (BrowserWebView?.CanGoBack == true) BrowserWebView.GoBack(); } catch { }
        }

        private void BrowserForwardBtn_Click(object sender, RoutedEventArgs e) {
            try { if (BrowserWebView?.CanGoForward == true) BrowserWebView.GoForward(); } catch { }
        }

        private void BrowserRefreshBtn_Click(object sender, RoutedEventArgs e) {
            try { BrowserWebView?.Reload(); } catch { }
        }

        private void BrowserMCPEDLToggleBtn_Click(object sender, RoutedEventArgs e) {
            OpenInAppBrowser("https://mcpedl.com");
        }

        private void BrowserCurseForgeToggleBtn_Click(object sender, RoutedEventArgs e) {
            OpenInAppBrowser("https://www.curseforge.com/minecraft-bedrock");
        }

        private void OpenDDFWFolderBtn_Click(object sender, RoutedEventArgs e) {
            try {
                DDFWManager.EnsureDirectories();
                Process.Start("explorer.exe", $"\"{DDFWManager.RootFolder}\"");
            } catch (Exception ex) {
                ThemedMessageBox.Show($"Could not open DDFW folder: {ex.Message}", "Error");
            }
        }

        private void CloseBrowserBtn_Click(object sender, RoutedEventArgs e) {
            if (InAppBrowserOverlay != null) {
                InAppBrowserOverlay.Visibility = Visibility.Collapsed;
            }
            if (DownloadToastBar != null) {
                DownloadToastBar.Visibility = Visibility.Collapsed;
            }
            if (TabNavPlay?.IsChecked == true) {
                if (PlayViewGrid != null) PlayViewGrid.Visibility = Visibility.Visible;
                if (ServerCardBorder != null) ServerCardBorder.Visibility = Visibility.Visible;
                if (ServersWebView != null) ServersWebView.Visibility = Visibility.Visible;
            }
        }
        #endregion

        private async void LoadVersionList() {
            if (_isLoadingVersionList) return;
            _isLoadingVersionList = true;
            try {
                _versions.VersionsApiUWP = UserPrefs.VersionsApiUWP != "" ? UserPrefs.VersionsApiUWP : VERSIONS_API_UWP;
                _versions.VersionsApiGDK = UserPrefs.VersionsApiGDK != "" ? UserPrefs.VersionsApiGDK : VERSIONS_API_GDK;
                _versions.PrepareForReload();

                LoadingProgressLabel.Content = "Loading GDK versions from cache";
                LoadingProgressBar.Value = 1;

                LoadingProgressGrid.Visibility = Visibility.Visible;

                try {
                    await _versions.LoadFromCacheGDK();
                } catch (Exception e) {
                    Trace.WriteLine("List cache load failed:\n" + e.ToString());
                }

                LoadingProgressLabel.Content = "Loading UWP versions from cache";
                LoadingProgressBar.Value = 2;
                try {
                    await _versions.LoadFromCacheUWP();
                } catch (Exception e) {
                    Trace.WriteLine("List cache load failed:\n" + e.ToString());
                }

                _versions.PrepareForReload();

                LoadingProgressLabel.Content = "Downloading new GDK version data";
                LoadingProgressBar.Value = 3;
                try {
                    await _versions.DownloadVersionsGDK();
                } catch (Exception e) {
                    Trace.WriteLine("List download failed:\n" + e.ToString());
                    try { await _versions.LoadFromCacheGDK(); } catch { }
                    ThemedMessageBox.Show("Failed to update version list from the internet. Some new versions might be missing.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }


                LoadingProgressLabel.Content = "Downloading new UWP version data";
                LoadingProgressBar.Value = 4;
                try {
                    await _versions.DownloadVersionsUWP();
                } catch (Exception e) {
                    Trace.WriteLine("List download failed:\n" + e.ToString());
                    try { await _versions.LoadFromCacheUWP(); } catch { }
                    ThemedMessageBox.Show("Failed to update version list from the internet. Some new versions might be missing.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                LoadingProgressLabel.Content = "Loading imported versions";
                LoadingProgressBar.Value = 5;
                await _versions.LoadImported();

                ApplyCustomInstallDirectories();

                LoadingProgressGrid.Visibility = Visibility.Collapsed;
                UpdateQuickPlayStatus();
            } finally {
                _isLoadingVersionList = false;
            }
        }

        private void ApplyCustomInstallDirectories() {
            if (UserPrefs?.CustomInstallDirectories == null) return;
            foreach (var v in _versions) {
                if (v.VersionType != VersionType.Imported && UserPrefs.CustomInstallDirectories.TryGetValue(v.Name, out var customDir)) {
                    if (!string.IsNullOrWhiteSpace(customDir) && Directory.Exists(customDir)) {
                        v.GameDirectory = customDir;
                        v.UpdateInstallStatus();
                    }
                }
            }
        }

        private void VersionEntryPropertyChanged(object? sender, PropertyChangedEventArgs e) {
            if (!Dispatcher.CheckAccess()) {
                Dispatcher.InvokeAsync(() => VersionEntryPropertyChanged(sender, e));
                return;
            }
            RefreshLists();
            UpdateQuickPlayStatus();
        }

        private async void ImportButtonClicked(object sender, RoutedEventArgs e) {
            Microsoft.Win32.OpenFileDialog openFileDlg = new Microsoft.Win32.OpenFileDialog();
            openFileDlg.Filter = "XVC and APPX packages (*.msixvc, *.appx)|*.msixvc;*.appx|APPX packages (*.appx)|*.appx|XVC packages (*.msixvc)|*.msixvc|All Files|*.*";
            Nullable<bool> result = openFileDlg.ShowDialog();
            if (result == true) {
                Directory.CreateDirectory(IMPORTED_VERSIONS_PATH);
                string directory = Path.Combine(IMPORTED_VERSIONS_PATH, openFileDlg.SafeFileName);
                if (Directory.Exists(directory)) {
                    var found = false;
                    foreach (var version in _versions) {
                        if (version.IsImported && version.GameDirectory == directory) {
                            if (version.IsStateChanging) {
                                ThemedMessageBox.Show("A version with the same name was already imported, and is currently being modified. Please wait a few moments and try again.", "Error");
                                return;
                            }
                            MessageBoxResult messageBoxResult = ThemedMessageBox.Show("A version with the same name was already imported. Do you want to delete it ?", "Delete Confirmation", System.Windows.MessageBoxButton.YesNo);
                            if (messageBoxResult == MessageBoxResult.Yes) {
                                var uninstallResult = await Remove(version);
                                if (!uninstallResult) {
                                    ThemedMessageBox.Show("Failed to remove existing version. Import aborted.", "Error");
                                    return;
                                }
                                found = true;
                                break;
                            } else {
                                return;
                            }
                        }
                    }
                    if (!found) {
                        ThemedMessageBox.Show("The destination path for importing already exists and doesn't contain a Minecraft installation known to the launcher. To avoid loss of data, importing was aborted. Please remove the files manually.", "Error");
                        return;
                    }
                }

                var extension = Path.GetExtension(openFileDlg.FileName).ToLowerInvariant();
                PackageType packageType;
                if (extension == ".msixvc") {
                    packageType = PackageType.GDK;
                } else if (extension == ".appx") {
                    packageType = PackageType.UWP;
                } else {
                    ThemedMessageBox.Show("Unsupported file extension: " + extension, "Import failure");
                    return;
                }


                var versionEntry = _versions.AddEntry(openFileDlg.SafeFileName, directory, packageType);
                VersionTabs.SelectedItem = ImportedTab;

                bool success = false;

                //TODO: for now we don't have any way to know whether these are preview builds or not
                if (packageType == PackageType.UWP) {
                    success = await ExtractAppx(openFileDlg.FileName, directory, versionEntry);
                
                } else if (packageType == PackageType.GDK) {
                    if (!ShowGDKFirstUseWarning()) {
                        success = false;
                    } else {
                        success = await ExtractMsixvc(openFileDlg.FileName, directory, versionEntry, isPreview: false);
                    }
                } else {
                    Debug.Assert(false);
                }

                if (success) {
                    versionEntry.StateChangeInfo = null;
                    versionEntry.UpdateInstallStatus();
                } else {
                    _versions.Remove(versionEntry);
                }
            }
        }

        private void InstallError(string userMessage, string debug, string fileName, Exception? ex) {
            string exceptionMessage = "none";
            if (ex != null) {
                Trace.WriteLine(debug + ": " + ex.ToString());
                exceptionMessage = ex.Message;
            } else {
                Trace.WriteLine(debug);
            }

            ThemedMessageBox.Show(
                "Failed to import file: " + fileName + "\n\n" +
                userMessage +
                (ex != null ? "\n\nException message: " + exceptionMessage : "") +
                "\n\nCheck the log file if you need more information (File -> Open log file).", "Import failure"
            );
        }

        private async Task<bool> ExtractAppx(string filePath, string directory, Version versionEntry, CancellationToken cancellationToken = default) {
            versionEntry.StateChangeInfo = new VersionStateChangeInfo(VersionState.Extracting);
            try {
                await Task.Run(() => {
                    using (var archive = ZipFile.OpenRead(filePath)) {
                        int totalEntries = archive.Entries.Count;
                        Dispatcher.BeginInvoke(() => {
                            if (versionEntry.StateChangeInfo != null) {
                                versionEntry.StateChangeInfo.MaxProgress = totalEntries;
                            }
                        });

                        int current = 0;
                        string fullTargetDir = Path.GetFullPath(directory);
                        string fullTargetDirWithSep = fullTargetDir.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                            ? fullTargetDir
                            : fullTargetDir + Path.DirectorySeparatorChar;
                        Directory.CreateDirectory(fullTargetDir);

                        foreach (var entry in archive.Entries) {
                            cancellationToken.ThrowIfCancellationRequested();

                            string destinationPath = Path.GetFullPath(Path.Combine(fullTargetDir, entry.FullName));
                            if (!destinationPath.StartsWith(fullTargetDirWithSep, StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(destinationPath, fullTargetDir, StringComparison.OrdinalIgnoreCase)) {
                                continue;
                            }

                            if (string.IsNullOrEmpty(entry.Name)) {
                                Directory.CreateDirectory(destinationPath);
                            } else {
                                string? parentDir = Path.GetDirectoryName(destinationPath);
                                if (!string.IsNullOrEmpty(parentDir)) {
                                    Directory.CreateDirectory(parentDir);
                                }
                                entry.ExtractToFile(destinationPath, overwrite: true);
                            }

                            current++;
                            if (current % 15 == 0 || current == totalEntries) {
                                int progressVal = current;
                                int percent = totalEntries > 0 ? (int)((long)current * 100 / totalEntries) : 0;
                                int remaining = 100 - percent;
                                Dispatcher.BeginInvoke(() => {
                                    if (versionEntry.StateChangeInfo != null) {
                                        versionEntry.StateChangeInfo.Progress = progressVal;
                                    }
                                });
                                UpdateHeroProgress($"Extracting... {percent}% ({remaining}% remaining)", percent, false);
                            }
                        }
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    string sigPath = Path.Combine(directory, "AppxSignature.p7x");
                    if (File.Exists(sigPath)) {
                        File.Delete(sigPath);
                    }
                }, cancellationToken);

                versionEntry.UpdateInstallStatus();

                return true;
            } catch (OperationCanceledException) {
                Trace.WriteLine("ExtractAppx was cancelled by user.");
                try {
                    if (Directory.Exists(directory)) {
                        Directory.Delete(directory, recursive: true);
                    }
                } catch { }
                return false;
            } catch (InvalidDataException ex) {
                InstallError(
                    "File seems to be corrupted or not an APPX file",
                    "Failed extracting appx",
                    filePath,
                    ex
                );
                return false;
            } finally {
                versionEntry.StateChangeInfo = null;
            }
        }

        private static bool IsValidPeExecutable(string filePath) {
            try {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return false;
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (fs.Length < 64) return false;
                byte[] buffer = new byte[2];
                if (fs.Read(buffer, 0, 2) != 2) return false;
                return buffer[0] == 0x4D && buffer[1] == 0x5A; // 'M', 'Z'
            } catch {
                return false;
            }
        }

        private async Task<bool> CheckGdkPrerequisitesAndWarn(bool isPreview, CancellationToken cancellationToken = default) {
            if (!UserPrefs.AutoFetchDecryptionKeys) {
                var res = ThemedMessageBox.Show(
                    "Auto-fetch decryption keys is currently disabled in Options.\n\n" +
                    "Modern Minecraft versions (1.21+) require GDK decryption keys to run.\n" +
                    "If these keys are not present on your system, this installation will fail to decrypt and cannot start.\n\n" +
                    "Would you like to enable Auto-fetch and proceed?",
                    "Decryption Keys Required",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning
                );

                if (res == MessageBoxResult.Yes) {
                    UserPrefs.AutoFetchDecryptionKeys = true;
                    if (AutoFetchDecryptionKeysOption != null) {
                        AutoFetchDecryptionKeysOption.IsChecked = true;
                    }
                    RewritePrefs();
                } else if (res == MessageBoxResult.Cancel) {
                    return false;
                }
            }

            if (UserPrefs.AutoFetchDecryptionKeys) {
                await EnsureGDKDecryptionKeys(isPreview, cancellationToken);
            }

            return true;
        }

        private async Task<bool> EnsureGDKDecryptionKeys(bool isPreview, CancellationToken cancellationToken = default) {
            try {
                var family = isPreview ? MinecraftPackageFamilies.MINECRAFT_PREVIEW : MinecraftPackageFamilies.MINECRAFT;
                var pkgManager = new PackageManager();
                foreach (var pkg in pkgManager.FindPackages(family)) {
                    if (!pkg.IsDevelopmentMode) {
                        Trace.WriteLine("Store package detected on system, GDK keys should be available.");
                        return true;
                    }
                }

                Trace.WriteLine("Acquiring GDK decryption keys in background...");
                UpdateHeroProgress("Acquiring decryption keys in background...", 0, isIndeterminate: true);

                string productId = isPreview ? "9P5X4QVLC2XR" : "9NBLGGH4279M";

                try {
                    var psi = new ProcessStartInfo {
                        FileName = "winget.exe",
                        Arguments = $"install --id {productId} --source msstore --accept-package-agreements --accept-source-agreements",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using var proc = Process.Start(psi);
                    if (proc != null) {
                        using var reg = cancellationToken.Register(() => {
                            try { proc.Kill(); } catch { }
                        });

                        await proc.WaitForExitAsync(cancellationToken);
                        Trace.WriteLine($"winget key acquisition finished with exit code {proc.ExitCode}");
                        if (proc.ExitCode == 0) {
                            return true;
                        }
                    }
                } catch (Exception ex) {
                    Trace.WriteLine("winget acquisition attempt error: " + ex.Message);
                }

                return false;
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                Trace.WriteLine("EnsureGDKDecryptionKeys error: " + ex.Message);
                return false;
            }
        }

        private bool ShowGDKFirstUseWarning() {
            return true;
        }

        private void FixGDKManifest(string path) {
            XDocument doc = XDocument.Load(path);
            XNamespace ns = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
            XNamespace rescap = "http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities";

            var apps = doc.Descendants(ns + "Application");
            foreach (var app in apps) {
                var executable = app.Attribute("Executable");
                //install from older versions will reference the minecraft exe directly, so we need to patch these
                if (executable != null && (executable.Value == "GameLaunchHelper.exe" || executable.Value == "Minecraft.Windows.exe")) {
                    executable.Value = GDK_SHIM_NAME;
                }
            }

            var extensions = doc.Root.Elements(ns + "Extensions").ToList();
            foreach (var ext in extensions) {
                ext.Remove();
            }

            var capabilities = doc.Descendants(ns + "Capabilities");
            var customInstall = capabilities
                .Elements(rescap + "Capability")
                .Where(c => c.Attribute("Name")?.Value == "customInstallActions")
                .ToList();
            foreach (var cap in customInstall) {
                cap.Remove();
            }

            var settings = new XmlWriterSettings();
            settings.Encoding = new UTF8Encoding(false); //no BOM
            settings.Indent = true;
            using (XmlWriter w = XmlWriter.Create(path, settings)) {
                doc.Save(w);
            }
        }


        private void RecursiveCopyDirectory(string from, string to, HashSet<string> skip) {
            Directory.CreateDirectory(to);
            foreach (var source in Directory.EnumerateFiles(from)) {
                if (skip.Contains(source)) {
                    continue;
                }
                string destination = Path.Combine(to, Path.GetFileName(source));
                Trace.WriteLine(source + " -> " + destination);
                File.Copy(source, destination, true);
            }
            foreach (var source in Directory.EnumerateDirectories(from)) {
                string destination = Path.Combine(to, Path.GetFileName(source));
                RecursiveCopyDirectory(source, destination, skip);
            }
        }

        private static string SafeReadAllText(string path) {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "";
            for (int retry = 0; retry < 10; retry++) {
                try {
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var sr = new StreamReader(fs)) {
                        return sr.ReadToEnd();
                    }
                } catch {
                    System.Threading.Thread.Sleep(100);
                }
            }
            return "";
        }

        private async Task<bool> ExtractMsixvc(string filePath, string directory, Version versionEntry, bool isPreview, CancellationToken cancellationToken = default) {
            if (_hasGdkExtractTask) {
                InstallError(
                    "Can't install multiple MSIXVC packages at the same time. Please wait for the current installation to finish before starting a new one.",
                    "Concurrent MSIXVC installation attempt",
                    filePath,
                    null
                );
                return false;
            }
            _hasGdkExtractTask = true;
            try {
                cancellationToken.ThrowIfCancellationRequested();
                if (!await CheckGdkPrerequisitesAndWarn(isPreview, cancellationToken)) {
                    return false;
                }
                directory = Path.GetFullPath(directory);
                // XVC are encrypted containers, I don't currently know of any way to extract them to an arbitrary directory
                // For now we just stage the package in XboxGames, and then move the files to the launcher data directory

                versionEntry.StateChangeInfo = new VersionStateChangeInfo(VersionState.Staging) {
                    MaxProgress = 100,
                    Progress = 0
                };

                var packageManager = new PackageManager();

                //make sure XboxGames is cleared
                Trace.WriteLine("Clearing existing XboxGames Minecraft installation");
                try {
                    await UnregisterPackage(versionEntry.GamePackageFamily, versionEntry, skipBackup: false);
                } catch (Exception ex) {
                    InstallError(
                        "The existing XboxGames Minecraft installation could not be removed. Please make sure Minecraft is not running and try again.",
                        "Failed clearing XboxGames Minecraft installation",
                        filePath,
                        ex
                    );
                    return false;
                }

                cancellationToken.ThrowIfCancellationRequested();

                try {
                    await DeploymentProgressWrapper(packageManager.StagePackageAsync(new Uri(filePath), null), versionEntry, cancellationToken);
                } catch (OperationCanceledException) {
                    Trace.WriteLine("StagePackageAsync cancelled by user.");
                    try {
                        await UnregisterPackage(versionEntry.GamePackageFamily, versionEntry, skipBackup: true);
                    } catch { }
                    return false;
                } catch (Exception ex) {
                    InstallError(
                        "Failed to stage package.\n" +
                            "This may mean that the file is damaged, not an MSIXVC file. Please check the integrity of the file.\n\n" +
                            "However, this error might also happen if you've never installed a GDK version of Minecraft from the Store before,\n" +
                            "as the launcher relies on the Store to install the keys needed to decrypt the installation package.\n" +
                            "Please ensure that you've installed " + (isPreview ? "Minecraft Preview" : "Minecraft") + " from the Store before installing GDK versions using the launcher.",
                        "Failed staging MSIXVC",
                        filePath,
                        ex
                    );
                    return false;
                }

                cancellationToken.ThrowIfCancellationRequested();

                string installPath = "";
                foreach (var pkg in new PackageManager().FindPackages(versionEntry.GamePackageFamily)) {
                    if (installPath != "") {
                        InstallError(
                            "Minecraft is installed in multiple places, and the launcher doesn't know where to copy files from.\n" +
                            "This is probably because another user has the game installed.",
                            "Multiple locations found for staged MSIXVC: " + installPath + ", " + pkg.InstalledLocation.Path,
                            filePath,
                            null
                        );
                        return false;
                    }
                    installPath = pkg.InstalledLocation.Path;
                }
                Trace.WriteLine("Detected staging path: " + installPath);
                string resolvedPath = LinkResolver.Resolve(installPath);
                Trace.WriteLine("Symlink resolved as " + resolvedPath);
                installPath = resolvedPath;

                var exeSrcPath = Path.Combine(installPath, "Minecraft.Windows.exe");
                if (!Directory.Exists(installPath)) {
                    InstallError(
                        "Didn't find installation expected at " + installPath + "\nMaybe your XboxGames folder is in a different location?",
                        "Expected XboxGames Minecraft directory not found" + installPath,
                        filePath,
                        null
                    );
                    return false;
                }
                if (!File.Exists(exeSrcPath)) {
                    InstallError(
                        "Didn't find Minecraft executable at " + exeSrcPath,
                        "Expected XboxGames Minecraft executable not found: " + exeSrcPath,
                        filePath,
                        null
                    );
                    return false;
                }

                cancellationToken.ThrowIfCancellationRequested();

                versionEntry.StateChangeInfo.VersionState = VersionState.Decrypting;
                UpdateHeroProgress("Decrypting Minecraft.Windows.exe...", 100, isIndeterminate: true);

                var exeTmpDir = Path.GetTempPath();
                var uuid = Guid.NewGuid().ToString();
                //Use a different tmp path to make sure we don't copy half-done files
                //UUID makes sure we don't copy the leftovers of a different, failed installation
                var exeTmpPath = Path.Combine(exeTmpDir, "Minecraft.Windows_" + uuid + ".exe");
                var donePath = exeTmpPath + ".done";

                var exeDstPath = Path.Combine(Path.GetFullPath(directory), "Minecraft.Windows.exe");
                var decryptHelperLogFile = Path.GetTempFileName();

                try {
                    var helperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, GDK_DECRYPT_HELPER_NAME);

                    static string EscapePsSingleQuoted(string s) => s.Replace("'", "''");
                    var innerArgs = $"\"{EscapePsSingleQuoted(exeSrcPath)}\" \"{EscapePsSingleQuoted(exeTmpPath)}\" \"{EscapePsSingleQuoted(decryptHelperLogFile)}\" \"{EscapePsSingleQuoted(donePath)}\"";

                    // Use Base64 EncodedCommand to prevent any quoting or space issues in paths
                    var psScript = $"Invoke-CommandInDesktopPackage -PackageFamilyName '{EscapePsSingleQuoted(versionEntry.GamePackageFamily)}' -App Game -Command '{EscapePsSingleQuoted(helperPath)}' -Args '{innerArgs}'";
                    var encodedCommand = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(psScript));
                    Trace.WriteLine("Decrypt command (raw): " + psScript);

                    var processInfo = new ProcessStartInfo {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -NonInteractive -EncodedCommand {encodedCommand}",
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false
                    };

                    Trace.WriteLine("Copying decrypted exe");
                    try {
                        using var process = Process.Start(processInfo);
                        if (process != null) {
                            using var reg = cancellationToken.Register(() => {
                                try { process.Kill(); } catch { }
                            });
                            await process.WaitForExitAsync(cancellationToken);
                            Trace.WriteLine("Process output:" + (await process.StandardOutput.ReadToEndAsync()));
                            Trace.WriteLine("Process errors:" + (await process.StandardError.ReadToEndAsync()));
                        }
                    } catch (OperationCanceledException) {
                        throw;
                    } catch (Exception ex) {
                        Trace.WriteLine("Decrypt helper log output: " + SafeReadAllText(decryptHelperLogFile));
                        InstallError(
                            "Failed to run PowerShell to copy the Minecraft executable out of the staged package",
                            "Failed running PowerShell for exe extraction",
                            filePath,
                            ex
                        );
                        return false;
                    }

                    for (int i = 0; i < 300 && !File.Exists(donePath); i++) {
                        cancellationToken.ThrowIfCancellationRequested();
                        //Give it up to 30 seconds to copy the file
                        //We can't block on the outcome of Invoke-CommandInDesktopPackage, so we have to poll for the file
                        await Task.Delay(100, cancellationToken);
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    Trace.WriteLine("Decrypt helper log output: " + SafeReadAllText(decryptHelperLogFile));

                    if (!File.Exists(exeTmpPath)) {
                        Trace.WriteLine("Src path: " + exeSrcPath);
                        Trace.WriteLine("Tmp path: " + exeTmpPath);
                        InstallError(
                            "The Minecraft executable could not be copied out of the staged package.\n" +
                                "This is usually due to the game license not being installed for your Windows user account.\n\n" +
                                "Please ensure that you've installed " + (isPreview ? "Minecraft Preview" : "Minecraft") + " from the Store before using this launcher.",
                            "PowerShell subprocess didn't seem to copy the exe in time",
                            filePath,
                            null
                        );
                        return false;
                    }

                    if (!IsValidPeExecutable(exeTmpPath)) {
                        Trace.WriteLine("Decrypted exe failed PE header check: still encrypted ciphertext.");
                        var storePrompt = ThemedMessageBox.Show(
                            "Minecraft.Windows.exe is encrypted and requires an active game license from Microsoft Store.\n\n" +
                            "Since your account already owns or has access to Minecraft, clicking 'Yes' will open the Store to register the decryption keys on this PC.\n\n" +
                            "Would you like to open Microsoft Store now to activate it?",
                            "Decryption Keys Required",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning
                        );
                        if (storePrompt == MessageBoxResult.Yes) {
                            try {
                                string storeUri = isPreview ? "ms-windows-store://pdp/?ProductId=9P5X4QVLC2XR" : "ms-windows-store://pdp/?ProductId=9NBLGGH4279M";
                                Process.Start(new ProcessStartInfo {
                                    FileName = storeUri,
                                    UseShellExecute = true
                                });
                            } catch { }
                        }
                        return false;
                    }

                    Trace.WriteLine("Minecraft executable decrypted successfully");

                    cancellationToken.ThrowIfCancellationRequested();

                    versionEntry.StateChangeInfo.VersionState = VersionState.Moving;
                    UpdateHeroProgress("Finalizing installation files...", 100, isIndeterminate: true);

                    //TODO: this could fail if the launcher is on a different drive than C: ?
                    try {
                        Trace.WriteLine("Moving staged files: " + installPath + " -> " + directory);
                        if (Path.GetPathRoot(installPath) == Path.GetPathRoot(directory)) {
                            Trace.WriteLine("Destination for extraction is on the same drive as the installation location - moving files for speed");
                            Directory.Move(installPath, directory);
                        } else {
                            Trace.WriteLine("Destination for extraction is on a different drive than staged - copying files");
                            //Minecraft.Windows.exe can't be copied directly due to permissions
                            HashSet<string> skip = new HashSet<string>();
                            skip.Add(exeSrcPath);
                            RecursiveCopyDirectory(installPath, directory, skip);
                        }


                        Trace.WriteLine("Moving decrypted exe into place");
                        if (File.Exists(exeDstPath)) {
                            File.Delete(exeDstPath);
                        }
                        if (File.Exists(exeTmpPath)) {
                            File.Move(exeTmpPath, exeDstPath);
                        }
                    } catch (Exception ex) {
                        InstallError(
                            "Failed copying/moving game files to the destination folder",
                            "Failed moving game files to destination",
                            filePath,
                            ex
                        );
                        return false;
                    }
                } finally {
                    try { if (File.Exists(exeTmpPath)) File.Delete(exeTmpPath); } catch { }
                    try { if (File.Exists(donePath)) File.Delete(donePath); } catch { }
                    try { if (File.Exists(decryptHelperLogFile)) File.Delete(decryptHelperLogFile); } catch { }
                }

                Trace.WriteLine("Cleaning up XboxGames");
                //we already created a backup earlier, so a new attempt would just get in the way
                await UnregisterPackage(versionEntry.GamePackageFamily, versionEntry, skipBackup: true);

                Trace.WriteLine("Done importing msixvc: " + filePath);
                return true;

            } catch (OperationCanceledException) {
                Trace.WriteLine("ExtractMsixvc cancelled by user.");
                try {
                    await UnregisterPackage(versionEntry.GamePackageFamily, versionEntry, skipBackup: true);
                } catch { }
                try {
                    if (Directory.Exists(directory)) Directory.Delete(directory, true);
                } catch { }
                return false;
            } finally {
                _hasGdkExtractTask = false;
            }
        }

        public ICommand LaunchCommand => new RelayCommand((v) => InvokeLaunch((Version)v));

        public ICommand RemoveCommand => new RelayCommand((v) => InvokeRemove((Version)v));

        public ICommand DownloadCommand => new RelayCommand((v) => InvokeDownload((Version)v));

        private void InvokeLaunch(Version v) {
            if (_hasLaunchTask)
                return;
            _hasLaunchTask = true;
            Task.Run(async () => {
                try {
                    v.StateChangeInfo = new VersionStateChangeInfo(VersionState.MovingData);
                    UpdateHeroProgress("Restoring Minecraft worlds...", 100, isIndeterminate: true);
                    if (!MoveMinecraftData(v.GamePackageFamily, v.PackageType)) {
                        Trace.WriteLine("Data restore error, aborting launch");
                        v.StateChangeInfo = null;
                        _hasLaunchTask = false;
                        HideHeroProgress();
                        return;
                    }
                    v.StateChangeInfo = new VersionStateChangeInfo(VersionState.Registering);
                    UpdateHeroProgress("Registering package...", 100, isIndeterminate: true);
                    string gameDir = Path.GetFullPath(v.GameDirectory);
                    try {
                        await ReRegisterPackage(v.GamePackageFamily, gameDir, v);
                    } catch (Exception e) {
                        Trace.WriteLine("App re-register failed:\n" + e.ToString());
                        ThemedMessageBox.Show("App re-register failed:\n" + e.ToString());
                        _hasLaunchTask = false;
                        v.StateChangeInfo = null;
                        HideHeroProgress();
                        return;
                    }
                    v.StateChangeInfo = new VersionStateChangeInfo(VersionState.Launching);
                    UpdateHeroProgress("Launching Minecraft...", 100, isIndeterminate: true);

                    if (v.PackageType == PackageType.GDK) {
                        string exePath = Path.Combine(gameDir, "Minecraft.Windows.exe");
                        if (!IsValidPeExecutable(exePath)) {
                            Trace.WriteLine("Refusing to launch encrypted/corrupted executable: " + exePath);
                            ThemedMessageBox.Show(
                                "Minecraft cannot be launched because Minecraft.Windows.exe is still encrypted.\n\n" +
                                "Please delete this installation and reinstall it with 'Auto-fetch decryption keys' enabled in Options.",
                                "Cannot Launch Minecraft",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error
                            );
                            _hasLaunchTask = false;
                            v.StateChangeInfo = null;
                            HideHeroProgress();
                            return;
                        }
                    }

                    try {
                        var pkg = await AppDiagnosticInfo.RequestInfoForPackageAsync(v.GamePackageFamily);
                        if (pkg.Count > 0) {
                            if (pkg.Count > 1) {
                                Trace.WriteLine("Multiple packages found ???");
                            }
                            var result = await pkg[0].LaunchAsync();
                            if (result.ExtendedError != null) {
                                Trace.WriteLine("LaunchAsync didn't throw, but returned an extended error???");
                                throw result.ExtendedError;
                            }
                        } else {
                            throw new Exception("No packages found for package family " + v.GamePackageFamily);
                        }
                        Trace.WriteLine("App launch finished!");
                    } catch (Exception e) {
                        Trace.WriteLine("App launch failed:\n" + e.ToString());
                        ThemedMessageBox.Show("App launch failed:\n" + e.ToString());
                        return;
                    } finally {
                        _hasLaunchTask = false;
                        v.StateChangeInfo = null;
                        HideHeroProgress();
                    }
                } catch {
                    _hasLaunchTask = false;
                    v.StateChangeInfo = null;
                    HideHeroProgress();
                }
            });
        }

        private async Task DeploymentProgressWrapper(IAsyncOperationWithProgress<DeploymentResult, DeploymentProgress> t, Version? version, CancellationToken cancellationToken = default) {
            TaskCompletionSource<int> src = new TaskCompletionSource<int>();
            using var reg = cancellationToken.Register(() => {
                try {
                    t.Cancel();
                    src.TrySetCanceled(cancellationToken);
                } catch { }
            });

            t.Progress += (v, p) => {
                Trace.WriteLine("Deployment progress: " + p.state + " " + p.percentage + "%");
                int pct = (int)p.percentage;
                int remaining = 100 - pct;
                UpdateHeroProgress($"Staging package... {pct}% ({remaining}% remaining)", pct, false);

                if (version?.StateChangeInfo != null) {
                    Dispatcher.BeginInvoke(() => {
                        if (version.StateChangeInfo != null) {
                            version.StateChangeInfo.MaxProgress = 100;
                            version.StateChangeInfo.Progress = (long)p.percentage;
                        }
                    });
                }
            };
            t.Completed += (v, p) => {
                if (p == AsyncStatus.Canceled) {
                    src.TrySetCanceled(cancellationToken);
                } else if (p == AsyncStatus.Error) {
                    Trace.WriteLine("Deployment failed: " + v.GetResults().ErrorText + " (error code " + v.GetResults().ExtendedErrorCode.HResult + ")");
                    src.TrySetException(new Exception("Deployment failed: " + v.GetResults().ErrorText));
                } else {
                    Trace.WriteLine("Deployment done: " + p);
                    src.TrySetResult(1);
                }
            };
            await src.Task;
        }

        private int GetWorldCountInDataDir(string dataDir) {
            var worldsFolder = Path.Combine(dataDir, "games", "com.mojang", "minecraftWorlds");
            if (!Directory.Exists(worldsFolder)) {
                return 0;
            }
            return Directory.GetDirectories(worldsFolder).Length;
        }

        private Dictionary<string, int> LocateMinecraftWorlds(string packageFamily) {
            List<string> candidates = new List<string>();

            var uwpDataDir = GetMinecraftUWPDataDir(packageFamily);
            if (uwpDataDir != "") {
                candidates.Add(uwpDataDir);
            }

            candidates.AddRange(GetMinecraftGDKDataDirs(packageFamily));
            candidates.Add(GetBackupMinecraftDataDir());

            var worldLocations = new Dictionary<string, int>();
            foreach(var dataDir in candidates) {
                Trace.WriteLine("Checking for worlds in: " + dataDir);
                var worldsFolder = Path.Combine(dataDir, "games", "com.mojang", "minecraftWorlds");
                if (!Directory.Exists(worldsFolder)) {
                    Trace.WriteLine("No worlds found in: " + worldsFolder);
                    continue;
                }
                int worlds = Directory.GetDirectories(worldsFolder).Length;
                if (worlds > 0) {
                    worldLocations[dataDir] = worlds;
                    Trace.WriteLine("Found " + worlds + " worlds in: " + worldsFolder);
                } else {
                    Trace.WriteLine("No worlds found in: " + worldsFolder);
                }
            }

            return worldLocations;
        }

        private string GetBackupMinecraftDataDir() {
            //TODO: this really ought to be separated by package family
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string tmpDir = Path.Combine(localAppData, "TmpMinecraftLocalState");
            return tmpDir;
        }

        private string GetMinecraftUWPRootDir(string packageFamily) {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Packages",
                packageFamily
            );
        }

        private string GetMinecraftUWPDataDir(string packageFamily) {
            return Path.Combine(GetMinecraftUWPRootDir(packageFamily), "LocalState");
        }

        private string GetMinecraftGDKRootDir(string packageFamily) {
            string infix;
            switch(packageFamily) {
                case MinecraftPackageFamilies.MINECRAFT:
                    infix = "Minecraft Bedrock";
                    break;
                case MinecraftPackageFamilies.MINECRAFT_PREVIEW:
                    infix = "Minecraft Bedrock Preview";
                    break;
                default: throw new ArgumentException("Invalid Minecraft package family: " + packageFamily);
            }
            var gdkRootDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                infix
            );
            return gdkRootDir;
        }

        private List<string> GetMinecraftGDKDataDirs(string packageFamily) {
            var parentDir = Path.Combine(
                GetMinecraftGDKRootDir(packageFamily),
                "Users"
            );
            var results = new List<string>();

            if (!Directory.Exists(parentDir)) {
                Trace.WriteLine("GDK Users directory doesn't exist: " + parentDir);
                return results;
            }

            results.AddRange(Directory.EnumerateDirectories(parentDir));

            return results;
        }

        private bool BackupMinecraftDataForRemoval(string packageFamily) {
            ApplicationData data;
            try {
                data = ApplicationDataManager.CreateForPackageFamily(packageFamily);
            }catch (FileNotFoundException e) {
                Trace.WriteLine("BackupMinecraftDataForRemoval: Application data not found for package family " + packageFamily + ": " + e.ToString());
                Trace.WriteLine("This should mean the package isn't installed, so we don't need to backup the data");
                return true;
            }
            if (!Directory.Exists(data.LocalFolder.Path)) {
                //this is fine only for GDK versions
                Trace.WriteLine("LocalState folder " + data.LocalFolder.Path + " doesn't exist, so it can't be backed up");
                return true;
            }
            string tmpDir = GetBackupMinecraftDataDir();
            if (Directory.Exists(tmpDir)) {
                if (GetWorldCountInDataDir(tmpDir) > 0) {
                    Trace.WriteLine("BackupMinecraftDataForRemoval: " + tmpDir + " already exists with worlds");
                    var res = ThemedMessageBox.Show(
                        "A previous backup of your Minecraft worlds was found in temporary storage.\n\n" +
                        "Would you like to preserve these worlds and continue the installation?\n\n" +
                        "Click 'Yes' to safely keep your worlds and proceed, or 'No' to cancel and inspect them in File Explorer.",
                        "Saved Worlds Backup Found",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question
                    );
                    if (res == MessageBoxResult.Yes) {
                        return true;
                    } else {
                        try { Process.Start("explorer.exe", tmpDir); } catch { }
                        return false;
                    }
                }
                Directory.Delete(tmpDir, recursive: true);
            }
            Trace.WriteLine("Moving Minecraft data to: " + tmpDir);
            Directory.Move(data.LocalFolder.Path, tmpDir);

            return true;
        }

        private void RestoreMove(string from, string to) {
            foreach (var f in Directory.EnumerateFiles(from)) {
                string ft = Path.Combine(to, Path.GetFileName(f));
                if (File.Exists(ft)) {
                    if (ThemedMessageBox.Show("The file " + ft + " already exists in the destination.\nDo you want to replace it? The old file will be lost otherwise.", "Restoring data directory from previous installation", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                        continue;
                    File.Delete(ft);
                }
                File.Move(f, ft);
            }
            foreach (var f in Directory.EnumerateDirectories(from)) {
                string tp = Path.Combine(to, Path.GetFileName(f));
                if (!Directory.Exists(tp)) {
                    if (File.Exists(tp) && ThemedMessageBox.Show("The file " + tp + " is not a directory. Do you want to remove it? The data from the old directory will be lost otherwise.", "Restoring data directory from previous installation", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                        continue;
                    Directory.CreateDirectory(tp);
                }
                RestoreMove(f, tp);
            }
        }

        private bool RestoreUWPData(string src, string uwpDataDir, string uwpParent) {
            Trace.WriteLine("Restoring Minecraft data from src dir " + src + " to " + uwpDataDir);
            try {
                if (Directory.Exists(uwpDataDir)) {
                    Trace.WriteLine("Deleting: " + uwpDataDir);
                    Directory.Delete(uwpDataDir, recursive: true);
                }
                if (!Directory.Exists(uwpParent)) {
                    Trace.WriteLine("Creating parent dir: " + uwpParent);
                    Directory.CreateDirectory(uwpParent);
                }
                Trace.WriteLine("Restoring files");
                RestoreMove(src, uwpDataDir);
                Trace.WriteLine("Deleting src dir: " + src);
                Directory.Delete(src, true);
                Trace.WriteLine("Restore complete");
                return true;
            } catch (Exception e) {
                Trace.WriteLine("Failed restoring Minecraft data from " + src + ": " + e.ToString());
                ThemedMessageBox.Show("Failed to move Minecraft data from:\n"
                    + src
                    + "\nto:\n"
                    + uwpDataDir
                    + "\n\nCheck the log file for more information.", "Data restore error"
                );
                return false;
            }
        }

        private bool MoveMinecraftData(string packageFamily, PackageType destinationType) {
            var dataLocations = LocateMinecraftWorlds(packageFamily);
            if (dataLocations.Count == 0) {
                Trace.WriteLine("No Minecraft data found to restore or link");
                return true;
            }

            string gdkRoot = GetMinecraftGDKRootDir(packageFamily);
            string uwpDataDir = GetMinecraftUWPDataDir(packageFamily);

            if (dataLocations.Count > 1) {
                var messageString = "";
                foreach (var loc in dataLocations) {
                    messageString += $"\n - {loc.Key}: {loc.Value} worlds";
                }

                //GDK might put worlds in several places, but this is fine as long as the target version is also GDK
                if (destinationType == PackageType.GDK) {
                    Trace.WriteLine("Checking for dirs with prefix: " + gdkRoot);
                    bool gdkOnly = true;
                    foreach (var loc in dataLocations) {
                        if (!loc.Key.StartsWith(gdkRoot)) {
                            gdkOnly = false;
                            Trace.WriteLine("Folder " + loc.Key + " doesn't start with " + gdkRoot);
                            break;
                        } else {
                            Trace.WriteLine("Folder " + loc.Key + " is in GDK data, ignoring");
                        }
                    }

                    if (gdkOnly) {
                        Trace.WriteLine("Worlds found in multiple places, but all of them are GDK:" + messageString);
                        Trace.WriteLine("This is fine since the target version is also GDK, doing nothing");
                        return true;
                    }
                }

                Trace.WriteLine("Can't automatically restore Minecraft data - multiple locations with worlds found:" + messageString);

                string destinationFolder = destinationType == PackageType.UWP ? uwpDataDir : Path.Combine(gdkRoot, "Users");
                
                bool proceed = Dispatcher.Invoke(() => {
                    var dialog = new WorldConflictDialog(dataLocations, destinationFolder);
                    dialog.Owner = this;
                    return dialog.ShowDialog() == true;
                });
                return proceed;
            }

            string dataLocation = dataLocations.Keys.First();

            string tmpDir = GetBackupMinecraftDataDir();

            string uwpParent = GetMinecraftUWPRootDir(packageFamily);
            if (dataLocation == tmpDir) {
                //we don't know where GDK will want to store this due to the user folder names containing some kind of UID
                //so we restore to UWP location and let Minecraft handle the GDK migration by itself
                Trace.WriteLine("Restoring Minecraft data from backup dir " + tmpDir + " to " + uwpDataDir);
                if (!RestoreUWPData(tmpDir, uwpDataDir, uwpParent)) {
                    return false;
                }
                dataLocation = uwpDataDir;
            }

            if (destinationType == PackageType.GDK && dataLocation == uwpDataDir) {
                //TODO: not sure it's a good idea to let the game migrate UWP data on its own,
                //considering how many people have had problems with it???
                Trace.WriteLine("Deleting uwpMigration.dat, so GDK Minecraft will migrate data from UWP next time it's used");
                var uwpMigrationDat = Path.Combine(
                    GetMinecraftGDKRootDir(packageFamily),
                    "games",
                    "com.mojang",
                    "uwpMigration.dat"
                );
                Trace.WriteLine("uwpMigration.dat path: " + uwpMigrationDat);
                try {
                    File.Delete(uwpMigrationDat);
                    return true;
                } catch (Exception e) {
                    Trace.WriteLine("Failed deleting uwpMigration.dat: " + e.ToString());
                    ThemedMessageBox.Show(
                        "Failed deleting uwpMigration.dat file.\n" +
                        "Your worlds will be visible to UWP versions, but GDK versions won't see them unless you move them back manually.\n\n" +
                        "Please delete the following file manually: " + uwpMigrationDat +
                        "\n\nAlternatively, you can copy your worlds back to the GDK folder next time you run a GDK version." +
                        "\nYour worlds are currently located at: " + uwpDataDir +
                        "Data migration notice"
                    );
                    return false;
                }
            } else if (destinationType == PackageType.UWP && dataLocation != uwpDataDir) {
                //this should mean that we found data in a GDK location, so move it for UWP
                var gdkDataDir = dataLocations.Keys.First();
                if (!RestoreUWPData(gdkDataDir, uwpDataDir, uwpParent)) {
                    return false;
                }

                return true;
            } else {
                Trace.WriteLine("Minecraft data already in the right place " + dataLocation);
                return true;
            }
        }

        private async Task RemovePackage(Package pkg, string packageFamily, Version? version, bool skipBackup) {
            Trace.WriteLine("Removing package: " + pkg.Id.FullName);
            if (!pkg.IsDevelopmentMode) {
                if (!skipBackup) {
                    //TODO: It would be nice to skip this if we're uninstalling a GDK version
                    //however, the package being removed may not be the one passed in the version parameter
                    //since the version parameter is only used for displaying UI status
                    if (!BackupMinecraftDataForRemoval(packageFamily)) {
                        throw new Exception("Failed backing up Minecraft data before uninstalling package");
                    }
                }
                //TODO: this will bomb data for other users. We only currently backup data for the current user
                await DeploymentProgressWrapper(new PackageManager().RemovePackageAsync(pkg.Id.FullName, RemovalOptions.RemoveForAllUsers), version);
            } else {
                Trace.WriteLine("Package is in development mode");
                await DeploymentProgressWrapper(new PackageManager().RemovePackageAsync(pkg.Id.FullName, RemovalOptions.PreserveApplicationData | RemovalOptions.RemoveForAllUsers), version);
            }
            Trace.WriteLine("Removal of package done: " + pkg.Id.FullName);
        }

        private string GetPackagePath(Package pkg) {
            try {
                return pkg.InstalledLocation.Path;
            } catch (FileNotFoundException) {
                return "";
            }
        }

        private async Task UnregisterPackage(string packageFamily, Version? version, bool skipBackup) {
            foreach (var pkg in new PackageManager().FindPackages(packageFamily)) {
                string location = GetPackagePath(pkg);
                Trace.WriteLine("Removing package: " + pkg.Id.FullName + " " + location);
                await RemovePackage(pkg, packageFamily, version, skipBackup);
            }
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CreateHardLink(
            string lpFileName,
            string lpExistingFileName,
            IntPtr lpSecurityAttributes
        );

        private async Task ReRegisterPackage(string packageFamily, string gameDir, Version version) {
            Trace.WriteLine("Registering package");
            string manifestPath = Path.Combine(gameDir, "AppxManifest.xml");

            bool updateManifest = false;

            if (version.PackageType == PackageType.GDK) {
                string shimPath = Path.Combine(gameDir, GDK_SHIM_NAME);

                string backupManifestPath = Path.Combine(gameDir, "AppxManifest_original.xml");
                bool hasManifestBackup = File.Exists(backupManifestPath);

                if (!File.Exists(shimPath)) {
                    updateManifest = true;
                    if (hasManifestBackup) {
                        //we need to redo the manifest for older versions that didn't have the shim present
                        File.Copy(backupManifestPath, manifestPath, overwrite: true);
                        Trace.WriteLine("Manifest needs re-patching because GDK launch shim has been added to an old install");
                    } else {
                        Trace.WriteLine("Adding launch shim to new GDK install");
                    }
                } else {
                    Trace.WriteLine("Updating GDK launch shim");
                }

                string shimSourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, GDK_SHIM_NAME);
                File.Delete(shimPath);
                if (CreateHardLink(shimPath, shimSourcePath, IntPtr.Zero)) {
                    //hardlink is way less annoying for development, in theory
                    Trace.WriteLine("Successfully hardlinked GDK launch shim");
                } else {
                    File.Copy(shimSourcePath, shimPath, overwrite: true);
                    Trace.WriteLine("Couldn't create hard link for GDK shim, copying instead");
                }

                //avoid patching the manifest unless necessary, the user might have edited it
                if (updateManifest) {
                    Trace.WriteLine("Patching AppxManifest.xml");
                    if (!hasManifestBackup) {
                        Trace.WriteLine("Backing up original manifest");
                        File.Copy(manifestPath, backupManifestPath);
                    }
                    FixGDKManifest(manifestPath);
                }
            }

            foreach (var pkg in new PackageManager().FindPackages(packageFamily)) {
                string location = GetPackagePath(pkg);
                if (location == gameDir && !updateManifest) {
                    Trace.WriteLine("Skipping package removal - same path: " + pkg.Id.FullName + " " + location);
                    return;
                }
                await RemovePackage(pkg, packageFamily, version, skipBackup: false);
            }

            Trace.WriteLine("Manifest path: " + manifestPath);
            await DeploymentProgressWrapper(new PackageManager().RegisterPackageAsync(new Uri(manifestPath), null, DeploymentOptions.DevelopmentMode), version);
            Trace.WriteLine("App re-register done!");
        }

        private void InvokeDownload(Version v) {
            string defaultDir = Path.GetFullPath((v.VersionType == VersionType.Preview ? "Minecraft-Preview-" : "Minecraft-") + v.Name);
            var dialog = new InstallLocationDialog(v.DisplayName, defaultDir, v.Name) {
                Owner = this
            };
            if (dialog.ShowDialog() != true) {
                return;
            }

            string chosenDir = dialog.SelectedPath;
            v.GameDirectory = chosenDir;

            if (!string.Equals(Path.GetFullPath(chosenDir), defaultDir, StringComparison.OrdinalIgnoreCase)) {
                UserPrefs.CustomInstallDirectories[v.Name] = chosenDir;
                RewritePrefs();
            } else {
                if (UserPrefs.CustomInstallDirectories.ContainsKey(v.Name)) {
                    UserPrefs.CustomInstallDirectories.Remove(v.Name);
                    RewritePrefs();
                }
            }

            CancellationTokenSource cancelSource = new CancellationTokenSource();
            v.IsNew = false;
            v.StateChangeInfo = new VersionStateChangeInfo(VersionState.Initializing);
            v.StateChangeInfo.CancelCommand = new RelayCommand((o) => cancelSource.Cancel());

            Trace.WriteLine("Download start");
            Task.Run(async () => {
                string dlExt = (v.PackageType == PackageType.UWP ? ".Appx" : ".msixvc");
                string parentOfChosen = Path.GetDirectoryName(chosenDir) ?? Directory.GetCurrentDirectory();
                string dlPath = Directory.Exists(parentOfChosen)
                    ? Path.Combine(parentOfChosen, (v.VersionType == VersionType.Preview ? "Minecraft-Preview-" : "Minecraft-") + v.Name + dlExt)
                    : Path.GetFullPath((v.VersionType == VersionType.Preview ? "Minecraft-Preview-" : "Minecraft-") + v.Name + dlExt);

                VersionDownloader downloader = _anonVersionDownloader;
                if (v.VersionType == VersionType.Beta) {
                    downloader = _userVersionDownloader;
                    if (Interlocked.CompareExchange(ref _userVersionDownloaderLoginTaskStarted, 1, 0) == 0) {
                        _userVersionDownloaderLoginTask.Start();
                    }
                    Trace.WriteLine("Waiting for authentication");
                    try {
                        await _userVersionDownloaderLoginTask;
                        Trace.WriteLine("Authentication complete");
                    } catch (WUTokenHelper.WUTokenException e) {
                        Trace.WriteLine("Authentication failed:\n" + e.ToString());
                        ThemedMessageBox.Show("Failed to authenticate because: " + e.Message, "Authentication failed", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    } catch (Exception e) {
                        Trace.WriteLine("Authentication failed:\n" + e.ToString());
                        ThemedMessageBox.Show(e.ToString(), "Authentication failed", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                VersionDownloader.DownloadProgress dlProgressHandler = (current, total) => {
                    var info = v.StateChangeInfo;
                    if (info == null) return;
                    if (info.VersionState != VersionState.Downloading) {
                        Trace.WriteLine("Actual download started");
                        info.VersionState = VersionState.Downloading;
                        if (total.HasValue && total.Value > 0)
                            info.MaxProgress = total.Value;
                    }
                    if (info.MaxProgress == 0 && total.HasValue && total.Value > 0) {
                        info.MaxProgress = total.Value;
                    }
                    info.Progress = current;
                };

                try {
                    if (v.PackageType == PackageType.UWP) {
                        await downloader.DownloadAppx(v.UUID, "1", dlPath, dlProgressHandler, cancelSource.Token);
                    } else if (v.PackageType == PackageType.GDK) {
                        if (!ShowGDKFirstUseWarning()) {
                            return;
                        }
                        await downloader.DownloadMsixvc(v.DownloadURLs, dlPath, dlProgressHandler, cancelSource.Token);
                    } else {
                        throw new Exception("Unknown package type");
                    }
                    Trace.WriteLine("Download complete");

                    string dirPath = v.GameDirectory;
                    if (Directory.Exists(dirPath))
                        Directory.Delete(dirPath, true);
                    bool extractSuccess = false;
                    if (v.PackageType == PackageType.UWP) {
                        extractSuccess = await ExtractAppx(dlPath, dirPath, v, cancelSource.Token);
                    } else if (v.PackageType == PackageType.GDK) {
                        extractSuccess = await ExtractMsixvc(dlPath, dirPath, v, isPreview: v.VersionType == VersionType.Preview, cancellationToken: cancelSource.Token);
                    } else {
                        throw new Exception("Unknown package type");
                    }
                    if (extractSuccess && UserPrefs.DeleteAppxAfterDownload) {
                        Trace.WriteLine("Deleting package to reduce disk usage");
                        File.Delete(dlPath);
                    } else {
                        Trace.WriteLine("Not deleting package due to user preferences or extraction failure");
                    }
                } catch (BadUpdateIdentityException) {
                    Trace.WriteLine("Download failed due to failure to fetch download URL");
                    string msg = (v.VersionType == VersionType.Beta)
                        ? "Unable to fetch download URL for beta version.\n\nPlease make sure your account is subscribed to the Minecraft beta programme in the Xbox Insider Hub app."
                        : $"Unable to download {v.DisplayName} ({v.Name}).\n\nMicrosoft has removed the download files for this legacy version from Windows Update servers.\nYou can still install and play it by obtaining the .Appx file and clicking '+ Import .appx'.";
                    ThemedMessageBox.Show(msg, "Version Unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
                } catch (OperationCanceledException) {
                    Trace.WriteLine("Download was cancelled by user.");
                } catch (Exception e) {
                    Trace.WriteLine("Download or extraction failed:\n" + e.ToString());
                    ThemedMessageBox.Show("Operation failed:\n" + e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                } finally {
                    v.StateChangeInfo = null;
                    v.UpdateInstallStatus();
                }
            });
        }

        private async Task<bool> Remove(Version v) {
            try {
                if (UserPrefs?.CustomInstallDirectories != null && UserPrefs.CustomInstallDirectories.ContainsKey(v.Name)) {
                    UserPrefs.CustomInstallDirectories.Remove(v.Name);
                    RewritePrefs();
                }

                v.StateChangeInfo = new VersionStateChangeInfo(VersionState.Unregistering);
                Trace.WriteLine("Unregistering version " + v.DisplayName);
                try {
                    int unregistered = 0;
                    foreach (var pkg in new PackageManager().FindPackages(v.GamePackageFamily)) {
                        string location = GetPackagePath(pkg);
                        if (location == "" || Path.GetFullPath(location) == Path.GetFullPath(v.GameDirectory)) {
                            Trace.WriteLine("Removing package: " + pkg.Id.FullName + " " + location);
                            await RemovePackage(pkg, v.GamePackageFamily, v, skipBackup: false);
                            unregistered++;
                        }
                    }
                    if (unregistered == 0) {
                        Trace.WriteLine($"Looks like {v.GameDirectory} is not registered with the system, no unregistering performed");
                    }
                } catch (Exception e) {
                    Trace.WriteLine("Failed unregistering package:\n" + e.ToString());
                    ThemedMessageBox.Show("Failed unregistering package:\n" + e.ToString(), "Uninstall error");
                    return false;
                }
                Trace.WriteLine("Cleaning up game files for version " + v.DisplayName);
                v.StateChangeInfo = new VersionStateChangeInfo(VersionState.CleaningUp);
                try {
                    // Use the \\?\ prefix to support long paths
                    Directory.Delete(@"\\?\" + Path.GetFullPath(v.GameDirectory), true);
                } catch (Exception e) {
                    Trace.WriteLine("Failed deleting game directory:\n" + e.ToString());
                    ThemedMessageBox.Show("Failed deleting game directory:\n" + e.ToString(), "Uninstall error");
                    return false;
                }

                if (v.IsImported) {
                    Dispatcher.Invoke(() => _versions.Remove(v));
                    Trace.WriteLine("Removed imported version " + v.DisplayName);
                } else {
                    v.GameDirectory = (v.VersionType == VersionType.Preview ? "Minecraft-Preview-" : "Minecraft-") + v.Name;
                    v.UpdateInstallStatus();
                    Trace.WriteLine("Removed release version " + v.DisplayName);
                }

                return true;
            } finally { 
                v.StateChangeInfo = null;
            }
        }

        private void InvokeRemove(Version v) {
            Task.Run(async () => await Remove(v));
        }

        private void ShowInstalledVersionsOnlyCheckbox_Changed(object sender, RoutedEventArgs e) {
            UserPrefs.ShowInstalledOnly = ShowInstalledVersionsOnlyCheckbox.IsChecked ?? false;
            RefreshLists();
            RewritePrefs();
        }

        private void RefreshLists() {
            if (!Dispatcher.CheckAccess()) {
                Dispatcher.InvokeAsync(RefreshLists);
                return;
            }
            foreach (var list in _versionListViews) {
                list.View.Refresh();
            }
        }

        private void DeleteAppxAfterDownloadCheck_Changed(object sender, RoutedEventArgs e) {
            UserPrefs.DeleteAppxAfterDownload = DeleteAppxAfterDownloadOption.IsChecked;
            RewritePrefs();
        }

        private void AutoFetchDecryptionKeysCheck_Changed(object sender, RoutedEventArgs e) {
            UserPrefs.AutoFetchDecryptionKeys = AutoFetchDecryptionKeysOption.IsChecked;
            RewritePrefs();
        }

        private void RewritePrefs() {
            File.WriteAllText(PREFS_PATH, JsonConvert.SerializeObject(UserPrefs));
        }

        private void MenuItemOpenLogFileClicked(object sender, RoutedEventArgs e) {
            if (!File.Exists(@"Log.txt")) {
                ThemedMessageBox.Show("Log file not found", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            } else 
                Process.Start(new ProcessStartInfo
                {
                    FileName = @"Log.txt",
                    UseShellExecute = true
                });
        }

        private void MenuItemOpenDataDirClicked(object sender, RoutedEventArgs e) {
            Process.Start(@"explorer.exe", Directory.GetCurrentDirectory());
        }

        private void MenuItemUninstallAllVersionsClicked(object sender, RoutedEventArgs e) {
            var result = ThemedMessageBox.Show(
                "All versions of Minecraft managed by the launcher will be unregistered and deleted.\n" +
                    "Your data (worlds, etc.) won't be removed.\n\n" +
                    "Note: If you just want to reinstall Minecraft from the Store, and don't want to delete your launcher-managed versions, you can use the \"Cleanup for Store reinstall\" option instead.\n\n" +
                    "Are you sure you want to continue?",
                "Uninstall all versions",
                MessageBoxButton.OKCancel
            );
            if (result == MessageBoxResult.OK) {
                Trace.WriteLine("Starting uninstall of ALL versions!");
                foreach (var version in _versions) {
                    if (version.IsInstalled) {
                        InvokeRemove(version);
                    }
                }
                Trace.WriteLine("Scheduled uninstall of ALL versions.");
            }
        }

        private void MenuItemRefreshVersionListClicked(object sender, RoutedEventArgs e) {
            Dispatcher.Invoke(LoadVersionList);
        }

        private void onEndpointChangedHandler(object sender, string newUwpIdsEndpoint, string newGdkPackageUrlsEndpoint) {
            UserPrefs.VersionsApiUWP = newUwpIdsEndpoint == "" ? VERSIONS_API_UWP : newUwpIdsEndpoint;
            UserPrefs.VersionsApiGDK = newGdkPackageUrlsEndpoint == "" ? VERSIONS_API_GDK : newGdkPackageUrlsEndpoint;
            Dispatcher.Invoke(LoadVersionList);
            RewritePrefs();
        }

        private void MenuItemSetVersionListEndpointClicked(object sender, RoutedEventArgs e) {
            var dialog = new VersionListEndpointDialog(UserPrefs.VersionsApiUWP, UserPrefs.VersionsApiGDK) {
                Owner = this
            };
            dialog.OnEndpointChanged += onEndpointChangedHandler;

            dialog.Show();
        }

        private string buildDataLocationMessage(string displayName, string packageFamily) {
            var message = "Data for " + displayName + ":";
            var locations = LocateMinecraftWorlds(packageFamily);
            if (locations.Count == 0) {
                return message + "\n - (no folders with worlds found)";
            }

            foreach (var loc in locations) {
                message += $"\n - {loc.Value} worlds found in {loc.Key}";
            }
            return message;
        }

        private void MenuItemFindMyDataClicked(object sender, RoutedEventArgs e) {
            var locations = LocateMinecraftWorlds(MinecraftPackageFamilies.MINECRAFT);

            ThemedMessageBox.Show(
                buildDataLocationMessage("Release", MinecraftPackageFamilies.MINECRAFT) + "\n\n" +
                buildDataLocationMessage("Preview", MinecraftPackageFamilies.MINECRAFT_PREVIEW) + "\n\n" +
                "Note: Data folders containing no worlds are not shown.",
                "Minecraft data locations"
            );
        }

        private async void MenuItemCleanupForStoreInstallClicked(object sender, RoutedEventArgs e) {
            var dialog = new ProgressDialog();
            dialog.Owner = this;
            bool allowClose = false;
            dialog.Closing += (object? sender_, CancelEventArgs e_) => {
                if (!allowClose) {
                    e_.Cancel = true;
                }
            };

            dialog.Show();

            Trace.WriteLine("Cleaning up system");
            try {
                await UnregisterPackage(MinecraftPackageFamilies.MINECRAFT, null, skipBackup: false);
                await UnregisterPackage(MinecraftPackageFamilies.MINECRAFT_PREVIEW, null, skipBackup: false);
            } catch (Exception ex) {
                Trace.WriteLine("Error cleaning up: " + ex.Message);
                ThemedMessageBox.Show("An error occurred while cleaning up. Check the log for details.", "Error");
            }
            Trace.WriteLine("Done cleaning up");
            allowClose = true;
            dialog.Close();
            ThemedMessageBox.Show("Cleanup completed. You should now be able to install Minecraft from Microsoft Store.", "Cleanup completed");
        }

        private void TabNav_Checked(object sender, RoutedEventArgs e) {
            if (PlayViewGrid == null || InstallationsViewGrid == null || PatchNotesViewGrid == null) return;
            bool isPlay = (TabNavPlay?.IsChecked == true);
            bool isBrowserOpen = (InAppBrowserOverlay != null && InAppBrowserOverlay.Visibility == Visibility.Visible);

            PlayViewGrid.Visibility = (isPlay && !isBrowserOpen) ? Visibility.Visible : Visibility.Collapsed;
            if (ServerCardBorder != null) ServerCardBorder.Visibility = (isPlay && !isBrowserOpen) ? Visibility.Visible : Visibility.Collapsed;
            if (ServersWebView != null) ServersWebView.Visibility = (isPlay && !isBrowserOpen) ? Visibility.Visible : Visibility.Collapsed;

            InstallationsViewGrid.Visibility = (TabNavInstallations.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
            PatchNotesViewGrid.Visibility = (TabNavPatchNotes.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SubFilter_Checked(object sender, RoutedEventArgs e) {
            if (VersionTabs == null) return;
            if (SubFilterAll != null && SubFilterAll.IsChecked == true) {
                VersionTabs.SelectedItem = ReleaseTab;
            } else if (SubFilterRelease != null && SubFilterRelease.IsChecked == true) {
                VersionTabs.SelectedItem = ReleaseTab;
            } else if (SubFilterPreview != null && SubFilterPreview.IsChecked == true) {
                VersionTabs.SelectedItem = PreviewTab;
            } else if (SubFilterBeta != null && SubFilterBeta.IsChecked == true) {
                VersionTabs.SelectedItem = BetaTab;
            } else if (SubFilterImported != null && SubFilterImported.IsChecked == true) {
                VersionTabs.SelectedItem = ImportedTab;
            }
        }

        private Version? _selectedQuickPlayVersion = null;

        public class LocalVersionCandidate {
            public string Name { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public string? PackageFilePath { get; set; }
            public string? InstalledDirectory { get; set; }
            public PackageType PackageType { get; set; }
            public VersionType VersionType { get; set; }
            public Version? VersionEntry { get; set; }
            public bool IsInstalled => !string.IsNullOrEmpty(InstalledDirectory) && Directory.Exists(InstalledDirectory);
            public bool IsActive { get; set; }
        }

        private Version? GetCurrentlyActiveVersion() {
            if (_selectedQuickPlayVersion != null && _selectedQuickPlayVersion.IsInstalled) {
                return _selectedQuickPlayVersion;
            }

            try {
                foreach (var family in new[] { MinecraftPackageFamilies.MINECRAFT, MinecraftPackageFamilies.MINECRAFT_PREVIEW }) {
                    foreach (var pkg in new PackageManager().FindPackages(family)) {
                        string loc = GetPackagePath(pkg);
                        if (!string.IsNullOrEmpty(loc)) {
                            var match = _versions.FirstOrDefault(v => string.Equals(Path.GetFullPath(v.GameDirectory), Path.GetFullPath(loc), StringComparison.OrdinalIgnoreCase));
                            if (match != null) {
                                return match;
                            }
                        }
                    }
                }
            } catch {
            }

            return _versions.FirstOrDefault(v => v.IsInstalled);
        }

        private CancellationTokenSource? _heroCancelSource = null;
        private bool _isHeroOperationRunning = false;

        private void ShowHeroProgress(string action, string initialStatus) {
            if (!Dispatcher.CheckAccess()) {
                Dispatcher.BeginInvoke(() => ShowHeroProgress(action, initialStatus));
                return;
            }
            _isHeroOperationRunning = true;
            if (HeroStatusAction != null) HeroStatusAction.Text = action;
            if (HeroStatusBadge != null) {
                if (action == "DOWNGRADING") {
                    HeroStatusBadge.Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x25, 0x18));
                    HeroStatusBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(0xD6, 0x9E, 0x2E));
                    if (HeroStatusAction != null) HeroStatusAction.Foreground = new SolidColorBrush(Color.FromRgb(0xF6, 0xAD, 0x55));
                } else {
                    HeroStatusBadge.Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x2E, 0x22));
                    HeroStatusBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(0x38, 0xA1, 0x69));
                    if (HeroStatusAction != null) HeroStatusAction.Foreground = new SolidColorBrush(Color.FromRgb(0x38, 0xA1, 0x69));
                }
            }
            if (HeroStatusDetail != null) HeroStatusDetail.Text = initialStatus;
            if (HeroProgressBar != null) {
                HeroProgressBar.Value = 0;
                HeroProgressBar.IsIndeterminate = false;
            }
            if (HeroCancelButton != null) HeroCancelButton.IsEnabled = true;
            if (HeroProgressContainer != null) HeroProgressContainer.Visibility = Visibility.Visible;
            if (QuickPlayButton != null) QuickPlayButton.IsEnabled = false;
        }

        private void UpdateHeroProgress(string statusDetail, double percent, bool isIndeterminate = false) {
            if (!Dispatcher.CheckAccess()) {
                Dispatcher.BeginInvoke(() => UpdateHeroProgress(statusDetail, percent, isIndeterminate));
                return;
            }
            if (!_isHeroOperationRunning) return;
            if (HeroStatusDetail != null) HeroStatusDetail.Text = statusDetail;
            if (HeroProgressBar != null) {
                HeroProgressBar.IsIndeterminate = isIndeterminate;
                if (!isIndeterminate) {
                    HeroProgressBar.Value = Math.Clamp(percent, 0, 100);
                }
            }
        }

        private void HideHeroProgress() {
            if (!Dispatcher.CheckAccess()) {
                Dispatcher.BeginInvoke(HideHeroProgress);
                return;
            }
            _isHeroOperationRunning = false;
            if (_heroCancelSource != null) {
                try { _heroCancelSource.Dispose(); } catch { }
                _heroCancelSource = null;
            }
            if (HeroProgressContainer != null) HeroProgressContainer.Visibility = Visibility.Collapsed;
            if (QuickPlayButton != null) QuickPlayButton.IsEnabled = true;
        }

        private void HeroCancelButton_Click(object sender, RoutedEventArgs e) {
            if (_heroCancelSource != null && !_heroCancelSource.IsCancellationRequested) {
                try {
                    if (HeroStatusDetail != null) HeroStatusDetail.Text = "Cancelling...";
                    if (HeroCancelButton != null) HeroCancelButton.IsEnabled = false;
                    _heroCancelSource.Cancel();
                } catch (Exception ex) {
                    Trace.WriteLine("Hero cancel error: " + ex);
                }
            }
        }

        private void QuickPlayButton_Click(object sender, RoutedEventArgs e) {
            if (_isHeroOperationRunning) return;
            var active = GetCurrentlyActiveVersion();
            if (active != null) {
                ShowHeroProgress("LAUNCHING", "Preparing launch...");
                InvokeLaunch(active);
            } else {
                ThemedMessageBox.Show("No version is currently installed. Please select a version in the Installations tab or choose a package from the version selector to install it.", "BUGrock Launcher", MessageBoxButton.OK, MessageBoxImage.Information);
                TabNavInstallations.IsChecked = true;
            }
        }

        private void UpdateQuickPlayStatus() {
            if (!Dispatcher.CheckAccess()) {
                Dispatcher.InvokeAsync(UpdateQuickPlayStatus);
                return;
            }
            if (QuickPlayVersionLabel == null) return;
            var current = GetCurrentlyActiveVersion();
            if (current != null) {
                string verTypeStr = current.VersionType == VersionType.Preview ? "Preview" : "Release";
                QuickPlayVersionLabel.Text = $"{current.Name} {verTypeStr}";
            } else {
                QuickPlayVersionLabel.Text = "Select Version";
            }
        }

        private static string ExtractVersionNameFromFileName(string fileName) {
            var match = Regex.Match(fileName, @"(?:Minecraft-(?:Preview-)?|Minecraft\.Windows-)?([0-9]+(?:\.[0-9]+)+)");
            if (match.Success) {
                return match.Groups[1].Value;
            }
            string noExt = Path.GetFileNameWithoutExtension(fileName);
            noExt = Regex.Replace(noExt, @"^(Minecraft-Preview-|Minecraft-|Minecraft\.Windows-)", "", RegexOptions.IgnoreCase);
            return noExt;
        }

        private int CompareVersionStrings(string v1, string? v2) {
            if (string.IsNullOrEmpty(v2)) return 1;
            if (string.IsNullOrEmpty(v1)) return -1;
            var matches1 = Regex.Matches(v1, @"\d+");
            var matches2 = Regex.Matches(v2, @"\d+");
            int count = Math.Max(matches1.Count, matches2.Count);
            for (int i = 0; i < count; i++) {
                int val1 = i < matches1.Count ? int.Parse(matches1[i].Value) : 0;
                int val2 = i < matches2.Count ? int.Parse(matches2[i].Value) : 0;
                if (val1 != val2) return val1.CompareTo(val2);
            }
            return 0;
        }

        private Version GetOrCreateVersionEntry(LocalVersionCandidate candidate) {
            if (candidate.VersionEntry != null) return candidate.VersionEntry;

            var existing = _versions.FirstOrDefault(v => v.Name == candidate.Name && v.PackageType == candidate.PackageType);
            if (existing != null) {
                candidate.VersionEntry = existing;
                return existing;
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string targetDir = candidate.InstalledDirectory ?? Path.Combine(baseDir, (candidate.VersionType == VersionType.Preview ? "Minecraft-Preview-" : "Minecraft-") + candidate.Name);
            var newVer = new WPFDataTypes.Version(candidate.Name, targetDir, this, candidate.PackageType) {
                VersionType = candidate.VersionType
            };
            _versions.Add(newVer);
            candidate.VersionEntry = newVer;
            return newVer;
        }

        private List<LocalVersionCandidate> ScanLocalVersions() {
            var candidates = new Dictionary<string, LocalVersionCandidate>(StringComparer.OrdinalIgnoreCase);

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string curDir = Directory.GetCurrentDirectory();
            var dirsToScan = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { baseDir, curDir };

            var currentActive = GetCurrentlyActiveVersion();

            // 1. Scan for package files (*.appx, *.msixvc)
            foreach (var dir in dirsToScan) {
                if (!Directory.Exists(dir)) continue;
                try {
                    var files = Directory.EnumerateFiles(dir)
                        .Where(f => f.EndsWith(".appx", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".msixvc", StringComparison.OrdinalIgnoreCase));

                    foreach (var file in files) {
                        string fileName = Path.GetFileName(file);
                        string ext = Path.GetExtension(file).ToLowerInvariant();
                        var pkgType = ext == ".msixvc" ? PackageType.GDK : PackageType.UWP;
                        var verType = fileName.Contains("Preview", StringComparison.OrdinalIgnoreCase) ? VersionType.Preview : VersionType.Release;

                        string verName = ExtractVersionNameFromFileName(fileName);
                        if (string.IsNullOrEmpty(verName)) continue;

                        string expectedDir = Path.Combine(dir, (verType == VersionType.Preview ? "Minecraft-Preview-" : "Minecraft-") + verName);
                        if (!Directory.Exists(expectedDir)) {
                            string altDir = Path.Combine(baseDir, (verType == VersionType.Preview ? "Minecraft-Preview-" : "Minecraft-") + verName);
                            if (Directory.Exists(altDir)) expectedDir = altDir;
                        }

                        var candidate = new LocalVersionCandidate {
                            Name = verName,
                            DisplayName = $"{verName} ({(verType == VersionType.Preview ? "Preview" : "Release")})",
                            PackageFilePath = file,
                            InstalledDirectory = Directory.Exists(expectedDir) ? expectedDir : null,
                            PackageType = pkgType,
                            VersionType = verType
                        };

                        var match = _versions.FirstOrDefault(v => v.Name == verName && v.PackageType == pkgType);
                        if (match != null) {
                            candidate.VersionEntry = match;
                            if (match.IsInstalled && string.IsNullOrEmpty(candidate.InstalledDirectory)) {
                                candidate.InstalledDirectory = match.GameDirectory;
                            }
                        }

                        candidates[verName] = candidate;
                    }
                } catch (Exception ex) {
                    Trace.WriteLine("Error scanning packages: " + ex.Message);
                }
            }

            // 2. Scan installed versions from _versions
            foreach (var v in _versions) {
                if (v.IsInstalled && !candidates.ContainsKey(v.Name)) {
                    candidates[v.Name] = new LocalVersionCandidate {
                        Name = v.Name,
                        DisplayName = $"{v.Name} ({(v.VersionType == VersionType.Preview ? "Preview" : "Release")})",
                        InstalledDirectory = v.GameDirectory,
                        PackageType = v.PackageType,
                        VersionType = v.VersionType,
                        VersionEntry = v
                    };
                } else if (v.IsInstalled && candidates.TryGetValue(v.Name, out var c)) {
                    c.VersionEntry = v;
                    c.InstalledDirectory = v.GameDirectory;
                }
            }

            // 3. Mark current active
            if (currentActive != null && candidates.TryGetValue(currentActive.Name, out var activeCand)) {
                activeCand.IsActive = true;
            }

            return candidates.Values
                .OrderByDescending(c => c.IsActive)
                .ThenByDescending(c => c.Name, Comparer<string>.Create((a, b) => CompareVersionStrings(a, b)))
                .ToList();
        }

        private bool _wasPopupOpenOnMouseDown = false;

        private void SelectedVersionBox_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left) {
                _wasPopupOpenOnMouseDown = (VersionSelectorPopup != null && VersionSelectorPopup.IsOpen);
            }
        }

        private void SelectedVersionBox_PreviewMouseUp(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton != MouseButton.Left) return;
            e.Handled = true;

            if (_isHeroOperationRunning) return;

            if (_wasPopupOpenOnMouseDown) {
                if (VersionSelectorPopup != null) VersionSelectorPopup.IsOpen = false;
                return;
            }

            PopulateVersionDropdown();
            if (VersionSelectorPopup != null) VersionSelectorPopup.IsOpen = true;
        }

        private void PopulateVersionDropdown() {
            VersionSelectorItemsPanel.Children.Clear();

            var header = new TextBlock {
                Text = "AVAILABLE LOCAL VERSIONS",
                Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x92, 0xA4)),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(6, 4, 6, 8)
            };
            VersionSelectorItemsPanel.Children.Add(header);

            var candidates = ScanLocalVersions();
            if (candidates.Count == 0) {
                VersionSelectorItemsPanel.Children.Add(new TextBlock {
                    Text = "No local .appx or .msixvc packages found in launcher directory.",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x92, 0xA4)),
                    FontSize = 11,
                    Margin = new Thickness(6, 12, 6, 12),
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                return;
            }

            foreach (var candidate in candidates) {
                bool isCurrent = candidate.IsActive;

                var itemBorder = new Border {
                    Background = isCurrent ? new SolidColorBrush(Color.FromRgb(0x1F, 0x2A, 0x24)) : new SolidColorBrush(Color.FromRgb(0x1C, 0x1E, 0x2A)),
                    BorderBrush = isCurrent ? new SolidColorBrush(Color.FromRgb(0x38, 0xA1, 0x69)) : new SolidColorBrush(Color.FromRgb(0x2D, 0x30, 0x42)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 2, 0, 2),
                    Cursor = Cursors.Hand
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var icon = new Image {
                    Width = 18,
                    Height = 18,
                    Margin = new Thickness(0, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                try {
                    icon.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/bugrock_block.png"));
                } catch {
                }
                Grid.SetColumn(icon, 0);
                grid.Children.Add(icon);

                var textPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var titleText = new TextBlock {
                    Text = candidate.Name,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 13
                };
                var subText = new TextBlock {
                    Text = $"{(candidate.VersionType == VersionType.Preview ? "Preview" : "Release")} â€¢ {candidate.PackageType}",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x92, 0xA4)),
                    FontSize = 10,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                textPanel.Children.Add(titleText);
                textPanel.Children.Add(subText);
                Grid.SetColumn(textPanel, 1);
                grid.Children.Add(textPanel);

                string badgeText;
                Color badgeBg;
                Color badgeFg;
                Color badgeBorder;

                if (isCurrent) {
                    badgeText = "CURRENT";
                    badgeBg = Color.FromRgb(0x1C, 0x2E, 0x22);
                    badgeFg = Color.FromRgb(0x38, 0xA1, 0x69);
                    badgeBorder = Color.FromRgb(0x38, 0xA1, 0x69);
                } else if (candidate.IsInstalled) {
                    badgeText = "READY";
                    badgeBg = Color.FromRgb(0x1E, 0x24, 0x33);
                    badgeFg = Color.FromRgb(0x7E, 0xA5, 0xF8);
                    badgeBorder = Color.FromRgb(0x3A, 0x4D, 0x7A);
                } else {
                    badgeText = "PACKAGE";
                    badgeBg = Color.FromRgb(0x2E, 0x22, 0x18);
                    badgeFg = Color.FromRgb(0xF6, 0xAD, 0x55);
                    badgeBorder = Color.FromRgb(0x7A, 0x50, 0x25);
                }

                var badge = new Border {
                    Background = new SolidColorBrush(badgeBg),
                    BorderBrush = new SolidColorBrush(badgeBorder),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0),
                    Child = new TextBlock {
                        Text = badgeText,
                        Foreground = new SolidColorBrush(badgeFg),
                        FontSize = 9,
                        FontWeight = FontWeights.Bold
                    }
                };
                Grid.SetColumn(badge, 2);
                grid.Children.Add(badge);

                itemBorder.Child = grid;

                itemBorder.MouseEnter += (s, ev) => {
                    if (!isCurrent) {
                        itemBorder.Background = new SolidColorBrush(Color.FromRgb(0x25, 0x28, 0x3A));
                        itemBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x38, 0xA1, 0x69));
                    }
                };
                itemBorder.MouseLeave += (s, ev) => {
                    if (!isCurrent) {
                        itemBorder.Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1E, 0x2A));
                        itemBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x2D, 0x30, 0x42));
                    }
                };

                var candCopy = candidate;
                itemBorder.PreviewMouseLeftButtonUp += (s, ev) => {
                    ev.Handled = true;
                    if (VersionSelectorPopup != null) VersionSelectorPopup.IsOpen = false;
                    HandleVersionSelected(candCopy);
                };

                VersionSelectorItemsPanel.Children.Add(itemBorder);
            }
        }

        private async void HandleVersionSelected(LocalVersionCandidate candidate) {
            try {
                var current = GetCurrentlyActiveVersion();
                bool isSame = (current != null && string.Equals(current.Name, candidate.Name, StringComparison.OrdinalIgnoreCase) && candidate.IsInstalled);

                var targetVer = candidate.VersionEntry ?? GetOrCreateVersionEntry(candidate);

                if (isSame) {
                    _selectedQuickPlayVersion = targetVer;
                    UpdateQuickPlayStatus();
                    ShowHeroProgress("LAUNCHING", "Launching Minecraft...");
                    InvokeLaunch(targetVer);
                    return;
                }

                int cmp = CompareVersionStrings(candidate.Name, current?.Name);
                string actionType = current == null ? "INSTALLING" : (cmp > 0 ? "UPDATING" : "DOWNGRADING");
                string msg;
                string title;
                if (current == null) {
                    msg = $"Installing Minecraft {candidate.Name}...";
                    title = "Install Version";
                } else if (cmp > 0) {
                    msg = $"Updating Minecraft from {current.Name} to {candidate.Name}...";
                    title = "Update Version";
                } else {
                    msg = $"Downgrading Minecraft from {current.Name} to {candidate.Name}...";
                    title = "Downgrade Version";
                }

                ThemedMessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Information);

                _selectedQuickPlayVersion = targetVer;
                if (QuickPlayVersionLabel != null) {
                    QuickPlayVersionLabel.Text = $"{candidate.Name} ({actionType})";
                }

                if (!candidate.IsInstalled) {
                    if (string.IsNullOrEmpty(candidate.PackageFilePath) || !File.Exists(candidate.PackageFilePath)) {
                        ThemedMessageBox.Show("The package file for this version could not be found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        UpdateQuickPlayStatus();
                        return;
                    }

                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string targetDir = Path.Combine(baseDir, (candidate.VersionType == VersionType.Preview ? "Minecraft-Preview-" : "Minecraft-") + candidate.Name);
                    targetVer.GameDirectory = targetDir;

                    _heroCancelSource = new CancellationTokenSource();
                    ShowHeroProgress(actionType, "Preparing package...");

                    bool extractSuccess = false;
                    try {
                        if (candidate.PackageType == PackageType.UWP) {
                            extractSuccess = await ExtractAppx(candidate.PackageFilePath, targetDir, targetVer, _heroCancelSource.Token);
                        } else if (candidate.PackageType == PackageType.GDK) {
                            if (!ShowGDKFirstUseWarning()) {
                                HideHeroProgress();
                                UpdateQuickPlayStatus();
                                return;
                            }
                            extractSuccess = await ExtractMsixvc(candidate.PackageFilePath, targetDir, targetVer, isPreview: candidate.VersionType == VersionType.Preview, cancellationToken: _heroCancelSource.Token);
                        }
                    } catch (OperationCanceledException) {
                        Trace.WriteLine("HandleVersionSelected: Operation cancelled by user.");
                        extractSuccess = false;
                    }

                    if (!extractSuccess) {
                        if (_heroCancelSource != null && _heroCancelSource.IsCancellationRequested) {
                            ThemedMessageBox.Show("Operation was cancelled.", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        HideHeroProgress();
                        UpdateQuickPlayStatus();
                        return;
                    }

                    targetVer.UpdateInstallStatus();
                    candidate.InstalledDirectory = targetDir;
                }

                ShowHeroProgress(actionType, "Launching Minecraft...");
                InvokeLaunch(targetVer);
                UpdateQuickPlayStatus();
            } catch (Exception ex) {
                HideHeroProgress();
                Trace.WriteLine("HandleVersionSelected error: " + ex);
                ThemedMessageBox.Show("An error occurred: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e) {
            if (VersionSelectorPopup != null && VersionSelectorPopup.IsOpen) return;
            if (SteveHeadRotationYaw == null || SteveHeadRotationPitch == null || HeadViewport == null) return;
            try {
                System.Windows.Point mousePos = e.GetPosition(HeadViewport);
                double centerX = HeadViewport.ActualWidth / 2.0;
                double centerY = HeadViewport.ActualHeight / 2.0;

                double deltaX = mousePos.X - centerX;
                double deltaY = mousePos.Y - centerY;

                // Yaw: look left/right (limit to +/- 48 degrees)
                double targetYaw = Math.Clamp(deltaX * 0.12, -48.0, 48.0);
                // Pitch: look up/down (deltaY is negative when mouse moves UP, tilting face UP)
                double targetPitch = Math.Clamp(deltaY * 0.12, -34.0, 34.0);

                SteveHeadRotationYaw.Angle = targetYaw;
                SteveHeadRotationPitch.Angle = targetPitch;
            } catch {
            }
        }

        private void InitSteveHead() {
            try {
                if (UserPrefs?.ActiveSkinType == "Custom" && !string.IsNullOrEmpty(UserPrefs?.CustomSkinPath) && File.Exists(UserPrefs.CustomSkinPath)) {
                    var bmp = SteveHeadHelper.LoadSkinFromFile(UserPrefs.CustomSkinPath);
                    string name = string.IsNullOrEmpty(UserPrefs.CustomSkinName) ? "Custom Skin" : UserPrefs.CustomSkinName;
                    ApplySkin(bmp, name, isBlock: false, isBugrock: false);
                    UpdateSkinSelectionUI(isCustom: true);
                    return;
                }
            } catch { }

            LoadDefaultBugrockSkin();
            UpdateSkinSelectionUI(isCustom: false);
        }

        private void UpdateSkinSelectionUI(bool isCustom) {
            try {
                if (PresetCustomRing != null) {
                    PresetCustomRing.BorderBrush = isCustom ? new SolidColorBrush(Color.FromRgb(0x38, 0xA1, 0x69)) : Brushes.Transparent;
                }
                if (PresetGlitchedRing != null) {
                    PresetGlitchedRing.BorderBrush = !isCustom ? new SolidColorBrush(Color.FromRgb(0xD0, 0x5C, 0xE3)) : Brushes.Transparent;
                }
            } catch { }
        }

        private void LoadDefaultBugrockSkin() {
            try {
                LoadSkinByPath("pack://application:,,,/Assets/bugrock_skin.png", "BUGrock (Glitched)", isBlock: false, isBugrock: true);
            } catch {
                try {
                    string localSkin = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "bugrock_skin.png");
                    if (System.IO.File.Exists(localSkin)) {
                        LoadSkinByPath(localSkin, "BUGrock (Glitched)", isBlock: false, isBugrock: true);
                    }
                } catch { }
            }
        }

        private void LoadSkinByPath(string uriOrPath, string displayName, bool isBlock = false, bool isBugrock = false) {
            try {
                BitmapImage bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(uriOrPath, UriKind.RelativeOrAbsolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();

                ApplySkin(bmp, displayName, isBlock, isBugrock);
            } catch { }
        }

        private void ApplySkin(BitmapSource skinImage, string displayName, bool isBlock = false, bool isBugrock = false) {
            try {
                if (HeadModelGroup == null || skinImage == null) return;
                HeadModelGroup.Children.Clear();
                var headModel = SteveHeadHelper.CreateHeadModel(skinImage, isBlock);
                HeadModelGroup.Children.Add(headModel);
                if (SkinNameLabel != null) {
                    SkinNameLabel.Text = displayName;
                    var magenta = TryFindResource("AccentMagenta") as SolidColorBrush ?? new SolidColorBrush(Color.FromRgb(0xD0, 0x5C, 0xE3));
                    var green = TryFindResource("AccentGreen") as SolidColorBrush ?? new SolidColorBrush(Color.FromRgb(0x38, 0xA1, 0x69));
                    SkinNameLabel.Foreground = (isBlock || isBugrock) ? magenta : green;
                }
            } catch { }
        }

        private void PresetCustom_Click(object sender, RoutedEventArgs e) {
            try {
                if (!string.IsNullOrEmpty(UserPrefs?.CustomSkinPath) && File.Exists(UserPrefs.CustomSkinPath)) {
                    UserPrefs.ActiveSkinType = "Custom";
                    RewritePrefs();
                    var bmp = SteveHeadHelper.LoadSkinFromFile(UserPrefs.CustomSkinPath);
                    string name = string.IsNullOrEmpty(UserPrefs.CustomSkinName) ? "Custom Skin" : UserPrefs.CustomSkinName;
                    ApplySkin(bmp, name, isBlock: false, isBugrock: false);
                    UpdateSkinSelectionUI(isCustom: true);
                } else {
                    UploadSkinButton_Click(sender, e);
                }
            } catch (Exception ex) {
                ThemedMessageBox.Show("Failed to switch skin: " + ex.Message, "Skin Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void PresetGlitched_Click(object sender, RoutedEventArgs e) {
            try {
                UserPrefs.ActiveSkinType = "BUGrock";
                RewritePrefs();
                LoadDefaultBugrockSkin();
                UpdateSkinSelectionUI(isCustom: false);
            } catch { }
        }

        private void UploadSkinButton_Click(object sender, RoutedEventArgs e) {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Minecraft Skin (*.png)|*.png|All Files (*.*)|*.*",
                Title = "Select Minecraft Skin"
            };
            if (ofd.ShowDialog() == true) {
                try {
                    var newSkin = SteveHeadHelper.LoadSkinFromFile(ofd.FileName);

                    string cacheDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skin_cache");
                    if (!Directory.Exists(cacheDir)) {
                        Directory.CreateDirectory(cacheDir);
                    }
                    string cachedFile = System.IO.Path.Combine(cacheDir, "cached_skin.png");
                    byte[] skinBytes = File.ReadAllBytes(ofd.FileName);
                    File.WriteAllBytes(cachedFile, skinBytes);

                    string skinName = System.IO.Path.GetFileNameWithoutExtension(ofd.FileName);
                    if (string.IsNullOrWhiteSpace(skinName)) skinName = "Custom Skin";

                    UserPrefs.ActiveSkinType = "Custom";
                    UserPrefs.CustomSkinPath = cachedFile;
                    UserPrefs.CustomSkinName = skinName;
                    RewritePrefs();

                    ApplySkin(newSkin, skinName, isBlock: false, isBugrock: false);
                    UpdateSkinSelectionUI(isCustom: true);
                } catch (Exception ex) {
                    ThemedMessageBox.Show("Failed to load skin image: " + ex.Message, "Skin Upload", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void btnMinimize_Click(object sender, RoutedEventArgs e) {
            WindowState = WindowState.Minimized;
        }

        private void btnMaximize_Click(object sender, RoutedEventArgs e) {
            WindowState = (WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized);
        }

        private void btnClose_Click(object sender, RoutedEventArgs e) {
            Close();
        }
    }
}
