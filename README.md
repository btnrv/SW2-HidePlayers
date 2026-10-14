# HidePlayers

Plugin to let players hide other players, and corpses, with their choice saved across sessions, on a CS2 SwiftlyS2 server.

## Requirements

- A SwiftlyS2 runtime that has `Core.Menu` (the component menu system, beta only). HidePlayers is built against `SwiftlyS2.CS2` 1.5.0-beta or later and does not load on a stable runtime without it.
- The [Cookies plugin](https://github.com/SwiftlyS2-Plugins/Cookies) with a database, to save choices. Without it HidePlayers still works, but choices last only until the player leaves.

## Installation

1. Extract `HidePlayers.zip` into SwiftlyS2's `plugins/` directory, preserving the `HidePlayers/resources/exports/` folder. It holds the shared API contract, which SwiftlyS2 loads at startup.
2. Install the Cookies plugin the same way. Cookies reads a database connection named `cookies` from SwiftlyS2's `database.jsonc` and falls back to the default connection if that name is missing. It supports SQLite, MySQL/MariaDB and PostgreSQL. Credentials belong to the server configuration, never to HidePlayers.
3. Restart the server. Installing or updating Cookies, or any plugin's exported contract (including `HidePlayers.Contract.dll`), needs a full restart, not a plugin reload.
4. Start the plugin to generate `configs/plugins/HidePlayers/settings.jsonc` under SwiftlyS2's configuration root. Keys added by later versions are inserted into an existing file with their defaults.
5. Join the server and enter `!hide` to open the menu.

## Saving choices

HidePlayers keeps each player's choice in the Cookies plugin, under the cookie key `hideplayers.preferences`. The value is a short text string, `1|<Mode>|<0|1>|<LastActiveMode>`.

- A choice is saved when the player is Steam-authorized. A choice made before authorization is written as soon as they are authorized.
- Choices are loaded in the background after authorization, so they can appear a moment after the player joins.
- Without Cookies, HidePlayers logs one warning and runs session-only.
- This version does not import the `hideplayers_preferences` table of earlier prerelease builds.

## Commands

These examples use the default command names. The chat prefix comes from the server's command configuration (`CommandPrefixes` for normal chat commands, `CommandSilentPrefixes` for silent ones); `!` is an example.

| Player console | Chat example | Action |
| --- | --- | --- |
| - | `!hide` / `!hidemodels` | Open the menu |
| `sw_hide` | - | Toggle player hiding off or back to your last selected mode |
| `sw_hide off` | `!hide off` | Show living players |
| `sw_hide all` | `!hide all` | Hide all other living players |
| `sw_hide team` | `!hide teammates` | Hide living teammates |
| `sw_hide enemy` | `!hide enemies` | Hide living enemies |
| `sw_hide dead on` | `!hide dead on` | Hide corpses |
| `sw_hide dead off` | `!hide dead off` | Show corpses |
| `sw_hide dead toggle` | `!hide dead toggle` | Toggle corpse visibility |

The console toggle remembers your last All/Team/Enemy choice across reconnects. It uses All if you have never selected a mode. Corpse hiding stays separate. These commands cannot be used from the server console.

You can use `teammates` instead of `team`, and `enemies` instead of `enemy`. If another plugin provides a custom menu, `!hide` opens that instead.

To restrict the commands, set `MenuCommand.Permission`, or map both `hide` and `sw_hide` (and each alias) in SwiftlyS2's `command_overrides.jsonc`.

## What is hidden

- While alive on T or CT, you can hide other living players, but never yourself. Dead players and spectators can see living players.
- Corpse hiding is a separate setting and also works while spectating. `!hide dead off` shows corpses; `!hide off` only shows living players.
- The player you are currently spectating is never hidden, even with corpse hiding on.
- Another plugin can override your choice for an activity. The menu then tells you the override is active.

Hiding works by stopping the server from sending a player's entity to you (`IPlayer.ShouldBlockTransmitEntity`). Those bits are per viewer and shared with other plugins that block the same entities. HidePlayers only ever unblocks entities it blocked itself, and it clears its own blocks when it unloads.

Known limitation: the weapons of a hidden player may still be visible. This still has to be confirmed on a live server and is not handled yet.

## Configuration

`settings.jsonc` has this structure:

```jsonc
{
  "Main": {
    "DefaultPreferences": {
      "Mode": "Off",
      "HideDead": true
    },
    "MenuCommand": {
      "Name": "hide",
      "Aliases": ["hidemodels"],
      "Permission": ""
    },
    "MenuRenderer": "Framework",
    "PanoramaLayout": "panorama/layout/custom_game/hideplayers/hideplayers.xml",
    "Prefix": "[gold][HidePlayers][default] "
  }
}
```

| Setting | What it does |
| --- | --- |
| `DefaultPreferences` | Settings for players who have no saved choices. Modes: `Off`, `All`, `Teammates`, `Enemies`. |
| `MenuCommand.Name`, `MenuCommand.Aliases` | The command and its aliases. They are registered as `sw_<name>` and as chat commands. Names must be non-empty, contain no spaces and be distinct. |
| `MenuCommand.Permission` | Permission needed to use the menu or commands. Leave empty to allow everyone. |
| `MenuRenderer` | `Framework` for the SwiftlyS2 component menu, or `Panorama` for the XML/CSS menu. |
| `PanoramaLayout` | Resource path of the Panorama layout in your addon. Required when `MenuRenderer` is `Panorama`. |
| `Prefix` | Text shown before the plugin's chat replies. It uses SwiftlyS2's bracket colour tokens such as `[gold]` and `[default]`. The `{Gold}` brace style is not supported. |

An invalid setting stops the plugin from loading, and the server log names the setting. The prefix and default choices apply as soon as the file changes. Command names, aliases, permission and `MenuRenderer` apply when the plugin is reloaded.

## Menus

**Framework menu.** The default menu is a SwiftlyS2 `Core.Menu` menu with the id `hideplayers.menu`. Its look and keys come from SwiftlyS2's `configs/menus/menus.jsonc`, not from HidePlayers. For example, to draw it as chat lines and change its keys:

```jsonc
{
  "Menus": {
    "hideplayers.menu": { "Renderer": "chat", "Select": ["e", "space"], "Close": "esc" }
  }
}
```

The selected mode is marked in the menu, and the menu updates if your choice changes while it is open. The classic `Menu` section in `core.jsonc` does not apply to it.

**Panorama menu.** Set `MenuRenderer` to `Panorama` to use the included XML/CSS menu. Compile the files in `resources/panorama-addon/` into your addon and deliver the addon to players first. See the [Panorama setup guide](docs/custom-menu.md#enable-the-bundled-panorama-menu).

Both menus show your choices and tell you when another plugin overrides them.

## Shared API and custom menus

Other plugins can read a player's settings, temporarily change what they see, or provide a custom menu. The API key is `HidePlayers.API.v1`.

- All calls run on the game thread, and a call from another thread throws `InvalidOperationException`.
- Overrides are session-only. They are never saved, and they are cleared when the player disconnects and when the map unloads.
- Only one menu provider can register at a time, and it must unregister when its plugin unloads.

See the [integration guide](docs/custom-menu.md#consume-the-shared-api) for the API and examples.

## Building

```bash
dotnet publish HidePlayers.csproj -c Release
```

Publishing writes `build/publish/HidePlayers/` and `build/HidePlayers.zip`. The Cookies contract is compiled from the vendored `lib/Cookies.Contract.dll`, built from [SwiftlyS2-Plugins/Cookies](https://github.com/SwiftlyS2-Plugins/Cookies) (MIT). It is not published, because the Cookies plugin exports it. The release workflow in `.github/workflows/release.yml` builds and uploads the zip for each version tag.

## Implemented features

- [x] Hide preferences
- [x] Menu with all/team/enemy/off choices and a separate corpse choice
- [x] Persistence across sessions through the Cookies plugin
- [x] SwiftlyS2 component menu (`Core.Menu`) with a configurable look and keys
- [x] Shared API for temporary activity overrides and custom menus
- [x] Editable Panorama XML/CSS menu with configurable renderer and layout path
- [ ] Hiding the weapons of hidden players (to be confirmed on a live server)

Pre-release checks that need a running server and client are listed in [docs/remaining-tests.md](docs/remaining-tests.md).

## Acknowledgements and license

Inspired by [CS2-HidePlayers](https://github.com/qstage/CS2-HidePlayers). See [LICENSE](LICENSE).
