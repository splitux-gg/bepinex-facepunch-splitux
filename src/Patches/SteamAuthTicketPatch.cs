using HarmonyLib;
using System;
using System.Reflection;
using System.Text;

namespace SplituxFacepunch.Patches
{
    /// <summary>
    /// Patches NetworkManager.GetSteamAuthTicket to return a fake ticket.
    /// Steam only allows ONE auth ticket per account at a time - second instance fails.
    /// By returning a fake ticket and setting AuthType=None, Photon won't validate it.
    /// </summary>
    public static class SteamAuthTicketPatch
    {
        private static string _fakeTicket;

        /// <summary>
        /// Apply fake auth ticket patch.
        /// </summary>
        public static void Apply(Harmony harmony, SplituxConfig config)
        {
            try
            {
                // Generate fake ticket with embedded Steam ID (448 hex chars like real tickets)
                _fakeTicket = GenerateFakeTicket(config.SteamId);
                Plugin.Log.LogInfo($"[SteamAuthTicketPatch] Generated fake ticket (length={_fakeTicket.Length})");

                // Find NetworkManager type
                var networkManagerType = FindType("NetworkManager");
                if (networkManagerType == null)
                {
                    Plugin.Log.LogDebug("[SteamAuthTicketPatch] NetworkManager not found - skipping");
                    return;
                }

                var patchType = typeof(SteamAuthTicketPatch);
                var flags = BindingFlags.Static | BindingFlags.Public;

                // Patch GetSteamAuthTicket
                var getTicketMethod = networkManagerType.GetMethod("GetSteamAuthTicket",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (getTicketMethod != null)
                {
                    harmony.Patch(getTicketMethod,
                        prefix: new HarmonyMethod(patchType.GetMethod(nameof(GetSteamAuthTicketPrefix), flags)));
                    Plugin.Log.LogInfo("[SteamAuthTicketPatch] Patched GetSteamAuthTicket - bypasses real Steam API");
                }
                else
                {
                    Plugin.Log.LogDebug("[SteamAuthTicketPatch] GetSteamAuthTicket method not found");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[SteamAuthTicketPatch] Failed: {ex}");
            }
        }

        /// <summary>
        /// Return fake auth ticket, skip real Steam API call.
        /// </summary>
        public static bool GetSteamAuthTicketPrefix(ref string __result)
        {
            if (Plugin.SplituxCfg == null) return true; // Run original

            Plugin.Log.LogInfo("[SteamAuthTicketPatch] Returning FAKE auth ticket");
            __result = _fakeTicket;
            return false; // Skip original method
        }

        /// <summary>
        /// Generate a fake Steam auth ticket (448 hex chars).
        /// Embeds the spoofed Steam ID for consistency.
        /// </summary>
        private static string GenerateFakeTicket(ulong steamId)
        {
            var random = new Random((int)(steamId & 0xFFFFFFFF));
            var bytes = new byte[224]; // 224 bytes = 448 hex chars
            random.NextBytes(bytes);

            // Embed Steam ID at offset 12 (where real tickets have it)
            var steamIdBytes = BitConverter.GetBytes(steamId);
            Array.Copy(steamIdBytes, 0, bytes, 12, 8);

            // Convert to hex string
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
            {
                sb.Append(b.ToString("X2"));
            }
            return sb.ToString();
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
