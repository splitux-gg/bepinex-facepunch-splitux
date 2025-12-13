using HarmonyLib;
using System;
using System.Reflection;
using System.Text;

namespace SplituxFacepunch
{
    /// <summary>
    /// Library of reusable patch actions that can be applied to game-specific targets.
    /// Users can PR new actions to expand the library.
    /// </summary>
    public static class PatchActions
    {
        /// <summary>
        /// Get the Harmony prefix/postfix methods for a named action.
        /// Returns null for both if action is unknown.
        /// </summary>
        public static (HarmonyMethod prefix, HarmonyMethod postfix) GetAction(string actionName)
        {
            var type = typeof(PatchActions);
            var flags = BindingFlags.Public | BindingFlags.Static;

            switch (actionName.ToLower())
            {
                case "force_true":
                    return (null, new HarmonyMethod(type.GetMethod(nameof(ForceTruePostfix), flags)));

                case "force_false":
                    return (null, new HarmonyMethod(type.GetMethod(nameof(ForceFalsePostfix), flags)));

                case "skip":
                    return (new HarmonyMethod(type.GetMethod(nameof(SkipPrefix), flags)), null);

                case "force_steam_loaded":
                    return (null, new HarmonyMethod(type.GetMethod(nameof(ForceSteamLoadedPostfix), flags)));

                case "fake_auth_ticket":
                    return (new HarmonyMethod(type.GetMethod(nameof(FakeAuthTicketPrefix), flags)), null);

                case "photon_auth_none":
                    return (new HarmonyMethod(type.GetMethod(nameof(PhotonAuthNonePrefix), flags)), null);

                case "log_call":
                    return (new HarmonyMethod(type.GetMethod(nameof(LogCallPrefix), flags)), null);

                default:
                    Plugin.Log.LogWarning($"Unknown action: {actionName}");
                    return (null, null);
            }
        }

        // ========================
        // Action Implementations
        // ========================

        /// <summary>
        /// force_true: Force a bool return value to true.
        /// Use for: IsValid, IsLoggedOn, steamLoaded getters, etc.
        /// </summary>
        public static void ForceTruePostfix(ref bool __result)
        {
            if (Plugin.SplituxCfg == null) return;

            if (!__result)
            {
                __result = true;
                Plugin.Log.LogDebug("[force_true] Forced result to TRUE");
            }
        }

        /// <summary>
        /// force_false: Force a bool return value to false.
        /// </summary>
        public static void ForceFalsePostfix(ref bool __result)
        {
            if (Plugin.SplituxCfg == null) return;

            if (__result)
            {
                __result = false;
                Plugin.Log.LogDebug("[force_false] Forced result to FALSE");
            }
        }

        /// <summary>
        /// skip: Skip the original method entirely.
        /// Use for: Quit calls, error handlers, validation methods.
        /// </summary>
        public static bool SkipPrefix()
        {
            if (Plugin.SplituxCfg == null) return true;

            Plugin.Log.LogDebug("[skip] Skipping original method");
            return false;
        }

        /// <summary>
        /// force_steam_loaded: Set steamLoaded=true, steamId=spoofed, steamName=spoofed.
        /// Use for: SteamManager.DoSteam, SteamManager.Awake, etc.
        /// This is the KEY fix that makes multiple instances work.
        /// </summary>
        public static void ForceSteamLoadedPostfix(object __instance)
        {
            if (Plugin.SplituxCfg == null || __instance == null) return;

            try
            {
                var type = __instance.GetType();
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                Plugin.Log.LogInfo("[force_steam_loaded] Forcing Steam state...");

                // Force steamLoaded = true
                var steamLoadedField = type.GetField("steamLoaded", flags);
                if (steamLoadedField != null)
                {
                    var before = steamLoadedField.GetValue(__instance);
                    steamLoadedField.SetValue(__instance, true);
                    Plugin.Log.LogInfo($"[force_steam_loaded] steamLoaded: {before} -> TRUE");
                }

                // Force steamId to our spoofed value
                var steamIdField = type.GetField("steamId", flags);
                if (steamIdField != null)
                {
                    var currentId = steamIdField.GetValue(__instance);
                    if (currentId != null)
                    {
                        var valueField = currentId.GetType().GetField("Value", BindingFlags.Public | BindingFlags.Instance);
                        if (valueField != null)
                        {
                            var beforeId = (ulong)valueField.GetValue(currentId);
                            var newId = Activator.CreateInstance(currentId.GetType());
                            valueField.SetValue(newId, Plugin.SplituxCfg.SteamId);
                            steamIdField.SetValue(__instance, newId);
                            Plugin.Log.LogInfo($"[force_steam_loaded] steamId: {beforeId} -> {Plugin.SplituxCfg.SteamId}");
                        }
                    }
                }

                // Force steamName
                var steamNameField = type.GetField("steamName", flags);
                if (steamNameField != null)
                {
                    var beforeName = steamNameField.GetValue(__instance);
                    steamNameField.SetValue(__instance, Plugin.SplituxCfg.AccountName);
                    Plugin.Log.LogInfo($"[force_steam_loaded] steamName: {beforeName} -> {Plugin.SplituxCfg.AccountName}");
                }

                Plugin.Log.LogInfo("[force_steam_loaded] Steam state forced successfully");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[force_steam_loaded] Error: {ex}");
            }
        }

