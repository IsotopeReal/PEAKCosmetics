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
            [HarmonyPatch(typeof(PassportManager), "Awake")]
            [HarmonyPostfix]
            public static void PassportManagerAwakePostfix(PassportManager __instance)
            {
                try
                {
                    Customization customization = __instance.GetComponent<Customization>();
                    if (customization == null) { Logger.LogError("Customization component not found on PassportManager!"); return; }

                    Logger.LogInfo("Adding all custom cosmetics to passport...");
                    foreach (var hat in CosmeticAPI.Hats) CreateCosmeticOption(customization, hat.Name, hat.Icon, Customization.Type.Hat, hat.Prefab, hat.RequiredAchievement);
                    foreach (var outfit in CosmeticAPI.Outfits) CreateCosmeticOption(customization, outfit.Name, outfit.Icon, Customization.Type.Fit, outfit.Prefab, outfit.RequiredAchievement);
                    foreach (var mouth in CosmeticAPI.Mouths) CreateCosmeticOption(customization, mouth.Name, mouth.Icon, Customization.Type.Mouth, null, mouth.RequiredAchievement);
                    foreach (var eye in CosmeticAPI.Eyes) CreateCosmeticOption(customization, eye.Name, eye.Icon, Customization.Type.Eyes, null, eye.RequiredAchievement);
                    foreach (var accessory in CosmeticAPI.Accessories) CreateCosmeticOption(customization, accessory.Name, accessory.Icon, Customization.Type.Accessory, null, accessory.RequiredAchievement);
                    Logger.LogInfo("Finished adding cosmetics to passport.");
                }
                catch (Exception e)
                {
                    Logger.LogError($"An error occurred in the PassportManager.Awake patch: {e}");
                }
            }

            /// <summary>
            /// A  cooperative method to add hats to a CharacterCustomization instance.
            /// </summary>
            private static void AddHatsCooperative(CharacterCustomization __instance)
            {
                if (__instance.refs.playerHats == null) return;

                Transform? hatsContainer = __instance.refs.playerHats.FirstOrDefault()?.transform.parent;
                if (hatsContainer == null) { Logger.LogError("Could not find the hats container transform!"); return; }

                var allHats = new List<Renderer>(__instance.refs.playerHats);
                int initialCount = allHats.Count;

                foreach (var hat in CosmeticAPI.Hats)
                {
                    if (allHats.Any(h => h.name == $"CustomHat_{hat.Name}")) continue;

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
                        allHats.Add(renderer);
                    }
                    else
                    {
                        Logger.LogError($"Prefab for hat '{hat.Name}' is missing a Renderer component.");
                    }
                }

                if (allHats.Count > initialCount)
                {
                    __instance.refs.playerHats = allHats.ToArray();
                    Logger.LogInfo($"Added {allHats.Count - initialCount} new hats. Total hats now: {__instance.refs.playerHats.Length}");
                }
            }

            [HarmonyPatch(typeof(CharacterCustomization), "Awake")]
            [HarmonyPostfix]
            public static void CharacterCustomizationAwakePostfix(CharacterCustomization __instance)
            {
                try
                {
                    Logger.LogInfo("Running cooperative hat injection in Awake...");
                    AddHatsCooperative(__instance);
                }
                catch (Exception e)
                {
                    Logger.LogError($"An error occurred in the CharacterCustomization.Awake patch: {e}");
                }
            }

            [HarmonyPatch(typeof(CharacterCustomization), "Start")]
            [HarmonyPostfix]
            public static void CharacterCustomizationStartPostfix(CharacterCustomization __instance)
            {
                try
                {
                    // --- Possible mod time race compatibility fix ---
                    // By running the cooperative add again in Start(), we ensure our hats are present
                    // even if another mod's Awake() patch overwrote them.
                    Logger.LogInfo("Running cooperative cosmetics verification in Start...");
                    AddHatsCooperative(__instance);

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

            private static void CreateCosmeticOption(Customization customization, string name, Texture2D? icon, Customization.Type type, GameObject? prefab, ACHIEVEMENTTYPE requiredAchievement)
            {
                if (icon == null) { Logger.LogWarning($"Skipping UI option for '{name}' (type {type}) due to null icon."); return; }
                if (customization.GetList(type).Any(option => option != null && option.name == name)) { return; }

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

                var list = customization.GetList(type).ToList();
                list.Add(cosmeticOption);

                switch (type)
                {
                    case Customization.Type.Hat: customization.hats = list.ToArray(); break;
                    case Customization.Type.Fit: customization.fits = list.ToArray(); break;
                    case Customization.Type.Mouth: customization.mouths = list.ToArray(); break;
                    case Customization.Type.Eyes: customization.eyes = list.ToArray(); break;
                    case Customization.Type.Accessory: customization.accessories = list.ToArray(); break;
                }
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
