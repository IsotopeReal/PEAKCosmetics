using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zorro.Core;

namespace PEAKCosmeticsLib
{
    [BepInPlugin("PEAKCosmeticsLib", "PEAK Cosmetics Library", "1.0.0")]
    public class PEAKCosmetics : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger = null!;
        public static PEAKCosmetics Instance { get; private set; } = null!;
        private static Harmony? PatcherInstance;

        public void Awake()
        {
            Logger = base.Logger;
            Instance = this;

            try
            {
                PatcherInstance = new Harmony("PEAKCosmetics");
                PatcherInstance.PatchAll(typeof(Patcher));
                Logger.LogInfo("Harmony patches applied successfully.");
            }
            catch (Exception e)
            {
                Logger.LogError("PEAKCosmeticsLib failed to apply Harmony patches. This is likely due to a game update. The mod will be disabled to prevent errors.");
                Logger.LogError($"Error details: {e}");
                return;
            }

            ConfigManager configManager = new ConfigManager();
            StartCoroutine(configManager.LoadCosmeticsFromConfiguration());
        }

        private class Patcher
        {
            /// <summary>
            /// A reusable helper that removes our cosmetics from a list of options before re-adding them.
            /// This ensures our cosmetics are always added last, in a consistent order and doesn't scramble the indexing of cosmetics.
            /// This should allow cooperative mod compatibility with other cosmetic mods.
            /// </summary>
            private static void SynchronizeCosmeticOptions(Customization customization)
            {
                // Get the names of all hats managed by our API
                var apiHatNames = new HashSet<string>(CosmeticAPI.Hats.Select(h => h.Name));
                // Create a new list containing only hats that are NOT from our API
                var syncedHatOptions = (customization.hats ?? Array.Empty<CustomizationOption>())
                                       .Where(h => h != null && !apiHatNames.Contains(h.name)).ToList();
                // Re-add our hats to the end of the list
                foreach (var hat in CosmeticAPI.Hats) CreateCosmeticOption(syncedHatOptions, hat.Name, hat.Icon, Customization.Type.Hat, hat.Prefab, hat.RequiredAchievement);
                customization.hats = syncedHatOptions.ToArray();

                // Repeat for other types...
                var apiOutfitNames = new HashSet<string>(CosmeticAPI.Outfits.Select(o => o.Name));
                var syncedOutfitOptions = (customization.fits ?? Array.Empty<CustomizationOption>())
                                          .Where(o => o != null && !apiOutfitNames.Contains(o.name)).ToList();
                foreach (var outfit in CosmeticAPI.Outfits) CreateCosmeticOption(syncedOutfitOptions, outfit.Name, outfit.Icon, Customization.Type.Fit, outfit.Prefab, outfit.RequiredAchievement);
                customization.fits = syncedOutfitOptions.ToArray();

                var apiMouthNames = new HashSet<string>(CosmeticAPI.Mouths.Select(m => m.Name));
                var syncedMouthOptions = (customization.mouths ?? Array.Empty<CustomizationOption>())
                                         .Where(m => m != null && !apiMouthNames.Contains(m.name)).ToList();
                foreach (var mouth in CosmeticAPI.Mouths) CreateCosmeticOption(syncedMouthOptions, mouth.Name, mouth.Icon, Customization.Type.Mouth, null, mouth.RequiredAchievement);
                customization.mouths = syncedMouthOptions.ToArray();

                var apiEyeNames = new HashSet<string>(CosmeticAPI.Eyes.Select(e => e.Name));
                var syncedEyeOptions = (customization.eyes ?? Array.Empty<CustomizationOption>())
                                       .Where(e => e != null && !apiEyeNames.Contains(e.name)).ToList();
                foreach (var eye in CosmeticAPI.Eyes) CreateCosmeticOption(syncedEyeOptions, eye.Name, eye.Icon, Customization.Type.Eyes, null, eye.RequiredAchievement);
                customization.eyes = syncedEyeOptions.ToArray();

                var apiAccessoryNames = new HashSet<string>(CosmeticAPI.Accessories.Select(a => a.Name));
                var syncedAccessoryOptions = (customization.accessories ?? Array.Empty<CustomizationOption>())
                                             .Where(a => a != null && !apiAccessoryNames.Contains(a.name)).ToList();
                foreach (var accessory in CosmeticAPI.Accessories) CreateCosmeticOption(syncedAccessoryOptions, accessory.Name, accessory.Icon, Customization.Type.Accessory, null, accessory.RequiredAchievement);
                customization.accessories = syncedAccessoryOptions.ToArray();
            }