        /// <summary>
        /// fake_auth_ticket: Return a fake Steam auth ticket string.
        /// Use for: GetSteamAuthTicket methods that return string.
        /// </summary>
        public static bool FakeAuthTicketPrefix(ref string __result)
        {
            if (Plugin.SplituxCfg == null) return true;

            try
            {
                Plugin.Log.LogInfo("[fake_auth_ticket] Generating fake auth ticket...");

                // Generate a deterministic fake ticket based on Steam ID
                var random = new Random((int)(Plugin.SplituxCfg.SteamId & 0xFFFFFFFF));
                var fakeTicket = new byte[224];
                random.NextBytes(fakeTicket);

                // Embed our spoofed Steam ID in the ticket (bytes 0-7)
                var steamIdBytes = BitConverter.GetBytes(Plugin.SplituxCfg.SteamId);
                Array.Copy(steamIdBytes, 0, fakeTicket, 0, 8);

                // Convert to hex string
                var sb = new StringBuilder();
                foreach (var b in fakeTicket)
                {
                    sb.AppendFormat("{0:x2}", b);
                }
                __result = sb.ToString();

                Plugin.Log.LogInfo($"[fake_auth_ticket] Generated ticket (length: {__result.Length} chars)");
                return false; // Skip original method
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[fake_auth_ticket] Error: {ex}");
                return true; // Fall back to original
            }
        }

        // Random UserId generated once per session
        private static string _randomUserId;

        /// <summary>
        /// photon_auth_none: Set AuthType=255 (None) and random UserId.
        /// Use for: PhotonNetwork.AuthValues setter.
        /// </summary>
        public static void PhotonAuthNonePrefix(ref object value)
        {
            if (Plugin.SplituxCfg == null || value == null) return;

            try
            {
                var authType = value.GetType();

                // Generate random UserId once per session
                if (_randomUserId == null)
                {
                    _randomUserId = Guid.NewGuid().ToString();
                    Plugin.Log.LogInfo($"[photon_auth_none] Generated random Photon UserId: {_randomUserId}");
                }

                // Set UserId
                var userIdProp = authType.GetProperty("UserId");
                if (userIdProp != null)
                {
                    userIdProp.SetValue(value, _randomUserId);
                    Plugin.Log.LogInfo($"[photon_auth_none] Injected UserId: {_randomUserId}");
                }

                // Set AuthType to None (255)
                // IMPORTANT: 0 = Custom (rejected!), 1 = Steam, 255 = None
                var authTypeProp = authType.GetProperty("AuthType");
                if (authTypeProp != null)
                {
                    authTypeProp.SetValue(value, (byte)255);
                    Plugin.Log.LogInfo("[photon_auth_none] Set AuthType to None (255)");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogDebug($"[photon_auth_none] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// log_call: Log when a method is called (for debugging).
        /// </summary>
        public static void LogCallPrefix(MethodBase __originalMethod)
        {
            if (Plugin.SplituxCfg == null) return;

            var methodName = __originalMethod?.Name ?? "unknown";
            var typeName = __originalMethod?.DeclaringType?.Name ?? "unknown";
            Plugin.Log.LogInfo($"[log_call] {typeName}.{methodName}() called");
        }
    }
}
