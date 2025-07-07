![](https://i.imgur.com/5DjwpVC.png)

-----
lorem ipsum dolor sit amet

You can find my Cosmetic Pack that utilizes this API below\
https://github.com/IsotopeReal/IsotopesCosmetics

You can also use the assetbundle from the above pack to add the cosmetics via a config instead of dll.

## Documentation

The PEAK Cosmetics Library allows average users and developers to easily add custom cosmetics into the game.\
There are two ways to add cosmetics:

1.  **For The Average User (Config Files):** The easiest method. Simply place your cosmetic asset bundle in the correct folder and create a config file with details about your items. No coding or compiling is required.
2.  **For Mod Developers (C\# API):** For more advanced control or for integration into other mods, you can use the `CosmeticAPI` to seamlessly load and add cosmetics directly from your own plugin's code.

-----

### The easy way to add your own cosmetics. (Using Config Files)

This method is perfect for literally anyone who wants to add their cosmetics to the game without writing any code.

#### 1\. File Placement

First, you need to place your files in the correct BepInEx folders:

  * **AssetBundles:** Place your Unity asset bundle files (e.g., `mycoolhats`) inside the `BepInEx/plugins/AssetBundles/` folder. If the `AssetBundles` folder doesn't exist, you can create it.
  * **Config File:** Create your configuration file inside the `BepInEx/config/PEAKCosmeticsLib/` folder. The library will automatically create this folder and a default example config file upon game start, you can just duplicate this .cfg and just rename it YourModName.

#### 2\. Config File

Your `.cfg` file uses a simple key-value format organized into sections. Each section represents one asset bundle.

  * **Section Name:** A unique name for your bundle, enclosed in `[]`. Example: `[MyCoolHats]`
  * **Keys:** Define the cosmetics within that bundle using the keys below.
  * `AssetBundle`: **(Required)** The name of your asset bundle file (e.g., `AssetBundle = mycoolhats`).
  * `HatNames`: A comma-separated list of hat cosmetic names from this bundle (e.g., `HatNames = top_hat,baseball_cap,beanie`).
  * `OutfitNames`: A comma-separated list of outfit cosmetic names.
  * `MouthNames`: A comma-separated list of mouth texture names.
  * `EyeNames`: A comma-separated list of eye texture names.
  * `AccessoryNames`: A comma-separated list of accessory texture names.
  * `Transformations`: **(Optional)** Used to adjust the position, scale, and rotation of your **hats**. The format is `HatName:property(x,y,z),property(x,y,z);`
  * `AchievementRequirements`: **(Optional)** Lock a cosmetic behind a game achievement. The format is `cosmeticName:ACHIEVEMENTNAME;`

#### 4\. Example Config

Here is a complete example of a `MyCoolHats.cfg` file. This would load two hats from the `cool_hats.bundle` file.

```ini
[MyCoolHatsCollection]
# The name of the asset bundle file located in BepInEx/plugins/AssetBundles/
AssetBundle = cool_hats.bundle

# A list of all hat prefabs to load from the bundle
HatNames = fancy_fedora,viking_helmet

# Optional: Add custom transformations for any hats that need it.
# The viking_helmet will be moved up slightly and scaled up.
Transformations = viking_helmet:position(0,0.1,0),scale(1.2,1.2,1.2)

# Optional: Lock the fancy_fedora behind an achievement.
# The player will not be able to equip it until this achievement is unlocked.
AchievementRequirements = fancy_fedora:SOME_ACHIEVEMENT_NAME
```

-----

### For Mod Developers (Using the C\# API)

This documentation is for plugin developers who want to add cosmetics by referencing the `PEAKCosmeticsLib.dll` in their own C\# project.

#### Getting Started

1.  In your Visual Studio project, add a reference to the `PEAKCosmeticsLib.dll`.
2.  In your main plugin file, add a dependency attribute above your class definition to ensure the library loads before your plugin:
    `[BepInDependency("PEAKCosmeticsLib")]`

There are two ways to add cosmetics using the API: the easy way (recommended) and the manual way (for special cases).

### Method 1: The Easy Way (Using the Naming Convention)

This is the recommended method for most use cases as it requires the least amount of code.\
It works by assuming your assets follow a simple naming rule.
You can add multiple cosmetics that follow this rule with a single API call.

For a cosmetic named **"MyHat"**, your assets inside the Unity project must be named **"MyHat.prefab"** and **"MyHat.png"** before you build the asset bundle.


**Example 1:**\
This code loads an asset bundle and uses the helper method to register two hats, one of which has a custom transform.
<details><summary>Show Code</summary>

```csharp
using BepInEx;
using UnityEngine;
using PEAKCosmeticsLib; 
using System.IO;
using System.Reflection;
using System.Collections.Generic;

[BepInPlugin("com.myname.easyhatpack", "My Easy Hat Pack", "1.0.0")]
[BepInDependency("PEAKCosmeticsLib")]
public class EasyHatPack : BaseUnityPlugin
{
    void Awake()
    {
        // 1. Find the path to your asset bundle.
        string assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string bundlePath = Path.Combine(assemblyFolder, "easyhats.bundle");

        // 2. Start the async loading process and provide the OnBundleLoaded method as the callback.
        StartCoroutine(CosmeticAPI.LoadCosmeticAssetBundleAsync(bundlePath, OnBundleLoaded));
    }

    void OnBundleLoaded(AssetBundle? myBundle)
    {
        if (myBundle == null) 
        {
            Logger.LogError("Failed to load easyhats.bundle!");
            return;
        }

        // 3. List the base names of the hats you want to add.
        var hatNames = new List<string> { "CowboyHat", "PropellerBeanie" };

        // 4. (Optional) Define transforms for any hats that need adjustment.
        var hatTransforms = new Dictionary<string, CosmeticAPI.HatTransform>
        {
            { "PropellerBeanie", new CosmeticAPI.HatTransform(new Vector3(0, 0.05f, 0), Vector3.one, Vector3.zero) }
        };

        // 5. Start another coroutine to register all hats with one simple call.
        StartCoroutine(CosmeticAPI.AddHatsFromBundleAsync(myBundle, hatNames, hatTransforms));
    }
}
```
</details>
-----

### Method 2: The Manual Way (For Special Cases)

Use this method if your asset names do not follow the naming convention, or if you need more granular control over a single cosmetic.\
This method is also fully asynchronous like the above method.


**Example 2:**\
This code loads one hat whose asset files have different names.
<details><summary>Show Code</summary>

```csharp
using BepInEx;
using UnityEngine;
using PEAKCosmeticsLib; 
using System.IO;
using System.Reflection;
using System.Collections;

[BepInPlugin("com.myname.manualhatpack", "My Manual Hat Pack", "1.0.0")]
[BepInDependency("PEAKCosmeticsLib")]
public class ManualHatPack : BaseUnityPlugin
{
    void Awake()
    {
        // 1. Find the path to your asset bundle.
        string assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string bundlePath = Path.Combine(assemblyFolder, "specialassets.bundle");

        // 2. Start the async loading process.
        StartCoroutine(CosmeticAPI.LoadCosmeticAssetBundleAsync(bundlePath, OnBundleLoaded));
    }

    void OnBundleLoaded(AssetBundle? specialBundle)
    {
        if (specialBundle == null)
        {
            Logger.LogError("Failed to load specialassets.bundle!");
            return;
        }

        // 3. Start a new coroutine using the manual async helper for this specific hat.
        StartCoroutine(CosmeticAPI.AddHatManuallyAsync(
            bundle: specialBundle,
            cosmeticName: "Viking Helmet",
            prefabPath: "Assets/Models/hat_viking_final.prefab",
            iconPath: "Assets/UI/Icons/viking_icon_preview.png"
        ));
    }
}
```
</details>