# Custom menu and shared API integration

## Enable the bundled Panorama menu

HidePlayers includes editable [layout XML](../resources/panorama-addon/panorama/layout/custom_game/hideplayers.xml) and [stylesheet CSS](../resources/panorama-addon/panorama/styles/custom_game/hideplayers.css). Configure `Main.MenuRenderer` as `Panorama` and `Main.PanoramaLayout` as `panorama/layout/custom_game/hideplayers.vxml_c` after compiling and distributing these resources. `Framework` remains the default. A registered `IHidePlayersMenuProvider` takes priority.

The plugin creates a native `CCSCustomHudLayout` when the menu is first needed on a map. Labels, selected classes, visibility, and mouse capture are set separately for each player. Close collapses the panel and releases input. Button selections dispatch the configured `sw_<MenuCommand.Name>` command through normal permissions and persistence.

### Compile and distribute the game resources

The archive bundles sources only. Installing its XML/CSS under the server plugin folder does not mount or deliver them to clients. Use an addon you own:

1. Create or open your addon in CS2 Workshop Tools. Copy the shipped layout into `content/csgo_addons/<your-addon>/panorama/layout/custom_game/hideplayers.xml` and the stylesheet into `panorama/styles/custom_game/hideplayers.css` under the same addon. Keep their contents and resource paths intact. Compile these source filenames directly; `.xml` becomes `.vxml_c` and `.css` becomes `.vcss_c`, as documented by [Source2Toolkit authoring](https://www.source2toolkit.net/docs/panorama/authoring) and the [resource type guide](https://github.com/Wend4r/s2r-skills/blob/main/resource-compiler/references/resources.md).
2. Compile with CS2's Workshop Tools/ResourceCompiler. Confirm the game addon contains `panorama/layout/custom_game/hideplayers.vxml_c` and `panorama/styles/custom_game/hideplayers.vcss_c`.
3. Publish or update your addon through Workshop Manager. Its contents preview must include both compiled resource types. If these directories are absent from the preview, include `panorama/layout/custom_game` and `panorama/styles/custom_game` in `AddonConfig/VpkDirectories` in the Workshop Tools installation's `game/csgo/gameinfo.gi`. The [probe development guide](https://github.com/nicedayzhu/CustomHudProbeSW2/blob/main/docs/DEVELOPMENT.md) documents this packaging step.
4. Install [SwiftlyS2 AddonsManager](https://github.com/SwiftlyS2-Plugins/AddonsManager) and add your published Workshop ID to `Main.Addons` in its `configs/plugins/AddonsManager/config.jsonc`, preserving existing IDs. Restart/reload AddonsManager. Use `sw_downloadaddon <your-workshop-id>` to request a download and `sw_searchpath` to confirm the mount. AddonsManager mounts the addon on the server and sends configured addons to joining clients.
5. Set HidePlayers' renderer to `Panorama`, restart the plugin, and join with a client that has received the addon. Run `!hide`, choose each setting, close, and reopen.

For an existing addon, preserve its `addoninfo.txt` and Panorama preprocessor configuration. The probe's resource build supplies `Panorama.AllowCustomGameUI = true` and `Panorama.AddonLayoutPath = "panorama/layout/custom_game/"` in addon metadata. For a new addon without `panorama/preprocessor_config.txt`, use this minimal KeyValues content:

```text
"PanzipCfg"
{
    "BlockDefs"
    {
    }
}
```

Compile CSS before XML from the compiler's own directory, checking each exit code and the corresponding output before continuing. Substitute your installation and addon paths in this PowerShell example; the [compiler guide](https://github.com/Wend4r/s2r-skills/blob/main/resource-compiler/references/compile.md) explains this sequence.

```powershell
$cs2Root = 'C:\Steam\steamapps\common\Counter-Strike Global Offensive'
$addon = 'your-addon'
$source = "$cs2Root\content\csgo_addons\$addon\panorama"
$output = "$cs2Root\game\csgo_addons\$addon\panorama"
Push-Location "$cs2Root\game\bin\win64"
try {
    & .\resourcecompiler.exe -i "$source\styles\custom_game\hideplayers.css"
    if ($LASTEXITCODE -ne 0) { throw 'Stylesheet compilation failed' }
    Get-Item "$output\styles\custom_game\hideplayers.vcss_c" -ErrorAction Stop

    & .\resourcecompiler.exe -i "$source\layout\custom_game\hideplayers.xml"
    if ($LASTEXITCODE -ne 0) { throw 'Layout compilation failed' }
    Get-Item "$output\layout\custom_game\hideplayers.vxml_c" -ErrorAction Stop
}
finally { Pop-Location }
```

Read the compiler summary and confirm new outputs' sizes and timestamps. A successful compile does not validate CSS values in the client; check the mounted layout in game.

The [SwiftlyS2 Custom HUD guide](https://swiftlys2.net/docs/development/custom-hud) illustrates an `.xml` layout path; this bundled configuration uses the compiled `.vxml_c` resource name produced by the addon workflow above. Changing `PanoramaLayout` requires your mounted addon to contain the matching resource. No HidePlayers Workshop ID or compiled VPK is provided by this repository.

### Editing the bundled layout

Change placement and styles while preserving the elements the renderer addresses:

| Element | Required ID or class |
| --- | --- |
| Outermost wrapper | No `id`; class `HidePlayersScreen`, full-screen, `hittest="false"`, `z-index: 99999` |
| Inner menu root | `hideplayers_root`, initially class `Dismissed` |
| Title and override notice | `hideplayers_title`, `hideplayers_override` |
| Mode buttons | `hideplayers_off`, `hideplayers_all`, `hideplayers_team`, `hideplayers_enemy` |
| Corpse section label | `hideplayers_dead_label` |
| Corpse buttons | `hideplayers_dead_on`, `hideplayers_dead_off` |
| Close button | `hideplayers_close` |
| Button labels | Each button ID followed by `_label`, including `hideplayers_close_label` |
| Dynamic label text | `text="{s:text}"` |
| Hidden/selected state | `Dismissed` collapses visibility; `Selected` highlights the chosen button |

Keep an anonymous outer wrapper: the loader assigns its root ID. Put server-addressed IDs on inner panels. Use `hittest="false"` on labels and decorative panels; buttons accept only `id` and `class`. These constraints follow the [Custom HUD layout guide](https://github.com/Wend4r/s2r-skills/blob/main/custom-hud-layout/SKILL.md).

The native click event handles button IDs. Scripts and `onactivate` handlers are not supported by this Custom HUD layout route; keep the XML declarative. To use a different set of IDs or behavior, register a provider and handle its owned layout's events as described below.

## Consume the shared API

A consumer plugin can replace the menu opened by a bare chat hide command, or temporarily override visibility for an activity. Reference `HidePlayers.Contract.dll` from the installed provider's `resources/exports/` directory and use `HidePlayers.Contract`; do not copy a second contract DLL into the consumer's deployed folder. Both plugins must use the same shared contract assembly.

For a direct assembly reference, disable copying:

```xml
<Reference Include="HidePlayers.Contract">
  <HintPath>path/to/HidePlayers/resources/exports/HidePlayers.Contract.dll</HintPath>
  <Private>false</Private>
</Reference>
```

See SwiftlyS2's [shared API lifecycle](https://swiftlys2.net/docs/development/shared-api). Retrieve the API in `UseSharedInterface`, which can run again when the plugin set changes:

```csharp
private IHidePlayersApi? _hideApi;

public override void UseSharedInterface(IInterfaceManager interfaces)
{
    _hideApi = interfaces.GetSharedInterface<IHidePlayersApi>(
        IHidePlayersApi.InterfaceKey); // "HidePlayers.API.v1"
}
```

For an optional dependency, use `TryGetSharedInterface` instead. Release registrations and overrides on the previous API before replacing a retained reference during reinjection.

## Preferences and temporary overrides

All API calls run on the game thread and identify players by SwiftlyS2 player slot. Schedule background work onto that thread before accessing players, entities, or this API.

| Method | Result |
| --- | --- |
| `GetPreferences(slot)` | Personal choices, or `null` if the player is absent |
| `GetEffectivePreferences(slot)` | Personal choices with the current override applied, or `null` |
| `SetOverride(slot, value)` | Replace the session override; return `false` if the player is absent |
| `ClearOverride(slot)` | Restore the latest personal choices; return `false` if the player is absent |
| `RegisterMenuProvider(provider)` | Register one provider; reject a different provider while one is registered |
| `UnregisterMenuProvider(provider)` | Remove only that same provider instance |

`HidePreferences` contains `Mode` and `HideDead`. A `HideOverride` uses nullable fields: an omitted field continues to follow the player's personal choice. Overrides are temporary and never written to the database.

```csharp
// Hide living enemies while leaving the player's corpse choice in effect.
_hideApi!.SetOverride(slot, new HideOverride(Mode: HideMode.Enemies));

// End the activity and restore the player's latest choices.
_hideApi.ClearOverride(slot);
```

Each `SetOverride` replaces the previous override; overrides do not stack or have consumer ownership tokens. Coordinate activities that could overlap. Track the player session alongside each slot you override, and clear your overrides when the activity ends or your plugin unloads without affecting a replacement player in a reused slot.

## Registering a provider

Implement `IHidePlayersMenuProvider.OpenMenu(int playerId)` on the instance that owns the HUD. After resolving the API, register that instance and check the returned value. Use the same instance to unregister on unload. The provider is called only for bare chat commands. A bare player-console command toggles player hiding between Off and the last selected mode. Command arguments still use HidePlayers' normal permission checks and preference persistence.

The examples below are methods and fields inside a provider plugin that derives from `BasePlugin` and implements `IHidePlayersMenuProvider`. They use `SwiftlyS2.Shared.Events` and `SwiftlyS2.Shared.SchemaDefinitions`, alongside the contract and plugin namespaces.

## Panorama buttons to server commands

Deliver the XML/CSS as Panorama game resources available to clients. Putting them in a plugin's resources folder alone does not deliver them to the game. This follows SwiftlyS2's [Custom HUD API](https://swiftlys2.net/docs/development/custom-hud).

This separate provider example uses its own `my_hide_menu.vxml_c` layout and button IDs. A button's `id` is sent through `Core.Event.OnCustomHudClicked`; no JavaScript command bridge is needed:

```xml
<root>
  <styles>
    <include src="s2r://panorama/styles/custom_game/my_hide_menu.vcss_c" />
  </styles>
  <Panel class="ProviderScreen" hittest="false">
    <Panel id="hide_menu" class="Dismissed" hittest="false">
      <Button id="hide_all"><Label text="Hide all" hittest="false" /></Button>
      <Button id="hide_team"><Label text="Hide teammates" hittest="false" /></Button>
      <Button id="hide_enemy"><Label text="Hide enemies" hittest="false" /></Button>
      <Button id="hide_off"><Label text="Show living players" hittest="false" /></Button>
      <Button id="hide_dead_toggle"><Label text="Toggle corpses" hittest="false" /></Button>
      <Button id="hide_close"><Label text="Close" hittest="false" /></Button>
    </Panel>
  </Panel>
</root>
```

The provider's stylesheet must size `.ProviderScreen` to the full screen and set its `z-index: 99999`. Define `.Dismissed { visibility: collapse; }` and toggle that class on `hide_menu` separately for each player when opening or closing.

Create the entity, set the layout path, notify the engine, then spawn it:

```csharp
private CCSCustomHudLayout? _hud;
private readonly HashSet<int> _openSlots = [];

private void CreateHud()
{
    _hud = Core.EntitySystem.CreateEntity<CCSCustomHudLayout>();
    _hud.StrLayout = "panorama/layout/custom_game/my_hide_menu.vxml_c";
    _hud.StrLayoutUpdated();
    _hud.DispatchSpawn();
}

public void OpenMenu(int playerId)
{
    _openSlots.Add(playerId);
    _hud!.SetHasClassForPlayer(playerId, "hide_menu", "Dismissed",
        EHudPanelClassStatus_t.k_eHudPanelClassStatus_DoesNotHaveClass);
    _hud!.SetInputCaptureEnabledForPlayer(playerId, true);
}

private void CloseMenu(int playerId)
{
    _hud!.SetHasClassForPlayer(playerId, "hide_menu", "Dismissed",
        EHudPanelClassStatus_t.k_eHudPanelClassStatus_HasClass);
    _hud!.SetInputCaptureEnabledForPlayer(playerId, false);
    _openSlots.Remove(playerId);
}
```

Subscribe once in `Load` and unsubscribe in `Unload`. The event covers every custom HUD, so compare the native entity handle, including its serial, against the layout your provider owns. Then map only fixed button IDs:

```csharp
// In Load:
// Core.Event.OnCustomHudClicked += OnHudClicked;

private void OnHudClicked(IOnCustomHudClickedEvent e)
{
    if (_hud is not { IsValid: true }
        || e.CustomHudLayout.Entity!.EntityHandle.Raw != _hud.Entity!.EntityHandle.Raw
        || !_openSlots.Contains(e.PlayerId))
        return;

    if (e.ButtonId == "hide_close")
    {
        CloseMenu(e.PlayerId);
        return;
    }

    var argument = e.ButtonId switch
    {
        "hide_all" => "all",
        "hide_team" => "team",
        "hide_enemy" => "enemy",
        "hide_off" => "off",
        "hide_dead_toggle" => "dead toggle",
        _ => null
    };
    if (argument is null)
        return;

    var player = Core.PlayerManager.GetPlayer(e.PlayerId);
    player?.ExecuteCommand($"sw_hide {argument}");
}
```

The default console command is `sw_hide`. If the server changes `Main.MenuCommand.Name`, configure your provider to use the matching `sw_<name>` command. Dispatching through `player.ExecuteCommand` keeps the command's configured permission checks and database save behavior. Do not use temporary overrides to implement personal menu choices.

On close and disconnect, release that player's input capture and panel state. On map changes, release the old layout state and create a new layout when needed. On unload, unsubscribe the click event, close all open player menus, despawn your valid HUD, unregister the same provider instance, and clear overrides for the sessions your plugin still owns. HidePlayers resumes its configured Framework/Panorama menu once the provider is unregistered.
