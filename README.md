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

- While alive on T or CT, you can hide other living players, but never yourself. Dead players and spectators always see living players, so they can spectate them.
- A hidden player is hidden with everything attached to them: held and holstered weapons, store cosmetics, particles and anything else parented to their pawn. Dropped weapons and thrown grenades stay visible.
- Corpse hiding is a separate setting. `!hide dead off` shows corpses; `!hide off` only shows living players. While alive, corpses disappear as soon as the player dies. While dead or spectating, a corpse disappears about 6.5 seconds after its death (longer if the server's death camera is longer), because CS2 lets spectators switch to a recently dead player. A corpse you are watching stays visible and is checked again every second until your camera leaves it, since CS2 reports no camera change.
- Your own pawns (current, original, default and taken-over bot) and whatever your camera uses (view entity, and while dead your observer targets) are never hidden, and neither is anything they are attached to.
- Another plugin can override your choice for an activity. The menu then tells you the override is active.

Hiding works by stopping the server from sending entities to you (`IPlayer.ShouldBlockTransmitEntity`). There is no per-tick work: spawns, deaths, team changes, bot takeovers, round starts and choice changes queue one recalculation, which runs at the end of the current game frame, before the engine builds the snapshot sent to players. A few changes also apply the moment they happen, because one made by a timer or another plugin's `NextTick` can come after that recalculation and still reach the frame's snapshot. A weapon or anything else attached to a hidden player is blocked when it is attached, so it is never sent without its holder, and shown again when it is detached. When you die, change team or take over a bot, your blocks are cleared, because your camera can move to another player, such as your killer. A player who respawns is shown at once to dead players and spectators.

SwiftlyS2 stops calling a game event's remaining handlers, in every plugin, once one returns `Handled` or `Stop`. GameManager returns `Handled` for `player_team` when `IgnoreJoinTeamMessages` is on, so HidePlayers listens to each event both before and after it fires. A plugin that returns `Stop` before `player_death` fires (GameManager's `HideKillFeed: 2`) removes the event entirely, and HidePlayers then learns of the death only at the next spawn, death or round start.

Those bits are per viewer and shared with other plugins that block the same entities. HidePlayers does not claim a bit another plugin already set and only clears bits it set. SwiftlyS2 clears an entity's bits when the entity is deleted and a viewer's bits when they disconnect. HidePlayers releases its blocks on map unload and plugin unload. SwiftlyS2 has no per-plugin ownership for these bits: two plugins managing the same pawn cannot have independent blocks. Use one plugin to manage player-pawn visibility.

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

## Engine behavior this relies on

Checked on 2026-10-25 against `SwiftlyS2.CS2/1.5.1-beta.3` (source revision `8d7fda2`) and CS2 1.41.9.2 (`server.dll`, `client.dll` and `engine2.dll` from build 2000935). Re-check these after a CS2 or SwiftlyS2 update.

- SwiftlyS2 filters the blocked bits after the game's `CheckTransmit`, and runs `NextTick` work at the end of its `GameFrame` hook. The engine sends snapshots afterwards (`CNetworkGameServer::PrepareSendClientUpdatesAsync`), so a recalculation queued during a frame applies to that frame's snapshot. Work queued while that queue runs waits for the next frame, and timers run after the queue, so changes made there reach the snapshot before a queued recalculation sees them. Deleting an entity clears its bits for every viewer before the managed callback.
- Weapons are equipped with `FollowEntity(owner)`, so they are scene-graph children of the pawn. Every `SetParent` notifies the entity system, which SwiftlyS2 forwards as `OnEntityParentChanged`. On the client a parent change only marks the hierarchy dirty, so a weapon whose pawn is not sent has no parent to follow. That was the cause of weapons floating where a hidden player was last seen.
- CS2 has no `spec_target_updated` or `spec_mode_updated` event; the server never fires them. Spectator camera changes therefore cannot be followed by events.
- `CPlayer_ObserverServices::IsValidObserverTarget` accepts a dying or dead pawn until `m_flDeathTime + 6.0`, and rejects the observer's own pawn, `EF_NODRAW` pawns and `LIFE_RESPAWNABLE` pawns. The observer think moves a spectator off a target once it is invalid. The default death camera (`spec_freeze_deathanim_time` 0.8 + `spec_freeze_traveltime` 0.3 + `spec_freeze_time` 3.0) ends within that window.

Live checks still required:

1. Hide all/team/enemy, then give a hidden player weapons (buy, `!guns`, pickup) and drop them. Weapons stay with the hidden player and dropped weapons appear.
2. Store cosmetics, the warden coin and other attached props disappear with their player.
3. Die while players are hidden, watch the death camera, cycle spectator targets and modes, then respawn (`!af`). The camera keeps working and living players stay visible while dead.
4. With corpse hiding on, spectate a player who dies, and press next/previous within 6 seconds of other deaths. No client error, and corpses disappear about 6.5 seconds after death.
5. Restart rounds, change maps, swap teams (`!swap @me`), reconnect into a reused slot and reload HidePlayers mid-round. Hiding resumes, and disabling or unloading releases every block.

## Implemented features

- [x] Hide preferences
- [x] Menu with all/team/enemy/off choices and a separate corpse choice
- [x] Persistence across sessions through the Cookies plugin
- [x] SwiftlyS2 component menu (`Core.Menu`) with a configurable look and keys
- [x] Shared API for temporary activity overrides and custom menus
- [x] Editable Panorama XML/CSS menu with configurable renderer and layout path
- [x] Hiding the weapons and attachments of hidden players

Pre-release checks that need a running server and client are listed in [docs/remaining-tests.md](docs/remaining-tests.md).

## Acknowledgements and license

Inspired by [CS2-HidePlayers](https://github.com/qstage/CS2-HidePlayers). See [LICENSE](LICENSE).
