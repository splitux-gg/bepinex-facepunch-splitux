using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace SplituxFacepunch
{
    /// <summary>
    /// Facepunch.Steamworks patch settings.
    /// Controls which known library patches are applied.
    /// </summary>
    public class FacepunchSettings
    {
        /// <summary>Spoof SteamClient.SteamId and SteamClient.Name</summary>
        public bool SpoofIdentity { get; set; }

        /// <summary>Force SteamClient.IsValid and IsLoggedOn to return true</summary>
        public bool ForceValid { get; set; }

        /// <summary>Bypass Photon Steam auth (AuthType=255)</summary>
        public bool PhotonBypass { get; set; }
    }

    /// <summary>
    /// A runtime patch specification for game-specific classes.
    /// </summary>
    public class RuntimePatch
    {
        public string Class { get; set; }
        public string Method { get; set; }
        public string Property { get; set; }
        public string Action { get; set; }

        /// <summary>Target is method if Method is set, otherwise Property getter</summary>
        public bool IsMethod => !string.IsNullOrEmpty(Method);

        public override string ToString()
        {
            var target = IsMethod ? $"{Class}.{Method}()" : $"{Class}.{Property}";
            return $"{target} -> {Action}";
        }
    }

    /// <summary>
    /// Configuration read from splitux launcher.
    /// Splitux writes a config file that tells each instance its player index,
    /// spoofed identity, patch settings, and runtime patches to apply.
    /// </summary>
    public class SplituxConfig
    {
        // Identity
        public int PlayerIndex { get; set; } = 0;
        public ulong SteamId { get; set; }
        public string AccountName { get; set; } = "Player";

        // Patch settings
        public FacepunchSettings Facepunch { get; set; } = new FacepunchSettings();

        // Runtime patches
        public List<RuntimePatch> RuntimePatches { get; set; } = new List<RuntimePatch>();

        // Base Steam ID for generating unique IDs per player
        private const ulong SteamIdBase = 76561198000000000;

        /// <summary>
        /// Load config from splitux config file.
        /// Looks for: BepInEx/config/splitux.cfg
        /// </summary>
        public static SplituxConfig Load()
        {
            var configPath = Path.Combine(
                BepInEx.Paths.ConfigPath,
                "splitux.cfg"
            );

            if (!File.Exists(configPath))
            {
                Plugin.Log.LogWarning($"Config not found at {configPath}");
                return null;
            }

            var config = new SplituxConfig();
            var currentSection = "";
            var patchData = new Dictionary<int, Dictionary<string, string>>();

            foreach (var line in File.ReadAllLines(configPath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith(";"))
                    continue;

                // Section header
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    currentSection = trimmed.Substring(1, trimmed.Length - 2).ToLower();
                    continue;
                }

                // Key=Value
                var eqIdx = trimmed.IndexOf('=');
                if (eqIdx < 0) continue;

                var key = trimmed.Substring(0, eqIdx).Trim().ToLower();
                var value = trimmed.Substring(eqIdx + 1).Trim();

                switch (currentSection)
                {
                    case "identity":
                        ParseIdentity(config, key, value);
                        break;

                    case "facepunch":
                        ParseFacepunch(config.Facepunch, key, value);
                        break;

                    case "runtimepatches":
                        ParseRuntimePatch(patchData, key, value);
                        break;
                }
            }

            // Build runtime patches from collected data
            BuildRuntimePatches(config, patchData);

            // Generate Steam ID from player index if not explicitly set
            if (config.SteamId == 0)
            {
                config.SteamId = SteamIdBase + (ulong)config.PlayerIndex + 1;
            }

            // Generate account name from player index if default
            if (config.AccountName == "Player")
            {
                config.AccountName = $"Player {config.PlayerIndex + 1}";
            }

            return config;
        }

        private static void ParseIdentity(SplituxConfig config, string key, string value)
        {
            switch (key)
            {
                case "player_index":
                    if (int.TryParse(value, out var idx))
                        config.PlayerIndex = idx;
                    break;
                case "steam_id":
                    if (ulong.TryParse(value, out var steamId))
                        config.SteamId = steamId;
                    break;
                case "account_name":
                    config.AccountName = value;
                    break;
            }
        }

        private static void ParseFacepunch(FacepunchSettings settings, string key, string value)
        {
            var boolValue = value.ToLower() == "true" || value == "1";

            switch (key)
            {
                case "spoof_identity":
                    settings.SpoofIdentity = boolValue;
                    break;
                case "force_valid":
                    settings.ForceValid = boolValue;
                    break;
                case "photon_bypass":
                    settings.PhotonBypass = boolValue;
                    break;
            }
        }

        private static void ParseRuntimePatch(Dictionary<int, Dictionary<string, string>> patchData, string key, string value)
        {
            // Format: patch.0.class=SteamManager
            var match = Regex.Match(key, @"^patch\.(\d+)\.(\w+)$");
            if (!match.Success) return;

            var idx = int.Parse(match.Groups[1].Value);
            var field = match.Groups[2].Value;

            if (!patchData.ContainsKey(idx))
                patchData[idx] = new Dictionary<string, string>();

            patchData[idx][field] = value;
        }

        private static void BuildRuntimePatches(SplituxConfig config, Dictionary<int, Dictionary<string, string>> patchData)
        {
            foreach (var kvp in patchData)
            {
                var data = kvp.Value;
                if (!data.ContainsKey("class") || !data.ContainsKey("action"))
                {
                    Plugin.Log.LogWarning($"RuntimePatch {kvp.Key} missing required fields (class, action)");
                    continue;
                }

                var patch = new RuntimePatch
                {
                    Class = data["class"],
                    Action = data["action"],
                    Method = data.ContainsKey("method") ? data["method"] : null,
                    Property = data.ContainsKey("property") ? data["property"] : null
                };

                if (string.IsNullOrEmpty(patch.Method) && string.IsNullOrEmpty(patch.Property))
                {
                    Plugin.Log.LogWarning($"RuntimePatch {kvp.Key} needs either method or property");
                    continue;
                }

                config.RuntimePatches.Add(patch);
                Plugin.Log.LogDebug($"Loaded RuntimePatch: {patch}");
            }
        }
    }
}