            /// <summary>
            /// A helper for cooperative cosmetic patching.
            /// </summary>
            private static void SynchronizeHatModels(CharacterCustomization __instance)
            {
                if (__instance.refs.playerHats == null) return;

                Transform? hatsContainer = __instance.refs.playerHats.FirstOrDefault(h => h != null)?.transform.parent;
                if (hatsContainer == null) { hatsContainer = __instance.transform.Find("Scout/Armature/Hip/Mid/AimJoint/Torso/Head/Hat"); }
                if (hatsContainer == null) { Logger.LogError("Could not find the hats container transform!"); return; }

                // Get a list of all hat names managed by our API
                var apiHatObjectNames = new HashSet<string>(CosmeticAPI.Hats.Select(h => $"CustomHat_{h.Name}"));
                // Create a new list containing only hats that are NOT from our API
                var syncedHats = new List<Renderer>(__instance.refs.playerHats.Where(h => h != null && !apiHatObjectNames.Contains(h.name)));

                // Re-add our hats to the end of the list
                foreach (var hat in CosmeticAPI.Hats)
                {
                    if (hat.Prefab == null) continue;
                    GameObject hatInstance = Instantiate(hat.Prefab, hatsContainer);
                    hatInstance.name = $"CustomHat_{hat.Name}";

                    if (hat.Transform is { } transform)
                    {
                        hatInstance.transform.localPosition = transform.Position;
                        hatInstance.transform.localScale = transform.Scale;
                        hatInstance.transform.localRotation = Quaternion.Euler(transform.Rotation);
                    }

                    if (hatInstance.GetComponentInChildren<Renderer>() is { } renderer)
                    {
                        renderer.material.shader = Shader.Find("W/Character");
                        syncedHats.Add(renderer);
                    }
                }

                __instance.refs.playerHats = syncedHats.ToArray();
            }

            [HarmonyPatch(typeof(PassportManager), "OpenTab")]
            [HarmonyPostfix]
            public static void PassportManagerOpenTabPostfix(PassportManager __instance)
            {
                try
                {
                    Customization customization = __instance.GetComponent<Customization>();
                    if (customization == null) return;

                    Logger.LogInfo("Passport tab opened. Synchronizing UI lists...");
                    SynchronizeCosmeticOptions(customization);
                }
                catch (Exception e)
                {
                    Logger.LogError($"An error occurred in the PassportManager.OpenTab patch: {e}");
                }
            }

            [HarmonyPatch(typeof(CharacterCustomization), "Start")]
            [HarmonyPostfix]
            public static void CharacterCustomizationStartPostfix(CharacterCustomization __instance)
            {
                try
                {
                    // Synchronize the 3D models first to establish the definitive order.
                    Logger.LogInfo("CharacterCustomization Start(). Synchronizing 3D hat models...");
                    SynchronizeHatModels(__instance);

                    // Then, force the character to refresh its appearance based on saved data.
                    FieldInfo? characterField = typeof(CharacterCustomization).GetField("_character", BindingFlags.NonPublic | BindingFlags.Instance);
                    Character? characterObject = characterField?.GetValue(__instance) as Character;
                    if (characterObject == null || !characterObject.IsLocal) return;

                    PersistentPlayerDataService? service = GameHandler.GetService<PersistentPlayerDataService>();
                    if (service == null || characterObject.photonView?.Owner == null) return;

                    PersistentPlayerData? playerData = service.GetPlayerData(characterObject.photonView.Owner);
                    if (playerData == null) return;

                    MethodInfo? onPlayerDataChangeMethod = typeof(CharacterCustomization).GetMethod("OnPlayerDataChange", BindingFlags.NonPublic | BindingFlags.Instance);
                    onPlayerDataChangeMethod?.Invoke(__instance, new object[] { playerData });
                }
                catch (Exception e)
                {
                    Logger.LogError($"An error occurred in the CharacterCustomization.Start patch: {e}");
                }
            }

            private static void CreateCosmeticOption(List<CustomizationOption> optionsList, string name, Texture2D? icon, Customization.Type type, GameObject? prefab, ACHIEVEMENTTYPE requiredAchievement)
            {
                if (icon == null) { Logger.LogWarning($"Skipping UI option for '{name}' (type {type}) due to null icon."); return; }
                if (optionsList.Any(option => option != null && option.name == name)) { return; }

                CustomizationOption cosmeticOption = ScriptableObject.CreateInstance<CustomizationOption>();
                cosmeticOption.name = name;
                cosmeticOption.texture = icon;
                cosmeticOption.type = type;
                cosmeticOption.requiredAchievement = requiredAchievement;
                cosmeticOption.color = Color.white;

                if (type == Customization.Type.Fit && prefab != null)
                {
                    if (prefab.GetComponentInChildren<SkinnedMeshRenderer>() is { } smr)
                    {
                        cosmeticOption.fitMesh = smr.sharedMesh;
                        if (smr.sharedMaterials.Length > 0) cosmeticOption.fitMaterial = smr.sharedMaterials[0];
                        if (smr.sharedMaterials.Length > 1) cosmeticOption.fitMaterialShoes = smr.sharedMaterials[1];
                    }
                }

                optionsList.Add(cosmeticOption);
            }
        }
    }

    internal static class ArrayExtensions
    {
        public static T[] AddToArray<T>(this T[] array, T item)
        {
            T[] newArray = new T[array.Length + 1];
            array.CopyTo(newArray, 0);
            newArray[array.Length] = item;
            return newArray;
        }
    }
}
