using HarmonyLib;
using Steamworks;
using System;
using System.Reflection;

namespace SplituxFacepunch.Patches
{
    /// <summary>
    /// Patches Facepunch.Steamworks SteamClient to return spoofed Steam ID and name.
    /// This allows multiple instances to appear as different Steam users.
    /// </summary>
    public static class SteamClientPatch
    {
        private static bool _identityInitialized = false;
        private static SteamId _spoofedSteamId;
        private static string _spoofedName;

        /// <summary>
        /// Apply identity spoofing patches: SteamClient.SteamId and SteamClient.Name
        /// </summary>
        public static void ApplyIdentityPatches(Harmony harmony, SplituxConfig config)
        {
            try
            {
                _spoofedSteamId = new SteamId { Value = config.SteamId };
                _spoofedName = config.AccountName;
                _identityInitialized = true;

                Plugin.Log.LogInfo($"[SteamClientPatch] Identity: SteamId={_spoofedSteamId.Value}, Name={_spoofedName}");

                var patchType = typeof(SteamClientPatch);
                var flags = BindingFlags.Static | BindingFlags.Public;

                // Patch SteamClient.SteamId getter
                var steamIdGetter = typeof(SteamClient).GetProperty(nameof(SteamClient.SteamId))?.GetGetMethod();
                if (steamIdGetter != null)
                {
                    harmony.Patch(steamIdGetter,
                        postfix: new HarmonyMethod(patchType.GetMethod(nameof(SteamIdPostfix), flags)));
                    Plugin.Log.LogInfo("[SteamClientPatch] Patched SteamClient.SteamId");
                }
                else
                {
                    Plugin.Log.LogWarning("[SteamClientPatch] SteamClient.SteamId not found");
                }

                // Patch SteamClient.Name getter
                var nameGetter = typeof(SteamClient).GetProperty(nameof(SteamClient.Name))?.GetGetMethod();
                if (nameGetter != null)
                {
                    harmony.Patch(nameGetter,
                        postfix: new HarmonyMethod(patchType.GetMethod(nameof(NamePostfix), flags)));
                    Plugin.Log.LogInfo("[SteamClientPatch] Patched SteamClient.Name");
                }
                else
                {
                    Plugin.Log.LogWarning("[SteamClientPatch] SteamClient.Name not found");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[SteamClientPatch] Failed to apply identity patches: {ex}");
            }
        }

        /// <summary>
        /// Apply validation patches: SteamClient.IsValid and SteamClient.IsLoggedOn
        /// </summary>
        public static void ApplyValidPatches(Harmony harmony)
        {
            try
            {
                var patchType = typeof(SteamClientPatch);
                var flags = BindingFlags.Static | BindingFlags.Public;

                // Patch SteamClient.IsValid getter
                var isValidGetter = typeof(SteamClient).GetProperty(nameof(SteamClient.IsValid))?.GetGetMethod();
                if (isValidGetter != null)
                {
                    harmony.Patch(isValidGetter,
                        postfix: new HarmonyMethod(patchType.GetMethod(nameof(IsValidPostfix), flags)));
                    Plugin.Log.LogInfo("[SteamClientPatch] Patched SteamClient.IsValid");
                }
                else
                {
                    Plugin.Log.LogWarning("[SteamClientPatch] SteamClient.IsValid not found");
                }

                // Patch SteamClient.IsLoggedOn getter
                var isLoggedOnGetter = typeof(SteamClient).GetProperty(nameof(SteamClient.IsLoggedOn))?.GetGetMethod();
                if (isLoggedOnGetter != null)
                {
                    harmony.Patch(isLoggedOnGetter,
                        postfix: new HarmonyMethod(patchType.GetMethod(nameof(IsLoggedOnPostfix), flags)));
                    Plugin.Log.LogInfo("[SteamClientPatch] Patched SteamClient.IsLoggedOn");
                }
                else
                {
                    Plugin.Log.LogWarning("[SteamClientPatch] SteamClient.IsLoggedOn not found");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[SteamClientPatch] Failed to apply valid patches: {ex}");
            }
        }

        // ========================
        // Patch Methods
        // ========================

        /// <summary>
        /// Spoof SteamClient.SteamId to return our spoofed ID
        /// </summary>
        public static void SteamIdPostfix(ref SteamId __result)
        {
            if (!_identityInitialized) return;

            var original = __result;
            __result = _spoofedSteamId;

            // Only log first time to avoid spam
            if (original.Value != _spoofedSteamId.Value)
            {
                Plugin.Log.LogDebug($"[SteamClientPatch] SteamId spoofed: {original.Value} -> {_spoofedSteamId.Value}");
            }
        }

        /// <summary>
        /// Spoof SteamClient.Name to return our spoofed name
        /// </summary>
        public static void NamePostfix(ref string __result)
        {
            if (!_identityInitialized) return;

            var original = __result;
            __result = _spoofedName;

            // Only log first time to avoid spam
            if (original != _spoofedName)
            {
                Plugin.Log.LogDebug($"[SteamClientPatch] Name spoofed: {original} -> {_spoofedName}");
            }
        }

        /// <summary>
        /// Force SteamClient.IsValid to return true
        /// </summary>
        public static void IsValidPostfix(ref bool __result)
        {
            if (Plugin.SplituxCfg == null) return;

            if (!__result)
            {
                Plugin.Log.LogDebug("[SteamClientPatch] SteamClient.IsValid was FALSE - forcing TRUE");
                __result = true;
            }
        }

        /// <summary>
        /// Force SteamClient.IsLoggedOn to return true
        /// </summary>
        public static void IsLoggedOnPostfix(ref bool __result)
        {
            if (Plugin.SplituxCfg == null) return;

            if (!__result)
            {
                Plugin.Log.LogDebug("[SteamClientPatch] SteamClient.IsLoggedOn was FALSE - forcing TRUE");
                __result = true;
            }
        }
    }
}
