using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace MaskFixes
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    [BepInDependency(GUID_STARLANCER_AI_FIX, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string PLUGIN_GUID = "butterystancakes.lethalcompany.maskfixes", PLUGIN_NAME = "Mask Fixes", PLUGIN_VERSION = "1.1.0";
        internal static new ManualLogSource Logger;

        const string GUID_STARLANCER_AI_FIX = "AudioKnight.StarlancerAIFix";
        internal static bool DISABLE_ENEMY_MESH_PATCH;

        internal static ConfigEntry<bool> configPatchHidingBehavior, configPatchRoamingBehavior/*, configPrioritizeHiding*/;

        void Awake()
        {
            Logger = base.Logger;

            if (Chainloader.PluginInfos.ContainsKey(GUID_STARLANCER_AI_FIX))
            {
                DISABLE_ENEMY_MESH_PATCH = true;
                Logger.LogInfo("CROSS-COMPATIBILITY - EnableEnemyMesh patch will be disabled");
            }

            configPatchHidingBehavior = Config.Bind(
                "Misc",
                "Patch Hiding Behavior",
                true,
                "(Host only) Changes the behavior Masked use to hide aboard the ship. This means multiple Masked will hide aboard different spots on the ship, and reduces the likelihood they will get stuck pathing into each other.");

            configPatchRoamingBehavior = Config.Bind(
                "Misc",
                "Patch Roaming Behavior",
                true,
                "(Host only) Rewrites Masked roaming behavior to fix some bugs with the vanilla implementation. This will fix Masked entering/exiting the building constantly, or getting stuck on the mineshaft elevator.");

            /*configPrioritizeHiding = Config.Bind(
                "Misc",
                "Prioritize Hiding",
                false,
                "(Host only) When there are no players on the surface of the moon, and a Masked has been roaming on the surface for a long time, they will head towards the ship to try and hide on it instead of returning inside the building.\nThis deviates significantly from vanilla, but helps highlight their rare hiding behavior.\nRequires \"Patch Roaming Behavior\" to be enabled.");*/

            new Harmony(PLUGIN_GUID).PatchAll();

            Logger.LogInfo($"{PLUGIN_NAME} v{PLUGIN_VERSION} loaded");
        }
    }
}