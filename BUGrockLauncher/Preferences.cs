using Newtonsoft.Json;

namespace BUGrockLauncher {
    public class Preferences {
        public bool ShowInstalledOnly { get; set; } = false;

        public bool DeleteAppxAfterDownload { get; set; } = true;

        public bool AutoFetchDecryptionKeys { get; set; } = true;

        [JsonProperty("VersionsApi")]
        public string VersionsApiUWP { get; set; } = "";

        public string VersionsApiGDK { get; set; } = "";

        public bool HasPreviouslyUsedGDK { get; set; } = false;

        public bool ShowLegacyBetaTab { get; set; } = false;

        public string ActiveSkinType { get; set; } = "BUGrock";

        public string CustomSkinPath { get; set; } = "";

        public string CustomSkinName { get; set; } = "";

        public Dictionary<string, string> CustomInstallDirectories { get; set; } = new Dictionary<string, string>();
    }
}
