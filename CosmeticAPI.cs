using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PEAKCosmeticsLib
{
    public static class CosmeticAPI
    {
        // Private lists for internal storage
        private static readonly List<HatEntry> _hats = new List<HatEntry>();
        private static readonly List<OutfitEntry> _outfits = new List<OutfitEntry>();
        private static readonly List<SashEntry> _sashes = new List<SashEntry>();
        private static readonly List<EyeEntry> _eyes = new List<EyeEntry>();
        private static readonly List<MouthEntry> _mouths = new List<MouthEntry>();
        private static readonly List<AccessoryEntry> _accessories = new List<AccessoryEntry>();

        // Public Read-Only properties for safe access by other mods
        public static IReadOnlyList<HatEntry> Hats => _hats.AsReadOnly();
        public static IReadOnlyList<OutfitEntry> Outfits => _outfits.AsReadOnly();
        public static IReadOnlyList<SashEntry> Sashes => _sashes.AsReadOnly();
        public static IReadOnlyList<EyeEntry> Eyes => _eyes.AsReadOnly();
        public static IReadOnlyList<MouthEntry> Mouths => _mouths.AsReadOnly();
        public static IReadOnlyList<AccessoryEntry> Accessories => _accessories.AsReadOnly();

        #region Base API Methods

        public static void AddHat(string name, GameObject? prefab, Texture2D? icon, string? bundleName = null, ACHIEVEMENTTYPE requiredAchievement = ACHIEVEMENTTYPE.NONE, HatTransform? transform = null)
        {
            if (string.IsNullOrEmpty(name)) { PEAKCosmetics.Logger.LogError("Attempted to add a hat with a null or empty name."); return; }
            _hats.Add(new HatEntry(name, prefab, icon, bundleName, requiredAchievement, transform));
            PEAKCosmetics.Logger.LogInfo($"[API] Added Hat: '{name}'");
        }

        public static void AddOutfit(string name, GameObject? prefab, Texture2D? icon, string? bundleName = null, ACHIEVEMENTTYPE requiredAchievement = ACHIEVEMENTTYPE.NONE)
        {
            if (string.IsNullOrEmpty(name)) { PEAKCosmetics.Logger.LogError("Attempted to add an outfit with a null or empty name."); return; }
            _outfits.Add(new OutfitEntry(name, prefab, icon, bundleName, requiredAchievement));
            PEAKCosmetics.Logger.LogInfo($"[API] Added Outfit: '{name}'");
        }

        public static void AddEye(string name, Texture2D? icon, string? bundleName = null, ACHIEVEMENTTYPE requiredAchievement = ACHIEVEMENTTYPE.NONE)
        {
            if (string.IsNullOrEmpty(name)) { PEAKCosmetics.Logger.LogError("Attempted to add an eye with a null or empty name."); return; }
            _eyes.Add(new EyeEntry(name, icon, bundleName, requiredAchievement));
            PEAKCosmetics.Logger.LogInfo($"[API] Added Eye: '{name}'");
        }

        public static void AddMouth(string name, Texture2D? icon, string? bundleName = null, ACHIEVEMENTTYPE requiredAchievement = ACHIEVEMENTTYPE.NONE)
        {
            if (string.IsNullOrEmpty(name)) { PEAKCosmetics.Logger.LogError("Attempted to add a mouth with a null or empty name."); return; }
            _mouths.Add(new MouthEntry(name, icon, bundleName, requiredAchievement));
            PEAKCosmetics.Logger.LogInfo($"[API] Added Mouth: '{name}'");
        }

        public static void AddAccessory(string name, Texture2D? icon, string? bundleName = null, ACHIEVEMENTTYPE requiredAchievement = ACHIEVEMENTTYPE.NONE)
        {
            if (string.IsNullOrEmpty(name)) { PEAKCosmetics.Logger.LogError("Attempted to add an accessory with a null or empty name."); return; }
            _accessories.Add(new AccessoryEntry(name, icon, bundleName, requiredAchievement));
            PEAKCosmetics.Logger.LogInfo($"[API] Added Accessory: '{name}'");
        }

        public static void AddSash(string name, GameObject? prefab, Texture2D? icon, string? bundleName = null, ACHIEVEMENTTYPE requiredAchievement = ACHIEVEMENTTYPE.NONE) { if (string.IsNullOrEmpty(name)) return; _sashes.Add(new SashEntry(name, prefab, icon, bundleName, requiredAchievement)); }

        #endregion

        #region Async Helper Methods

        public static IEnumerator LoadCosmeticAssetBundleAsync(string fullPath, Action<AssetBundle?> onLoaded)
        {
            if (!File.Exists(fullPath))
            {
                PEAKCosmetics.Logger.LogError($"[API] AssetBundle not found at path: {fullPath}");
                onLoaded?.Invoke(null);
                yield break;
            }

            var createRequest = AssetBundle.LoadFromFileAsync(fullPath);
            yield return createRequest;

            onLoaded?.Invoke(createRequest.assetBundle);
        }

        private static IEnumerator AddCosmeticsFromBundleAsync<T>(AssetBundle bundle, List<string> names, Action<string, T?, Texture2D?> addAction) where T : class
        {
            foreach (string name in names)
            {
                AssetBundleRequest? prefabRequest = null;
                AssetBundleRequest? iconRequest = null;

                try
                {
                    if (typeof(T) != typeof(Texture2D))
                    {
                        prefabRequest = bundle.LoadAssetAsync<T>($"Assets/{name}.prefab");
                    }
                    iconRequest = bundle.LoadAssetAsync<Texture2D>($"Assets/{name}.png");
                }
                catch (Exception e)
                {
                    PEAKCosmetics.Logger.LogError($"[API] An error occurred while queuing asset loading for '{name}'. Skipping. Error: {e}");
                    continue;
                }

                if (prefabRequest != null) yield return prefabRequest;
                if (iconRequest != null) yield return iconRequest;

                T? loadedPrefab = prefabRequest?.asset as T;
                Texture2D? loadedIcon = iconRequest?.asset as Texture2D;

                if (typeof(T) == typeof(Texture2D))
                {
                    loadedPrefab = loadedIcon as T;
                }

                if (loadedPrefab != null && loadedIcon != null)
                {
                    addAction(name, loadedPrefab, loadedIcon);
                }
                else
                {
                    PEAKCosmetics.Logger.LogWarning($"[API] Could not find required assets for '{name}' in bundle '{bundle.name}'. Skipping.");
                }
            }
        }

        public static IEnumerator AddHatsFromBundleAsync(AssetBundle bundle, List<string> hatNames, Dictionary<string, HatTransform>? transforms = null)
        {
            yield return AddCosmeticsFromBundleAsync<GameObject>(bundle, hatNames, (name, prefab, icon) =>
            {
                HatTransform? transform = null;
                transforms?.TryGetValue(name, out transform);
                AddHat(name, prefab, icon, bundle.name, ACHIEVEMENTTYPE.NONE, transform);
            });
        }

        public static IEnumerator AddOutfitsFromBundleAsync(AssetBundle bundle, List<string> outfitNames)
        {
            yield return AddCosmeticsFromBundleAsync<GameObject>(bundle, outfitNames, (name, prefab, icon) =>
                AddOutfit(name, prefab, icon, bundle.name));
        }

        public static IEnumerator AddEyesFromBundleAsync(AssetBundle bundle, List<string> eyeNames)
        {
            yield return AddCosmeticsFromBundleAsync<Texture2D>(bundle, eyeNames, (name, _, icon) =>
                AddEye(name, icon, bundle.name));
        }

        public static IEnumerator AddMouthsFromBundleAsync(AssetBundle bundle, List<string> mouthNames)
        {
            yield return AddCosmeticsFromBundleAsync<Texture2D>(bundle, mouthNames, (name, _, icon) =>
                AddMouth(name, icon, bundle.name));
        }

        public static IEnumerator AddAccessoriesFromBundleAsync(AssetBundle bundle, List<string> accessoryNames)
        {
            yield return AddCosmeticsFromBundleAsync<Texture2D>(bundle, accessoryNames, (name, _, icon) =>
                AddAccessory(name, icon, bundle.name));
        }

        #endregion

        #region Data Structures

        public class HatTransform { public Vector3 Position { get; } public Vector3 Scale { get; } public Vector3 Rotation { get; } public HatTransform(Vector3 p, Vector3 s, Vector3 r) { Position = p; Scale = s; Rotation = r; } }

        public abstract class CosmeticEntry { public string Name; public Texture2D? Icon; public string? BundleName; public ACHIEVEMENTTYPE RequiredAchievement; protected CosmeticEntry(string n, Texture2D? i, string? b, ACHIEVEMENTTYPE a) { Name = n; Icon = i; BundleName = b; RequiredAchievement = a; } }

        public class HatEntry : CosmeticEntry { public GameObject? Prefab; public HatTransform? Transform; public HatEntry(string n, GameObject? p, Texture2D? i, string? b, ACHIEVEMENTTYPE a, HatTransform? t) : base(n, i, b, a) { Prefab = p; Transform = t; } }
        public class OutfitEntry : CosmeticEntry { public GameObject? Prefab; public OutfitEntry(string n, GameObject? p, Texture2D? i, string? b, ACHIEVEMENTTYPE a) : base(n, i, b, a) { Prefab = p; } }
        public class SashEntry : CosmeticEntry { public GameObject? Prefab; public SashEntry(string n, GameObject? p, Texture2D? i, string? b, ACHIEVEMENTTYPE a) : base(n, i, b, a) { Prefab = p; } }

        public class EyeEntry : CosmeticEntry { public EyeEntry(string n, Texture2D? i, string? b, ACHIEVEMENTTYPE a) : base(n, i, b, a) { } }
        public class MouthEntry : CosmeticEntry { public MouthEntry(string n, Texture2D? i, string? b, ACHIEVEMENTTYPE a) : base(n, i, b, a) { } }
        public class AccessoryEntry : CosmeticEntry { public AccessoryEntry(string n, Texture2D? i, string? b, ACHIEVEMENTTYPE a) : base(n, i, b, a) { } }

        #endregion
    }
}
