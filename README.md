# SplituxFacepunch

BepInEx plugin for [splitux](https://github.com/splitux-gg/splitux) that enables local split-screen multiplayer in games using Facepunch.Steamworks.

## Features

- **Identity Spoofing**: Each instance gets a unique Steam ID and display name
- **Facepunch Library Patches**: Patches `SteamClient.SteamId`, `SteamClient.Name`, `IsValid`, `IsLoggedOn`
- **Photon Auth Bypass**: Sets `AuthType=255` (None) for games using Photon networking
- **Runtime Patches**: Apply game-specific patches via config (no recompilation needed)

## How It Works

Splitux generates a `BepInEx/config/splitux.cfg` for each game instance. The plugin reads this config and applies:

1. **Facepunch Settings** - Toggle switches for known library patches
2. **Runtime Patches** - Game-specific class/method patches using Harmony

## Configuration Format

```ini
[Identity]
player_index=0
account_name=Player 1
steam_id=76561198000000001

[Facepunch]
spoof_identity=true
force_valid=true
photon_bypass=true

[RuntimePatches]
patch.0.class=SteamManager
patch.0.method=DoSteam
patch.0.action=force_steam_loaded

patch.1.class=GameManager
patch.1.property=DisableSteamAuthorizationForPhoton
patch.1.action=force_true
```

## Available Actions

| Action | Description |
|--------|-------------|
| `force_true` | Force bool return to true |
| `force_false` | Force bool return to false |
| `skip` | Skip original method entirely |
| `force_steam_loaded` | Set steamLoaded=true, steamId/steamName to spoofed values |
| `fake_auth_ticket` | Return fake Steam auth ticket string |
| `photon_auth_none` | Set Photon AuthType=255, random UserId |
| `log_call` | Log method calls (debug) |

## Splitux Handler Example

```yaml
name: "Across The Obelisk"
steam_appid: 1385380
exec: "AcrossTheObelisk.exe"

facepunch_settings:
  spoof_identity: true
  force_valid: true
  photon_bypass: true

runtime_patches:
  - class: "SteamManager"
    method: "DoSteam"
    action: "force_steam_loaded"
  - class: "GameManager"
    property: "DisableSteamAuthorizationForPhoton"
    action: "force_true"
  - class: "NetworkManager"
    method: "GetSteamAuthTicket"
    action: "fake_auth_ticket"
  - class: "GameManager"
    method: "SteamNotConnected"
    action: "skip"
```

## Supported Games

Games using Facepunch.Steamworks:
- Across The Obelisk
- (more as tested)

## Building

```bash
dotnet build src/SplituxFacepunch.csproj -c Release
```

Output: `src/bin/Release/net472/SplituxFacepunch.dll`

## License

MIT
