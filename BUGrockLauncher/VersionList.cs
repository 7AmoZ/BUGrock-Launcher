using BUGrockLauncher.WPFDataTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace BUGrockLauncher {
    public class VersionList : ObservableCollection<WPFDataTypes.Version> {

        public string VersionsApiUWP { get; set; }
        public string VersionsApiGDK { get; set; }

        private static readonly string GDK_CONFIG_FILENAME = "MicrosoftGame.Config";

        private readonly string _cacheFileUWP;
        private readonly string _cacheFileGDK;
        private readonly string _importedDirectory;
        private readonly WPFDataTypes.ICommonVersionCommands _commands;
        private readonly HttpClient _client = new HttpClient();
        HashSet<string> dbVersions = new HashSet<string>();

        private PropertyChangedEventHandler _versionPropertyChangedHandler;
        public VersionList(string cacheFileUWP, string importedDirectory, string versionsApiUWP, WPFDataTypes.ICommonVersionCommands commands, PropertyChangedEventHandler versionPropertyChangedEventHandler, string cacheFileGDK, string versionsApiGDK) {
            _cacheFileUWP = cacheFileUWP;
            _cacheFileGDK = cacheFileGDK;
            _importedDirectory = importedDirectory;
            VersionsApiUWP = versionsApiUWP;
            VersionsApiGDK = versionsApiGDK;
            _commands = commands;
            _versionPropertyChangedHandler = versionPropertyChangedEventHandler;
            CollectionChanged += versionListOnCollectionChanged;
        }

        private void versionListOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
            if (e.OldItems != null) {
                foreach (var item in e.OldItems) {
                    if (item is WPFDataTypes.Version version) {
                        version.PropertyChanged -= _versionPropertyChangedHandler;
                    }
                }
            }
            if (e.NewItems != null) {
                foreach (var item in e.NewItems) {
                    if (item is WPFDataTypes.Version version) {
                        version.PropertyChanged += _versionPropertyChangedHandler;
                    }
                }
            }
        }

        private void ParseListUWP(JArray? data, bool isCache) {
            if (data == null) return;
            // ([name, uuid, versionType])[]
            foreach (JArray o in data.AsEnumerable().Reverse()) {
                if (o.Count < 3) continue;
                string? name = o[0]?.Value<string>();
                string? uuid = o[1]?.Value<string>();
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(uuid)) continue;
                bool exists = !dbVersions.Add(name);
                bool isNew = !exists && !isCache;
                int versionType = o[2].Value<int>();
                if (!Enum.IsDefined(typeof(WPFDataTypes.VersionType), versionType) || versionType == (int) WPFDataTypes.VersionType.Imported)
                    continue;
                Add(new WPFDataTypes.Version(uuid, name, (WPFDataTypes.VersionType)versionType, isNew, _commands, PackageType.UWP, null));
            }
        }

        private void ParseListGDK(JObject? data, bool isCache, WPFDataTypes.VersionType versionType) {
            if (data == null) return;
            foreach (var keyValue in data.Properties().Reverse()) {
                string versionName = keyValue.Name;
                if (keyValue.Value is not JArray urls) continue;

                List<string> downloadUrls = new List<string>();
                foreach (var url in urls) {
                    string? urlStr = url?.Value<string>();
                    if (!string.IsNullOrWhiteSpace(urlStr)) {
                        downloadUrls.Add(urlStr);
                    }
                }
                if (downloadUrls.Count == 0) {
                    Trace.WriteLine("Not showing version " + versionName + " because it has no download URLs");
                    continue;
                }
                bool exists = !dbVersions.Add(versionName);
                bool isNew = !exists && !isCache;

                Add(new WPFDataTypes.Version(WPFDataTypes.Version.UNKNOWN_UUID, versionName, versionType, isNew, _commands, PackageType.GDK, downloadUrls));
            }
        }

        private void ParseDataGDK(JObject? data, bool isCache) {
            if (data == null) return;
            ParseListGDK(data["release"] as JObject, isCache, WPFDataTypes.VersionType.Release);
            ParseListGDK(data["preview"] as JObject, isCache, WPFDataTypes.VersionType.Preview);
        }

        public void PrepareForReload() {
            for (int i = Count - 1; i >= 0; i--) {
                if (this[i].VersionType != WPFDataTypes.VersionType.Imported) {
                    RemoveAt(i);
                }
            }
        }

        public async Task LoadImported() {
            string[] subdirectoryEntries = await Task.Run(() => Directory.Exists(_importedDirectory) ? Directory.GetDirectories(_importedDirectory) : Array.Empty<string>());
            foreach (string subdirectory in subdirectoryEntries) {
                if (dbVersions.Add("IMPORTED_" + Path.GetFileName(subdirectory))) {
                    AddEntry(Path.GetFileName(subdirectory), subdirectory, File.Exists(Path.Combine(subdirectory, GDK_CONFIG_FILENAME)) ? PackageType.GDK : PackageType.UWP);
                }
            }
        }

        public async Task LoadFromCacheUWP() {
            try {
                using (var reader = File.OpenText(_cacheFileUWP)) {
                    var data = await reader.ReadToEndAsync();
                    ParseListUWP(JArray.Parse(data), true);
                }
            } catch (FileNotFoundException) { // ignore
            } catch (JsonException ex) {
                Trace.WriteLine("Corrupted UWP cache file ignored: " + ex.Message);
            }
        }

        public async Task LoadFromCacheGDK() {
            try {
                using (var reader = File.OpenText(_cacheFileGDK)) {
                    var data = await reader.ReadToEndAsync();
                    ParseDataGDK(JObject.Parse(data), true);
                }
            } catch (FileNotFoundException) { // ignore
            } catch (JsonException ex) {
                Trace.WriteLine("Corrupted GDK cache file ignored: " + ex.Message);
            }
        }

        public async Task DownloadVersionsGDK() {
            var resp = await _client.GetAsync(VersionsApiGDK);
            resp.EnsureSuccessStatusCode();
            var data = await resp.Content.ReadAsStringAsync();
            File.WriteAllText(_cacheFileGDK, data);
            ParseDataGDK(JObject.Parse(data), false);
        }

        public async Task DownloadVersionsUWP() { 
            var resp = await _client.GetAsync(VersionsApiUWP);
            resp.EnsureSuccessStatusCode();
            var data = await resp.Content.ReadAsStringAsync();
            File.WriteAllText(_cacheFileUWP, data);
            ParseListUWP(JArray.Parse(data), false);
        }

        public WPFDataTypes.Version AddEntry(string name, string path, PackageType packageType) {
            string cleanName = name;
            if (cleanName.EndsWith(".appx", StringComparison.OrdinalIgnoreCase)) {
                cleanName = cleanName.Substring(0, cleanName.Length - 5);
            } else if (cleanName.EndsWith(".msixvc", StringComparison.OrdinalIgnoreCase)) {
                cleanName = cleanName.Substring(0, cleanName.Length - 7);
            }
            var result = new WPFDataTypes.Version(cleanName, path, _commands, packageType);
            Add(result);
            return result;
        }

    }
}
