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

        // Public Read-Only properties for access by other mods
        // to allow possible custom passport/wardrobe/whatever UIs with sorting and whatnot.
        // Could also use this for an outfit saver/randomizer.
        public static IReadOnlyList<HatEntry> Hats => _hats.AsReadOnly();
        public static IReadOnlyList<OutfitEntry> Outfits => _outfits.AsReadOnly();
        public static IReadOnlyList<SashEntry> Sashes => _sashes.AsReadOnly();
        public static IReadOnlyList<EyeEntry> Eyes => _eyes.AsReadOnly();
        public static IReadOnlyList<MouthEntry> Mouths => _mouths.AsReadOnly();
        public static IReadOnlyList<AccessoryEntry> Accessories => _accessories.AsReadOnly();

        /// <summary>
        /// Asynchronously loads an AssetBundle from a file path to prevent game freezes.
        /// This should be called from a StartCoroutine() method.
        /// </summary>
        /// <param name="fullPath">The full file path to the asset bundle.</param>
        /// <param name="onLoaded">An action or callback that will be invoked with the loaded bundle once complete.</param>
 
        
        public static IEnumerator LoadCosmeticAssetBundle(string fullPath, System.Action<AssetBundle?> onLoaded)
        {
            if (!File.Exists(fullPath))
            {
                PEAKCosmetics.Logger.LogError($"AssetBundle not found at path: {fullPath}");
                onLoaded?.Invoke(null);
                yield break;
            }

            var createRequest = AssetBundle.LoadFromFileAsync(fullPath);
            yield return createRequest;

            onLoaded?.Invoke(createRequest.assetBundle);
        }
        
        
        /// <summary>
        /// A streamlined helper method to load multiple hats from a bundle based on a naming convention.
        /// This assumes that for a cosmetic named "MyHat", the assets are named "MyHat.prefab" and "MyHat.png".
        /// </summary>
        /// <param name="bundle">The loaded AssetBundle containing the cosmetics.</param>
        /// <param name="hatNames">A list of the base names for the hats you want to load.</param>
        /// <param name="transforms">An optional dictionary mapping hat names to their custom transforms.</param>
        public static void AddHatsFromBundle(AssetBundle bundle, List<string> hatNames, Dictionary<string, HatTransform>? transforms = null)
        {
            if (bundle == null)
            {
                PEAKCosmetics.Logger.LogError("[API] AddHatsFromBundle was called with a null bundle.");
                return;
            }

            foreach (string hatName in hatNames)
            {
                // Construct asset paths based on the naming convention
                GameObject? prefab = bundle.LoadAsset<GameObject>($"Assets/{hatName}.prefab");
                Texture2D? icon = bundle.LoadAsset<Texture2D>($"Assets/{hatName}.png");

                if (prefab == null || icon == null)
                {
                    PEAKCosmetics.Logger.LogWarning($"[API] Could not find required assets for '{hatName}' in bundle '{bundle.name}'. Skipping.");
                    continue;
                }

                // --- Start of Correction ---

                // 1. Declare the variable and initialize it to null.
                HatTransform? transform = null;
                // 2. The TryGetValue method will now assign to the existing variable.
                transforms?.TryGetValue(hatName, out transform);

                // --- End of Correction ---

                // Use the existing AddHat method to register the cosmetic
                AddHat(hatName, prefab, icon, bundle.name, ACHIEVEMENTTYPE.NONE, transform);
            }
        }

        /// <summary>
        /// Adds a new Hat cosmetic entry to the library.
        /// </summary>
        /// <param name="name">The unique name of the cosmetic.</param>
        /// <param name="prefab">The hat's 3D model prefab.</param>
        /// <param name="icon">The 2D icon for the passport UI.</param>
        /// <param name="bundleName">The name of the asset bundle this cosmetic came from (optional).</param>
        /// <param name="requiredAchievement">The achievement required to unlock this cosmetic (optional).</param>
        /// <param name="transform">Custom position, rotation, and scale data for the hat (optional).</param>
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

        public static void AddMouth(string name, Texture2D? icon, string? bundleName = null, GameObject? prefab = null, ACHIEVEMENTTYPE requiredAchievement = ACHIEVEMENTTYPE.NONE)
        {
            if (string.IsNullOrEmpty(name)) return;
            _mouths.Add(new MouthEntry(name, prefab, icon, bundleName, requiredAchievement));
            PEAKCosmetics.Logger.LogInfo($"[API] Added Mouth: '{name}'");
        }

        public static void AddEye(string name, Texture2D? icon, string? bundleName = null, GameObject? prefab = null, ACHIEVEMENTTYPE requiredAchievement = ACHIEVEMENTTYPE.NONE)
        {
            if (string.IsNullOrEmpty(name)) return;
            _eyes.Add(new EyeEntry(name, prefab, icon, bundleName, requiredAchievement));
            PEAKCosmetics.Logger.LogInfo($"[API] Added Eye: '{name}'");
        }

        public static void AddAccessory(string name, Texture2D? icon, string? bundleName = null, GameObject? prefab = null, ACHIEVEMENTTYPE requiredAchievement = ACHIEVEMENTTYPE.NONE)
        {
            if (string.IsNullOrEmpty(name)) return;
            _accessories.Add(new AccessoryEntry(name, prefab, icon, bundleName, requiredAchievement));
            PEAKCosmetics.Logger.LogInfo($"[API] Added Accessory: '{name}'");
        }

        public static void AddSash(string name, GameObject? prefab, Texture2D? icon, string? bundleName = null, ACHIEVEMENTTYPE requiredAchievement = ACHIEVEMENTTYPE.NONE)
        {
            if (string.IsNullOrEmpty(name)) return;
            _sashes.Add(new SashEntry(name, prefab, icon, bundleName, requiredAchievement));
            PEAKCosmetics.Logger.LogInfo($"[API] Added Sash: '{name}'");
        }

        /// <summary>
        /// A data structure for holding custom transform information for a hat.
        /// </summary>
        public class HatTransform
        {
            public Vector3 Position { get; }
            public Vector3 Scale { get; }
            public Vector3 Rotation { get; }
            public HatTransform(Vector3 position, Vector3 scale, Vector3 rotation) { Position = position; Scale = scale; Rotation = rotation; }
        }

        public class HatEntry
        {
            public string Name;
            public GameObject? Prefab;
            public Texture2D? Icon;
            public string? BundleName;
            public ACHIEVEMENTTYPE RequiredAchievement;
            public HatTransform? Transform;
            public HatEntry(string name, GameObject? prefab, Texture2D? icon, string? bundleName, ACHIEVEMENTTYPE requiredAchievement, HatTransform? transform) { Name = name; Prefab = prefab; Icon = icon; BundleName = bundleName; RequiredAchievement = requiredAchievement; Transform = transform; }
        }

        public class OutfitEntry { public string Name; public GameObject? Prefab; public Texture2D? Icon; public string? BundleName; public ACHIEVEMENTTYPE RequiredAchievement; public OutfitEntry(string n, GameObject? p, Texture2D? i, string? b, ACHIEVEMENTTYPE a) { Name = n; Prefab = p; Icon = i; BundleName = b; RequiredAchievement = a; } }
        public class SashEntry { public string Name; public GameObject? Prefab; public Texture2D? Icon; public string? BundleName; public ACHIEVEMENTTYPE RequiredAchievement; public SashEntry(string n, GameObject? p, Texture2D? i, string? b, ACHIEVEMENTTYPE a) { Name = n; Prefab = p; Icon = i; BundleName = b; RequiredAchievement = a; } }
        public class EyeEntry { public string Name; public GameObject? Prefab; public Texture2D? Icon; public string? BundleName; public ACHIEVEMENTTYPE RequiredAchievement; public EyeEntry(string n, GameObject? p, Texture2D? i, string? b, ACHIEVEMENTTYPE a) { Name = n; Prefab = p; Icon = i; BundleName = b; RequiredAchievement = a; } }
        public class MouthEntry { public string Name; public GameObject? Prefab; public Texture2D? Icon; public string? BundleName; public ACHIEVEMENTTYPE RequiredAchievement; public MouthEntry(string n, GameObject? p, Texture2D? i, string? b, ACHIEVEMENTTYPE a) { Name = n; Prefab = p; Icon = i; BundleName = b; RequiredAchievement = a; } }
        public class AccessoryEntry { public string Name; public GameObject? Prefab; public Texture2D? Icon; public string? BundleName; public ACHIEVEMENTTYPE RequiredAchievement; public AccessoryEntry(string n, GameObject? p, Texture2D? i, string? b, ACHIEVEMENTTYPE a) { Name = n; Prefab = p; Icon = i; BundleName = b; RequiredAchievement = a; } }
    }
}