# Live-server checklist

A build only checks types. Everything below needs a running SwiftlyS2 server, usually with two clients, and the Panorama items need a client with the addon mounted. Tick an item once it has been seen working, and note the SwiftlyS2 and CS2 versions you tested.

## Visibility

- [ ] Switch between off/all/team/enemy and both corpse settings. Check teammates and enemies separately, before death and after respawn, including a corpse that already exists when the choice changes.
- [ ] You never lose sight of yourself. As a living player with `all`, your own player and first-person view stay normal.
- [ ] Spectate a target with `HideDead` on, then let that target die. The client stays stable, the target stays visible, and other living players stay visible to the spectator.
- [ ] Switch the spectate target (mouse click) and the spectate mode while hiding is on. The new target is never hidden and the old one follows the normal rules.
- [ ] Dead players and spectators see living players, whatever their mode.
- [ ] Change teams, join spectators and rejoin. The hidden set follows the new teams within a tick or two.
- [ ] Teams other than T and CT (spectator, unassigned) are never hidden or hiding.
- [ ] Reconnect and change maps. No transmit blocks remain on old player or entity slots, and another player who takes a freed slot starts with defaults.
- [ ] Weapons of a hidden player: confirm whether they stay visible (held, dropped, on the back). This is not implemented; record what you see before deciding.
- [ ] Run another plugin that blocks transmit for the same viewer. HidePlayers unblocks only what it blocked, and the other plugin's blocks survive both an unblock and a plugin reload.
- [ ] Unload and reload HidePlayers with players hidden. Everyone is visible again at once, and the reload re-applies each player's saved choice.
- [ ] Join a server that is mid-round with the plugin already loaded (hot reload). Connected players get sessions and correct visibility.

## Saving with Cookies

- [ ] Choose a mode and corpse setting, reconnect, and check the choice returns. Check the `hideplayers.preferences` cookie in the database, value `1|<Mode>|<0|1>|<LastActiveMode>`.
- [ ] Change a choice before Steam authorization completes (join and run `!hide all` at once). It is written after authorization and survives a reconnect.
- [ ] A new player (no cookie) gets `DefaultPreferences`. A cookie with an unknown version or broken value is ignored and does not throw.
- [ ] Run without the Cookies plugin: one warning in the log, the plugin works, nothing persists. Install Cookies and restart; saving starts.
- [ ] Update or reinstall Cookies without restarting. Confirm the documented behavior (a full restart is needed) and that HidePlayers logs the contract mismatch instead of breaking other plugins.
- [ ] Reload HidePlayers while Cookies stays loaded. Players keep their choices and nothing is written on unload.
- [ ] Take the database down for a moment. Hiding keeps working, errors are logged once per failed load or save, and no game-thread stall appears.
- [ ] Change maps, then reconnect. Saved choices return, and overrides from the API are gone.

## Commands and chat

- [ ] `Prefix` values for chat `!hide`, silent `/hide`, console `sw_hide` and the alias (`hidemodels`), in both chat and console. A bare chat command opens the menu, a bare console command toggles.
- [ ] The `[gold][HidePlayers][default]` prefix renders in colour in chat, including the leading bracket, and reads cleanly in the console reply.
- [ ] `command.usage` shows the invoked prefix and command name, and both English and Turkish clients get their own language.
- [ ] Set `MenuCommand.Permission`, then try a player with and without it from chat and console. Try `command_overrides.jsonc` entries for `hide` and `sw_hide`.
- [ ] Rename the command and aliases, reload the plugin, and confirm the old names are gone.
- [ ] Break `settings.jsonc` (bad mode, empty or duplicate command name, empty layout with `Panorama`). The plugin refuses to load and the log names the setting. Edit `Prefix` and `DefaultPreferences` and confirm they apply without a reload. Confirm missing keys are added to an old file.

## Framework menu (`Core.Menu`)

- [ ] Open `!hide` as a living player. The default centre-screen menu draws, its keys work (select, up/down, close), and the keys do not trigger other game actions.
- [ ] Select each mode and the corpse row. The selected mode is marked and the change applies immediately.
- [ ] With the menu open, change the choice by command, by an API override, and by a late Cookies load. The menu redraws within a tick without a click.
- [ ] Open the menu as a dead player and as a spectator.
- [ ] Set `"Renderer": "chat"` and different keys for `hideplayers.menu` in `configs/menus/menus.jsonc`. The menu follows them. An unregistered renderer id makes the menu fail to build; check what the log says.
- [ ] Disconnect, change the map and reload the plugin while the menu is open. No stale session, no exception.
- [ ] Open another plugin's menu, then `!hide`, and the reverse. Check which one replaces the other.
- [ ] Menu text for a Turkish client, and menu cost with many players open (use the profiler scopes).

## Panorama menu (client with the addon mounted)

- [ ] The layout loads. Check which name the engine accepts, `.xml` or `.vxml_c`, in the client console, and record it in `docs/custom-menu.md`. Confirm the XML comment before `<root>` is accepted by the custom HUD validator; remove it if it is not.
- [ ] `font-family: "Stratum2", "Arial"`, `horizontal-align: right` on the close button, and the `:hover` styles render as intended, and the two corpse buttons sit side by side with a gap only between them.
- [ ] Layout and style paths work with the namespaced `hideplayers` folders, in the compiled addon and in the Workshop preview (`VpkDirectories`).
- [ ] Two players open the UI independently. Selected buttons, labels and the override notice stay private to each player. Close and reopen.
- [ ] Close releases the mouse. Use every button, then close with the close button, and check no cursor is left behind.
- [ ] Reconnect with the menu open, change the map with it open, and unload the plugin with it open. The mouse is released, a replacement player in the same slot sees no menu, and no entity is left behind.
- [ ] Apply and clear an API override with the menu open. The notice and selection update.
- [ ] A click from a player who has not opened the menu does nothing. A click does not use the `sw_hide` command.
- [ ] Open `!hide` while another plugin's menu is open and check what happens to it.

## Shared API and providers

- [ ] From a second plugin: read preferences, set and clear an override, and check the effective result in game.
- [ ] Call the API from a background thread and confirm `InvalidOperationException`.
- [ ] Overrides are gone after the player disconnects and after a map change.
- [ ] Register a custom provider, open and close its HUD, then unload and reload it. Input capture is released, its override is cleared, and the built-in menu returns after unregistration. A second provider is rejected while the first is registered.
- [ ] Registering a provider closes any menu HidePlayers has open.

## Packaging and CI

- [ ] `build.yml` passes on a pull request. A zip containing `SwiftlyS2*.dll`, `Microsoft.Extensions*.dll` or `Cookies.Contract.dll` fails it.
- [ ] A test tag run of `release.yml` uploads `build/HidePlayers.zip`. A tag that differs from `[PluginMetadata] Version` is rejected.
- [ ] Extract the zip into a clean server. `HidePlayers/resources/exports/HidePlayers.Contract.dll` is present, and the plugin loads next to the Cookies plugin with no duplicate contract.
