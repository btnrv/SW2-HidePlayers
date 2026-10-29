namespace HidePlayers.Contract;

/// <summary>Which living players a viewer hides.</summary>
public enum HideMode
{
    Off,
    All,
    Teammates,
    Enemies
}

/// <summary>A viewer's personal choices: which living players to hide and whether to hide corpses.</summary>
public readonly record struct HidePreferences(HideMode Mode, bool HideDead);

/// <summary>Temporary values that replace a viewer's personal choices. A null member keeps the personal value.</summary>
public readonly record struct HideOverride(HideMode? Mode = null, bool? HideDead = null)
{
    public HidePreferences Apply(HidePreferences preferences) =>
        new(Mode ?? preferences.Mode, HideDead ?? preferences.HideDead);
}

/// <summary>
/// Shared API of the HidePlayers plugin, resolved with <see cref="InterfaceKey"/>.
/// </summary>
/// <remarks>
/// Every member must be called on the game thread; the implementation throws <see cref="InvalidOperationException"/> otherwise.
/// <c>playerId</c> is the SwiftlyS2 slot of a connected human player, never a SteamID or entity index.
/// Overrides exist for the current connection only: they are never saved, and HidePlayers clears them when the player
/// disconnects and when the map unloads. A plugin that needs an override on the next map applies it again after the map loads.
/// HidePlayers rebuilds its state when it reloads, so consumers resolve the interface again in <c>UseSharedInterface</c> and
/// apply their overrides again.
/// </remarks>
public interface IHidePlayersApi
{
    const string InterfaceKey = "HidePlayers.API.v1";

    /// <summary>Returns the player's personal choices, or null when no human player occupies the slot.</summary>
    HidePreferences? GetPreferences(int playerId);

    /// <summary>Returns the choices in effect, personal choices with any override applied, or null for an absent player.</summary>
    HidePreferences? GetEffectivePreferences(int playerId);

    /// <summary>Replaces this connection's override without saving it. Returns false for an absent player.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> holds an undefined <see cref="HideMode"/>.</exception>
    bool SetOverride(int playerId, HideOverride value);

    /// <summary>Restores the player's personal choices. Consumers call this when their activity ends.</summary>
    bool ClearOverride(int playerId);

    /// <summary>
    /// Legacy registration retained for compatibility with existing consumers. Only one provider can be registered.
    /// Commands no longer invoke providers: hide toggles preferences and hidemenu always opens the native menu.
    /// </summary>
    bool RegisterMenuProvider(IHidePlayersMenuProvider provider);

    /// <summary>Unregisters this provider. The owning plugin must call this in its <c>Unload</c>.</summary>
    bool UnregisterMenuProvider(IHidePlayersMenuProvider provider);
}

/// <summary>Legacy menu-provider contract. Current HidePlayers commands do not invoke custom providers.</summary>
public interface IHidePlayersMenuProvider
{
    /// <summary>Opens the menu for the human player in this slot. Called on the game thread.</summary>
    void OpenMenu(int playerId);
}
