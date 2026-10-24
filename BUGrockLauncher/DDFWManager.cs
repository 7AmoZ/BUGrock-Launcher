using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace BUGrockLauncher
{
    public static class DDFWManager
    {
        public static string RootFolder => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DDFW");
        public static string AddonsFolder => Path.Combine(RootFolder, "Addons");
        public static string TexturePacksFolder => Path.Combine(RootFolder, "TexturePacks");
        public static string MapsFolder => Path.Combine(RootFolder, "Maps");
        public static string SkinPacksFolder => Path.Combine(RootFolder, "SkinPacks");
        public static string ShadersFolder => Path.Combine(RootFolder, "Shaders");
        public static string OtherFolder => Path.Combine(RootFolder, "Other");

        public static void EnsureDirectories()
        {
            try
            {
                Directory.CreateDirectory(AddonsFolder);
                Directory.CreateDirectory(TexturePacksFolder);
                Directory.CreateDirectory(MapsFolder);
                Directory.CreateDirectory(SkinPacksFolder);
                Directory.CreateDirectory(ShadersFolder);
                Directory.CreateDirectory(OtherFolder);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create DDFW directories: {ex.Message}");
            }
        }

        public static string DetermineTargetFolder(string sourceUrl, string fileName, string? tempFilePath = null)
        {
            EnsureDirectories();
            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            string lowerUrl = (sourceUrl ?? string.Empty).ToLowerInvariant();
            string lowerName = fileName.ToLowerInvariant();

            // 1. Extension Direct Matches
            if (ext == ".mcworld") return MapsFolder;
            if (ext == ".mcaddon") return AddonsFolder;
            if (ext == ".png" && (lowerUrl.Contains("skin") || lowerName.Contains("skin"))) return SkinPacksFolder;

            // 2. Deep Inspection of .mcpack and .zip using inner manifest.json
            if ((ext == ".mcpack" || ext == ".zip") && !string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
            {
                try
                {
                    using var archive = ZipFile.OpenRead(tempFilePath);
                    var manifestEntry = archive.Entries.FirstOrDefault(e => e.Name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase));
                    if (manifestEntry != null)
                    {
                        using var reader = new StreamReader(manifestEntry.Open());
                        string json = reader.ReadToEnd();
                        
                        // Check if skin pack
                        if (json.Contains("\"type\": \"skin_pack\"") || json.Contains("\"type\":\"skin_pack\""))
                        {
                            return SkinPacksFolder;
                        }

                        // Check if resources (textures or shaders)
                        if (json.Contains("\"type\": \"resources\"") || json.Contains("\"type\":\"resources\""))
                        {
                            if (lowerName.Contains("shader") || lowerUrl.Contains("shader") || json.IndexOf("shader", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                return ShadersFolder;
                            }
                            return TexturePacksFolder;
                        }

                        // Check if data / behavior pack / addon
                        if (json.Contains("\"type\": \"data\"") || json.Contains("\"type\":\"data\"") ||
                            json.Contains("\"type\": \"client_data\"") || json.Contains("\"type\":\"client_data\"") ||
                            json.Contains("\"type\": \"javascript\"") || json.Contains("\"type\":\"javascript\""))
                        {
                            return AddonsFolder;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error reading manifest.json inside archive: {ex.Message}");
                }
            }

            // 3. Contextual Fallback matching URL paths and names
            if (lowerUrl.Contains("/texture-packs") || lowerUrl.Contains("/textures") || lowerUrl.Contains("texturepack")) return TexturePacksFolder;
            if (lowerUrl.Contains("/shaders") || lowerUrl.Contains("shader")) return ShadersFolder;
            if (lowerUrl.Contains("/skins") || lowerUrl.Contains("/skin-packs") || lowerUrl.Contains("skinpack")) return SkinPacksFolder;
            if (lowerUrl.Contains("/maps") || lowerUrl.Contains("/worlds") || lowerUrl.Contains("world")) return MapsFolder;
            if (lowerUrl.Contains("/addons") || lowerUrl.Contains("/mods") || lowerUrl.Contains("addon") || lowerUrl.Contains("mod")) return AddonsFolder;

            // Default for .mcpack when manifest was missing or inaccessible
            if (ext == ".mcpack") return AddonsFolder;

            return OtherFolder;
        }

        public static string GetCategoryDisplayName(string folderPath)
        {
            string name = Path.GetFileName(folderPath);
            return name switch
            {
                "Addons" => "Add-ons & Mods",
                "TexturePacks" => "Texture & Resource Packs",
                "Maps" => "Maps & Worlds",
                "SkinPacks" => "Skins & Skin Packs",
                "Shaders" => "Shaders",
                _ => "Other Downloads"
            };
        }
    }
}
