<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>HidePlayers</strong></h2>
  <h3>Let players toggle visibility of other player models on a CS2 SwiftlyS2 server.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/btnrv/SW2-HidePlayers/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/btnrv/SW2-HidePlayers?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/btnrv/SW2-HidePlayers" alt="License">
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4" alt=".NET 10">
</p>

---

## Commands

| Command (default) | Description |
|---|---|
| `!hide` / `!hidemodels` | Toggle hiding other player models |
| `hide_reload` | Reload the config (admin) |

Command names and permissions are fully configurable in `config.jsonc`.

## Configuration

Config file: `cfg/hideplayers/config.jsonc`

| Option | Default | Description |
|---|---|---|
| `Prefix` | `[gold][Hide][default]` | Chat message prefix |
| `Commands` | `["hide", "hidemodels"]` | Commands that toggle visibility |
| `CommandPermission` | `""` | Permission required to use the toggle (empty = everyone) |
| `ReloadCommand` | `"hide_reload"` | Command to reload the config |
| `ReloadPermission` | `"hideplayers.reload"` | Permission required to reload |
| `HideMode` | `"@all"` | Who gets hidden when toggled (see below) |

### Hide Modes

| Value | Behavior |
|---|---|
| `@all` | Hides all other players |
| `@team` | Hides only teammates |
| `@enemy` | Hides only enemies |

## Building from Source

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- SwiftlyS2 server runtime

### Build

```bash
git clone https://github.com/btnrv/HidePlayers.git
cd HidePlayers
dotnet publish -c Release
```

The output zip will be at `build/publish/HidePlayers.zip`, ready to drop into your server.

## Installation

1. Extract `HidePlayers.zip` into your SwiftlyS2 `plugins/` directory.
2. Edit `cfg/hideplayers/config.jsonc` to set your preferred commands, permissions, and hide mode.
3. Restart the server or reload SwiftlyS2.

---

## Planned Features

- [ ] Hide dead players (remove corpses from transmit for better FPS)
- [ ] "Hide Menu" so players can select hide target instead of a simple toggle
- [ ] Cookies dependency for preference persistence across sessions
- [ ] Add shared API so other plugins can control player hide state dict (useful for minigame plugins etc.)

## Acknowledgements

Inspired by and mostly ported from [CS2-HidePlayers](https://github.com/qstage/CS2-HidePlayers).

## License

See [LICENSE](LICENSE) for details.
