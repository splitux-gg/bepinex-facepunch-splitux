using HarmonyLib;
using System;
using System.Reflection;

namespace SplituxFacepunch.Patches
{
    /// <summary>
    /// Patches GameManager.DisableSteamAuthorizationForPhoton to always return true.
    /// When true, Photon uses AuthType=None and won't validate Steam tickets.
    /// </summary>
    public static class NetworkManagerPatch
    {
        /// <summary>
        /// Apply DisableSteamAuthorizationForPhoton patch.
        /// </summary>
        public static void Apply(Harmony harmony)
        {
            try
            {
                // Find GameManager type (ATO uses GameManager, not NetworkManager for this)
                var gameManagerType = FindType("GameManager");
                if (gameManagerType == null)
                {
                    Plugin.Log.LogDebug("[NetworkManagerPatch] GameManager not found - skipping");
                    return;
                }

                var patchType = typeof(NetworkManagerPatch);
                var flags = BindingFlags.Static | BindingFlags.Public;

                // Patch DisableSteamAuthorizationForPhoton getter
                var disableAuthProp = gameManagerType.GetProperty("DisableSteamAuthorizationForPhoton",
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                if (disableAuthProp != null)
                {
                    var getter = disableAuthProp.GetGetMethod(true);
                    if (getter != null)
                    {
                        harmony.Patch(getter,
                            postfix: new HarmonyMethod(patchType.GetMethod(nameof(DisableSteamAuthPostfix), flags)));
                        Plugin.Log.LogInfo("[NetworkManagerPatch] Patched DisableSteamAuthorizationForPhoton");
                    }
                }
                else
                {
                    Plugin.Log.LogDebug("[NetworkManagerPatch] DisableSteamAuthorizationForPhoton property not found");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[NetworkManagerPatch] Failed: {ex}");
            }
        }

        /// <summary>
        /// Force DisableSteamAuthorizationForPhoton to return true.
        /// This makes the game set AuthType=None when connecting to Photon.
        /// </summary>
        public static void DisableSteamAuthPostfix(ref bool __result)
        {
            if (Plugin.SplituxCfg == null) return;

            if (!__result)
            {
                Plugin.Log.LogInfo("[NetworkManagerPatch] DisableSteamAuthorizationForPhoton was FALSE - forcing TRUE");
                __result = true;
            }
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
