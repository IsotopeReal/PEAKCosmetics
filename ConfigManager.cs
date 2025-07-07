using BepInEx;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace PEAKCosmeticsLib
{
    public class ConfigManager
    {
        private readonly Dictionary<string, (string assetBundleName, List<string> hatNames, List<string> outfitNames,
                                    List<string> mouthNames, List<string> eyeNames, List<string> sashNames,
                                    List<string> accessoryNames, Dictionary<string, CosmeticAPI.HatTransform> transformations,
                                    Dictionary<string, ACHIEVEMENTTYPE> achievementRequirements)> assetBundlesWithCosmetics =
            new Dictionary<string, (string, List<string>, List<string>, List<string>, List<string>, List<string>, List<string>, Dictionary<string, CosmeticAPI.HatTransform>, Dictionary<string, ACHIEVEMENTTYPE>)>();

        private FileSystemWatcher? configWatcher;

        public ConfigManager()
        {
            SetupConfigWatcher();
        }

        public IEnumerator LoadCosmeticsFromConfiguration()
        {
            LoadAllConfigFiles();
            yield return LoadAllCosmeticsFromDisk();
        }

        #region Configuration Management
        private void SetupConfigWatcher()
        {
            string configSubfolderPath = Path.Combine(Paths.ConfigPath, "PEAKCosmeticsLib");
            if (!Directory.Exists(configSubfolderPath))
            {
                Directory.CreateDirectory(configSubfolderPath);
            }

            configWatcher = new FileSystemWatcher(configSubfolderPath)
            {
                Filter = "*.cfg",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
            };
            configWatcher.Changed += OnConfigChanged;
            configWatcher.Created += OnConfigChanged;
            configWatcher.EnableRaisingEvents = true;
        }

        private void OnConfigChanged(object sender, FileSystemEventArgs e)
        {
            Task.Delay(500).ContinueWith(t =>
            {
                PEAKCosmetics.Logger.LogInfo($"Config file changed: {e.FullPath}. Please restart the game for changes to take effect.");
            });
        }

        private void LoadAllConfigFiles()
        {
            string configSubfolderPath = Path.Combine(Paths.ConfigPath, "PEAKCosmeticsLib");
            string[] configFiles = Directory.GetFiles(configSubfolderPath, "*.cfg", SearchOption.TopDirectoryOnly);

            if (configFiles.Length == 0)
            {
                PEAKCosmetics.Logger.LogInfo("No config files found. Creating a default config.");
                string defaultConfigFile = Path.Combine(configSubfolderPath, "PEAKCosmeticsConfig_Default.cfg");
                using (StreamWriter writer = new StreamWriter(defaultConfigFile))
                {
                    writer.WriteLine("[PEAKCosmeticsLib_Bundle1]");
                    writer.WriteLine("AssetBundle = morecustomhats");
                    writer.WriteLine("HatNames = chibidoki");
                    writer.WriteLine("OutfitNames = chibidoki");
                    writer.WriteLine("Transformations = chibidoki:position(0,0.5,8),scale(3,3,3),rotation(0,0,0)");
                }
                configFiles = new[] { defaultConfigFile };
            }

            assetBundlesWithCosmetics.Clear();

            foreach (string filePath in configFiles)
            {
                PEAKCosmetics.Logger.LogInfo($"Reading cosmetics from config file: {filePath}");
                ReadSingleConfigFile(filePath);
            }
        }

        private void ReadSingleConfigFile(string configPath)
        {
            string[] lines = File.ReadAllLines(configPath);
            string currentSection = string.Empty;

            foreach (var line in lines)
            {
                string trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#")) continue;

                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    currentSection = trimmedLine.Trim('[', ']');
                    continue;
                }

                if (trimmedLine.Contains("=") && !string.IsNullOrEmpty(currentSection))
                {
                    var parts = trimmedLine.Split(new[] { '=' }, 2);
                    string key = parts[0].Trim().ToLowerInvariant();
                    string value = parts.Length > 1 ? parts[1].Trim() : string.Empty;

                    if (!assetBundlesWithCosmetics.TryGetValue(currentSection, out var bundleInfo))
                    {
                        bundleInfo = (string.Empty, new List<string>(), new List<string>(), new List<string>(), new List<string>(), new List<string>(), new List<string>(), new Dictionary<string, CosmeticAPI.HatTransform>(), new Dictionary<string, ACHIEVEMENTTYPE>());
                    }

                    switch (key)
                    {
                        case "assetbundle": bundleInfo.assetBundleName = value; break;
                        case "hatnames": bundleInfo.hatNames.AddRange(value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)); break;
                        case "outfitnames": bundleInfo.outfitNames.AddRange(value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)); break;
                        case "mouthnames": bundleInfo.mouthNames.AddRange(value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)); break;
                        case "eyenames": bundleInfo.eyeNames.AddRange(value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)); break;
                        case "sashnames": bundleInfo.sashNames.AddRange(value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)); break;
                        case "accessorynames": bundleInfo.accessoryNames.AddRange(value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)); break;
                        case "transformations": ParseTransformations(value, bundleInfo.transformations); break;
                        case "achievementrequirements": ParseAchievementRequirements(value, bundleInfo.achievementRequirements); break;
                    }
                    assetBundlesWithCosmetics[currentSection] = bundleInfo;
                }
            }
        }

        private void ParseTransformations(string transformationsValue, Dictionary<string, CosmeticAPI.HatTransform> targetDictionary)
        {
            if (string.IsNullOrWhiteSpace(transformationsValue)) return;
            string[] transformations = transformationsValue.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var transformEntry in transformations)
            {
                var transformParts = transformEntry.Split(':');
                if (transformParts.Length != 2) continue;
                string cosmeticName = transformParts[0].Trim();
                string data = transformParts[1].Trim();
                Vector3 position = Vector3.zero; Vector3 scale = Vector3.one; Vector3 rotation = Vector3.zero;
                Match posMatch = Regex.Match(data, @"position\(([^)]+)\)");
                if (posMatch.Success) { string[] values = posMatch.Groups[1].Value.Split(','); if (values.Length == 3) { float.TryParse(values[0], out position.x); float.TryParse(values[1], out position.y); float.TryParse(values[2], out position.z); } }
                Match scaleMatch = Regex.Match(data, @"scale\(([^)]+)\)");
                if (scaleMatch.Success) { string[] values = scaleMatch.Groups[1].Value.Split(','); if (values.Length == 3) { float.TryParse(values[0], out scale.x); float.TryParse(values[1], out scale.y); float.TryParse(values[2], out scale.z); } }
                Match rotMatch = Regex.Match(data, @"rotation\(([^)]+)\)");
                if (rotMatch.Success) { string[] values = rotMatch.Groups[1].Value.Split(','); if (values.Length == 3) { float.TryParse(values[0], out rotation.x); float.TryParse(values[1], out rotation.y); float.TryParse(values[2], out rotation.z); } }
                targetDictionary[cosmeticName] = new CosmeticAPI.HatTransform(position, scale, rotation);
            }
        }

        private void ParseAchievementRequirements(string achievementReqValue, Dictionary<string, ACHIEVEMENTTYPE> targetDictionary)
        {
            if (string.IsNullOrWhiteSpace(achievementReqValue)) return;
            string[] requirements = achievementReqValue.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var reqEntry in requirements)
            {
                var reqParts = reqEntry.Split(':');
                if (reqParts.Length != 2) continue;
                string cosmeticName = reqParts[0].Trim();
                string achievementStr = reqParts[1].Trim();
                if (Enum.TryParse<ACHIEVEMENTTYPE>(achievementStr, true, out var achievementType)) { targetDictionary[cosmeticName] = achievementType; }
            }
        }
        #endregion

        #region Asset Loading
        private IEnumerator LoadAllCosmeticsFromDisk()
        {
            PEAKCosmetics.Logger.LogInfo("Starting to load asset bundles from config files.");
            string assetBundlesFolderPath = Path.Combine(Paths.PluginPath, "AssetBundles");
            if (!Directory.Exists(assetBundlesFolderPath)) Directory.CreateDirectory(assetBundlesFolderPath);

            foreach (var entry in assetBundlesWithCosmetics.Values)
            {
                if (string.IsNullOrEmpty(entry.assetBundleName))
                {
                    continue;
                }

                AssetBundle? loadedBundle = null;
                string assetBundlePath = Path.Combine(assetBundlesFolderPath, entry.assetBundleName);

                // We call the async method and wait for it to complete.
                // This coroutine yields control until the nested coroutine is finished.
                yield return PEAKCosmetics.Instance.StartCoroutine(CosmeticAPI.LoadCosmeticAssetBundleAsync(
                    assetBundlePath,
                    (bundle) => loadedBundle = bundle // This callback sets the loadedBundle variable
                ));

                if (loadedBundle == null)
                {
                    PEAKCosmetics.Logger.LogError($"Failed to load AssetBundle: {entry.assetBundleName}, skipping this config entry.");
                    continue;
                }

                PEAKCosmetics.Logger.LogInfo($"AssetBundle loaded: {entry.assetBundleName}");

                // Now that the bundle is loaded, we can start the async operations to load assets from it.
                // Each of these will be waited on in sequence.
                yield return PEAKCosmetics.Instance.StartCoroutine(CosmeticAPI.AddHatsFromBundleAsync(loadedBundle, entry.hatNames, entry.transformations));
                yield return PEAKCosmetics.Instance.StartCoroutine(CosmeticAPI.AddOutfitsFromBundleAsync(loadedBundle, entry.outfitNames));
                yield return PEAKCosmetics.Instance.StartCoroutine(CosmeticAPI.AddEyesFromBundleAsync(loadedBundle, entry.eyeNames));
                yield return PEAKCosmetics.Instance.StartCoroutine(CosmeticAPI.AddMouthsFromBundleAsync(loadedBundle, entry.mouthNames));
                yield return PEAKCosmetics.Instance.StartCoroutine(CosmeticAPI.AddAccessoriesFromBundleAsync(loadedBundle, entry.accessoryNames));

                // Unload the bundle memory once we are done with it.
                loadedBundle.Unload(false);
            }
            PEAKCosmetics.Logger.LogInfo("Finished processing all cosmetic configs.");
        }
        #endregion
    }
}