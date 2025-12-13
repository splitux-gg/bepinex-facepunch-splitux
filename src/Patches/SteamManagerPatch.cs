using HarmonyLib;
using System;
using System.Reflection;

namespace SplituxFacepunch.Patches
{
    /// <summary>
    /// Patches game's SteamManager to force Steam state after DoSteam() runs.
    /// ATO's SteamManager caches steamLoaded, steamId, steamName - we need to override AFTER caching.
    /// Also prevents SteamNotConnected from quitting the game.
    /// </summary>
    public static class SteamManagerPatch
    {
        private static Type _steamManagerType;
        private static FieldInfo _steamLoadedField;
        private static FieldInfo _steamIdField;
        private static FieldInfo _steamNameField;

        /// <summary>
        /// Apply SteamManager patches for games that cache Steam state.
        /// </summary>
        public static void Apply(Harmony harmony, SplituxConfig config)
        {
            try
            {
                // Find SteamManager type
                _steamManagerType = FindType("SteamManager");
                if (_steamManagerType == null)
                {
                    Plugin.Log.LogDebug("[SteamManagerPatch] SteamManager type not found - skipping");
                    return;
                }

                Plugin.Log.LogInfo("[SteamManagerPatch] Found SteamManager - applying patches");

                // Cache field references
                _steamLoadedField = _steamManagerType.GetField("steamLoaded",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                _steamIdField = _steamManagerType.GetField("steamId",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                _steamNameField = _steamManagerType.GetField("steamName",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                var patchType = typeof(SteamManagerPatch);
                var flags = BindingFlags.Static | BindingFlags.Public;

                // Patch DoSteam with postfix to override cached values
                var doSteamMethod = _steamManagerType.GetMethod("DoSteam",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (doSteamMethod != null)
                {
                    harmony.Patch(doSteamMethod,
                        postfix: new HarmonyMethod(patchType.GetMethod(nameof(DoSteamPostfix), flags)));
                    Plugin.Log.LogInfo("[SteamManagerPatch] Patched DoSteam - KEY FIX for cached values");
                }

                // Patch SteamNotConnected to prevent quit
                var steamNotConnectedMethod = _steamManagerType.GetMethod("SteamNotConnected",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (steamNotConnectedMethod != null)
                {
                    harmony.Patch(steamNotConnectedMethod,
                        prefix: new HarmonyMethod(patchType.GetMethod(nameof(SteamNotConnectedPrefix), flags)));
                    Plugin.Log.LogInfo("[SteamManagerPatch] Patched SteamNotConnected - safety net");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[SteamManagerPatch] Failed: {ex}");
            }
        }

        /// <summary>
        /// Force steamLoaded=true, steamId=spoofed, steamName=spoofed AFTER DoSteam runs.
        /// This overrides whatever the real Steam API returned.
        /// </summary>
        public static void DoSteamPostfix(object __instance)
        {
            if (Plugin.SplituxCfg == null) return;

            try
            {
                Plugin.Log.LogInfo("[SteamManagerPatch] DoSteam POSTFIX - Forcing Steam state");

                // Force steamLoaded = true
                if (_steamLoadedField != null)
                {
                    var was = _steamLoadedField.GetValue(__instance);
                    _steamLoadedField.SetValue(__instance, true);
                    Plugin.Log.LogInfo($"[SteamManagerPatch] steamLoaded: {was} -> TRUE");
                }

                // Force steamId to spoofed value
                if (_steamIdField != null)
                {
                    var was = _steamIdField.GetValue(__instance);
                    _steamIdField.SetValue(__instance, Plugin.SplituxCfg.SteamId);
                    Plugin.Log.LogInfo($"[SteamManagerPatch] steamId: {was} -> {Plugin.SplituxCfg.SteamId}");
                }

                // Force steamName to spoofed value
                if (_steamNameField != null)
                {
                    var was = _steamNameField.GetValue(__instance);
                    _steamNameField.SetValue(__instance, Plugin.SplituxCfg.AccountName);
                    Plugin.Log.LogInfo($"[SteamManagerPatch] steamName: {was} -> {Plugin.SplituxCfg.AccountName}");
                }

                Plugin.Log.LogInfo("[SteamManagerPatch] DoSteam POSTFIX COMPLETE");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[SteamManagerPatch] DoSteamPostfix failed: {ex}");
            }
        }

        /// <summary>
        /// Prevent SteamNotConnected from calling Application.Quit().
        /// </summary>
        public static bool SteamNotConnectedPrefix()
        {
            Plugin.Log.LogWarning("[SteamManagerPatch] SteamNotConnected called - BLOCKED quit");
            return false; // Skip original method
        }

        private static Type FindType(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(typeName, false, true);
                    if (type != null) return type;

                    foreach (var t in assembly.GetTypes())
                    {
                        if (t.Name == typeName || t.FullName == typeName)
                            return t;
                    }
                }
                catch { }
            }
            return null;
        }
    }
}
