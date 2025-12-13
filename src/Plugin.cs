using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using SplituxFacepunch.Patches;

namespace SplituxFacepunch
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "gg.splitux.facepunch";
        public const string PluginName = "SplituxFacepunch";
        public const string PluginVersion = "2.1.0";

        internal static ManualLogSource Log;
        internal static SplituxConfig SplituxCfg;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo($"{PluginName} v{PluginVersion} loading...");

            // Load splitux config
            SplituxCfg = SplituxConfig.Load();
            if (SplituxCfg == null)
            {
                Log.LogWarning("No splitux config found - running in passthrough mode");
                return;
            }

            Log.LogInfo($"Player Index: {SplituxCfg.PlayerIndex}");
            Log.LogInfo($"Spoofed Steam ID: {SplituxCfg.SteamId}");
            Log.LogInfo($"Spoofed Name: {SplituxCfg.AccountName}");

            // Log facepunch settings
            Log.LogInfo($"Facepunch Settings:");
            Log.LogInfo($"  spoof_identity: {SplituxCfg.Facepunch.SpoofIdentity}");
            Log.LogInfo($"  force_valid: {SplituxCfg.Facepunch.ForceValid}");
            Log.LogInfo($"  photon_bypass: {SplituxCfg.Facepunch.PhotonBypass}");
            Log.LogInfo($"  runtime_patches: {SplituxCfg.RuntimePatches.Count}");

            _harmony = new Harmony(PluginGuid);

            // ========================
            // Apply facepunch_settings patches (known library targets)
            // ========================

            if (SplituxCfg.Facepunch.SpoofIdentity)
            {
                Log.LogInfo("Applying spoof_identity patches...");
                SteamClientPatch.ApplyIdentityPatches(_harmony, SplituxCfg);
            }

            if (SplituxCfg.Facepunch.ForceValid)
            {
                Log.LogInfo("Applying force_valid patches...");
                SteamClientPatch.ApplyValidPatches(_harmony);
            }

            if (SplituxCfg.Facepunch.PhotonBypass)
            {
                Log.LogInfo("Applying photon_bypass patches...");
                PhotonPatch.ApplyAuthBypass(_harmony);
            }

            // ========================
            // Apply game-specific patches (ATO and similar games)
            // ========================

            // SteamManager patches - force steamLoaded=true after DoSteam() caches values
            Log.LogInfo("Applying SteamManager patches...");
            SteamManagerPatch.Apply(_harmony, SplituxCfg);

            // NetworkManager patches - force DisableSteamAuthorizationForPhoton=true
            Log.LogInfo("Applying NetworkManager patches...");
            NetworkManagerPatch.Apply(_harmony);

            // SteamAuthTicket patches - return fake ticket (Steam rejects 2nd ticket from same account)
            Log.LogInfo("Applying SteamAuthTicket patches...");
            SteamAuthTicketPatch.Apply(_harmony, SplituxCfg);

            // ========================
            // Apply runtime_patches (game-specific targets from config)
            // ========================

            RuntimePatcher.ApplyAll(_harmony, SplituxCfg.RuntimePatches);

            Log.LogInfo($"{PluginName} loaded successfully!");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
