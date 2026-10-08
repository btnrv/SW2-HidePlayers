# HidePlayers

Plugin to allow players to hide players with persistence across sessions on a CS2 SwiftlyS2 server.

## Installation

1. Extract `HidePlayers.zip` into SwiftlyS2's `plugins/` directory, preserving the `HidePlayers/resources/exports/` folder.
2. Configure a MariaDB connection in SwiftlyS2's database configuration. HidePlayers uses the native `Core.Database` service and the connection named `default`; credentials belong to the server configuration.
3. Start the plugin to generate `configs/plugins/HidePlayers/settings.jsonc` under SwiftlyS2's configuration root. Edit it, then restart the plugin to apply changes.
4. Join the server and enter `!hide` to open the menu.

The database account must be able to create, alter, and read/write `hideplayers_preferences`. The plugin creates or updates this table automatically and loads/saves preferences asynchronously by SteamID.

## Commands

These examples use the default command names. The chat prefix comes from the server's command configuration; `!` is an example.

| Player console | Chat example | Action |
| --- | --- | --- |
| — | `!hide` / `!hidemodels` | Open the menu |
| `sw_hide` | — | Toggle player hiding off or back to your last selected mode |
| `sw_hide off` | `!hide off` | Show living players |
| `sw_hide all` | `!hide all` | Hide all other living players |
| `sw_hide team` | `!hide teammates` | Hide living teammates |
| `sw_hide enemy` | `!hide enemies` | Hide living enemies |
| `sw_hide dead on` | `!hide dead on` | Hide corpses |
| `sw_hide dead off` | `!hide dead off` | Show corpses |
| `sw_hide dead toggle` | `!hide dead toggle` | Toggle corpse visibility |

The console toggle remembers your last All/Team/Enemy choice across reconnects. It uses All if you have never selected a mode. Corpse hiding stays separate. These commands cannot be used from the server console.

You can use `teammates` instead of `team`, and `enemies` instead of `enemy`. While alive on T or CT, you can hide other players, but never yourself. Dead players and spectators can see living players.

Corpse hiding is a separate setting and also works while spectating. Use `!hide dead off` to show corpses; `!hide off` only shows living players.

By default, the menu uses your server's SwiftlyS2 style. You can also use the included Panorama menu. Both show your choices and tell you when another plugin overrides them. If another plugin provides a custom menu, `!hide` opens that instead.

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
    "PanoramaLayout": "panorama/layout/custom_game/hideplayers.vxml_c",
    "DatabaseConnection": "default",
    "Prefix": "{Gold}[HidePlayers]{Default} "
  }
}
```

| Setting | What it does |
| --- | --- |
| `DefaultPreferences` | Settings for players who have no saved choices. Modes: `Off`, `All`, `Teammates`, `Enemies`. |
| `MenuCommand.Permission` | Permission needed to use the menu or commands. Leave empty to allow everyone. |
| `DatabaseConnection` | Name of the MariaDB connection configured in SwiftlyS2. |
| `Prefix` | Text shown before the plugin's chat replies. |
| `MenuRenderer` | `Framework` for the usual SwiftlyS2 menu, or `Panorama` for the XML/CSS menu. |
| `PanoramaLayout` | Path to the compiled layout in your Panorama addon. |

To use Panorama, compile the editable files in `resources/panorama-addon/` into your addon and distribute it to players. Then set `MenuRenderer` to `Panorama`. See the [Panorama setup guide](docs/custom-menu.md#enable-the-bundled-panorama-menu).

## Shared API and custom menus

Other plugins can read a player's settings, temporarily change what they see, or provide a custom menu. See the [integration guide](docs/custom-menu.md) for the API and examples.

## Implemented features

- [x] Hide preferences
- [x] Menu with all/team/enemy/off choices
- [x] Database persistence across sessions
- [x] Shared API for temporary activity overrides and custom menus
- [x] Editable Panorama XML/CSS menu with configurable renderer and layout path

## Acknowledgements and license

Inspired by [CS2-HidePlayers](https://github.com/qstage/CS2-HidePlayers). See [LICENSE](LICENSE).
