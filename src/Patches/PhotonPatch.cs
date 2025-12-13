using HarmonyLib;
using System;
using System.Reflection;

namespace SplituxFacepunch.Patches
{
    /// <summary>
    /// Patches Photon networking to bypass Steam authentication.
    /// Sets AuthType=255 (None) and injects a random UserId.
    /// </summary>
    public static class PhotonPatch
    {
        private static string _randomUserId;

        /// <summary>
        /// Apply Photon auth bypass: AuthType=255, random UserId.
        /// </summary>
        public static void ApplyAuthBypass(Harmony harmony)
        {
            try
            {
                // Generate random UserId once per session
                _randomUserId = Guid.NewGuid().ToString();
                Plugin.Log.LogInfo($"[PhotonPatch] Generated random UserId: {_randomUserId}");

                // Find PhotonNetwork class
                var photonNetworkType = FindType("Photon.Pun.PhotonNetwork") ?? FindType("PhotonNetwork");
                if (photonNetworkType == null)
                {
                    Plugin.Log.LogDebug("[PhotonPatch] PhotonNetwork type not found - skipping");
                    return;
                }

                var patchType = typeof(PhotonPatch);
                var flags = BindingFlags.Static | BindingFlags.Public;

                // Patch AuthValues setter to inject our UserId and AuthType
                var authValuesProp = photonNetworkType.GetProperty("AuthValues",
                    BindingFlags.Public | BindingFlags.Static);

                if (authValuesProp != null)
                {
                    var setter = authValuesProp.GetSetMethod(true);
                    if (setter != null)
                    {
                        harmony.Patch(setter,
                            prefix: new HarmonyMethod(patchType.GetMethod(nameof(AuthValuesSetterPrefix), flags)));
                        Plugin.Log.LogInfo("[PhotonPatch] Patched PhotonNetwork.AuthValues setter");
                    }
                    else
                    {
                        Plugin.Log.LogDebug("[PhotonPatch] AuthValues setter not found");
                    }
                }
                else
                {
                    Plugin.Log.LogDebug("[PhotonPatch] AuthValues property not found");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[PhotonPatch] Failed to apply auth bypass: {ex}");
            }
        }

        /// <summary>
        /// Intercept AuthValues setter to inject random UserId and set AuthType=None.
        /// IMPORTANT: 0=Custom (rejected!), 1=Steam, 255=None (what we want)
        /// </summary>
        public static void AuthValuesSetterPrefix(ref object value)
        {
            if (Plugin.SplituxCfg == null || value == null) return;

            try
            {
                var authType = value.GetType();

                // Set UserId
                var userIdProp = authType.GetProperty("UserId");
                if (userIdProp != null)
                {
                    userIdProp.SetValue(value, _randomUserId);
                    Plugin.Log.LogInfo($"[PhotonPatch] Injected UserId: {_randomUserId}");
                }

                // Set AuthType to None (255)
                var authTypeProp = authType.GetProperty("AuthType");
                if (authTypeProp != null)
                {
                    authTypeProp.SetValue(value, (byte)255);
                    Plugin.Log.LogInfo("[PhotonPatch] Set AuthType to None (255)");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogDebug($"[PhotonPatch] AuthValues injection failed: {ex.Message}");
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
