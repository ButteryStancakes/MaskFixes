using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace MaskFixes
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    [BepInDependency(GUID_STARLANCER_AI_FIX, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(GUID_LOBBY_COMPATIBILITY, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(GUID_SMART_ENEMY_PATHFINDING, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        internal const string PLUGIN_GUID = "butterystancakes.lethalcompany.maskfixes", PLUGIN_NAME = "Mask Fixes", PLUGIN_VERSION = "1.5.2";
        internal static new ManualLogSource Logger;

        const string GUID_STARLANCER_AI_FIX = "AudioKnight.StarlancerAIFix";
        internal static bool DISABLE_ENEMY_MESH_PATCH;

        const string GUID_LOBBY_COMPATIBILITY = "BMX.LobbyCompatibility";

        internal const string HARMONY_MORE_SUITS = "x753.More_Suits", HARMONY_CLASSIC_SUIT_RESTORATION = "butterystancakes.lethalcompany.classicsuitrestoration";
        internal const string VANILLA_SUITS = "Orange suit,Green suit,Hazard suit,Pajama suit,Purple Suit,Bee Suit,Bunny Suit";

        const string GUID_SMART_ENEMY_PATHFINDING = "Zaggy1024.SmartEnemyPathfinding";
        internal static bool INSTALLED_SMART_ENEMY_PATHFINDING, FORCE_DISABLE_ROAMING_PATCH;

        internal static ConfigEntry<bool> configPatchHidingBehavior, configPatchRoamingBehavior, configRandomSuits, configFixAttackConversion;
        internal static ConfigEntry<float> configTragedyChance;
        internal static ConfigEntry<string> configSuitWhitelist;

        void Awake()
        {
            Logger = base.Logger;

            if (Chainloader.PluginInfos.ContainsKey(GUID_LOBBY_COMPATIBILITY))
            {
                Logger.LogInfo("CROSS-COMPATIBILITY - Lobby Compatibility detected");
                LobbyCompatibility.Init();
            }

            if (Chainloader.PluginInfos.ContainsKey(GUID_STARLANCER_AI_FIX))
            {
                DISABLE_ENEMY_MESH_PATCH = true;
                Logger.LogInfo("CROSS-COMPATIBILITY - EnableEnemyMesh patch will be disabled");
            }

            if (Chainloader.PluginInfos.ContainsKey(GUID_SMART_ENEMY_PATHFINDING))
            {
                INSTALLED_SMART_ENEMY_PATHFINDING = true;
                FORCE_DISABLE_ROAMING_PATCH = true;
                Logger.LogInfo("CROSS-COMPATIBILITY - SmartEnemyPathfinding detected");
            }

            configPatchHidingBehavior = Config.Bind(
                "Misc",
                "Patch Hiding Behavior",
                true,
                "(Host only) Changes the behavior Masked use to hide aboard the ship. This means multiple Masked will hide aboard different spots on the ship, and reduces the likelihood they will get stuck pathing into each other.");

            configPatchRoamingBehavior = Config.Bind(
                "Misc",
                "Patch Roaming Behavior",
                !FORCE_DISABLE_ROAMING_PATCH,
                "(Host only) Rewrites Masked roaming behavior to fix some bugs with the vanilla implementation. This will fix Masked entering/exiting the building constantly, or getting stuck on the mineshaft elevator.");

            configFixAttackConversion = Config.Bind(
                "Misc",
                "Fix Attack Conversion",
                true,
                "(Client-side, requires restart) When you are killed by a Masked, this will fix your mimic's appearance not synchronizing for anybody except the host. As a side effect this will also make you drop your items when converted.");

            configRandomSuits = Config.Bind(
                "Bonus",
                "Random Suits",
                false,
                "(Client-side) Naturally spawning Masked will wear a random vanilla suit.");

            configSuitWhitelist = Config.Bind(
                "Bonus",
                "Suit Whitelist",
                VANILLA_SUITS,
                "(Client-side) A comma-separated list of suits that natural Masked are permitted to wear. This is not case-sensitive, and will match strings from front-to-back. (Example: \"bROw\" will find \"Brown suit\" successfully)\nEntering only \"all\" instead will automatically add every suit to the list.");

            configTragedyChance = Config.Bind(
                "Bonus",
                "Tragedy Chance",
                0f,
                new ConfigDescription(
                    "(Client-side) The chance that a natural Masked will wear a Tragedy mask instead of a Comedy. (0.0 = all Comedy, 1.0 = all Tragedy, 0.5 = 50/50)",
                    new AcceptableValueRange<float>(0f, 1f)));

            if (FORCE_DISABLE_ROAMING_PATCH && configPatchRoamingBehavior.Value)
                Plugin.Logger.LogWarning("You have a mod installed that will conflict with the \"Patch Roaming Behavior\" setting. It will be ignored, but consider turning it off in your config to avoid future problems!");

            new Harmony(PLUGIN_GUID).PatchAll();

            Logger.LogInfo($"{PLUGIN_NAME} v{PLUGIN_VERSION} loaded");
        }
    }
}