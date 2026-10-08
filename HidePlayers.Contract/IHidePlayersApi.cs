using System.Text.Json.Serialization;

namespace HidePlayers.Contract;

[JsonConverter(typeof(JsonStringEnumConverter<HideMode>))]
public enum HideMode
{
    Off,
    All,
    Teammates,
    Enemies
}

[method: JsonConstructor]
public readonly record struct HidePreferences(HideMode Mode = HideMode.Off, bool HideDead = true)
{
    public HidePreferences() : this(HideMode.Off, true) { }
}

public readonly record struct HideOverride(HideMode? Mode = null, bool? HideDead = null)
{
    public HidePreferences Apply(HidePreferences preferences) =>
        new(Mode ?? preferences.Mode, HideDead ?? preferences.HideDead);
}

/// <summary>Main-thread API for connected players, addressed by their SwiftlyS2 player slot.</summary>
public interface IHidePlayersApi
{
    const string InterfaceKey = "HidePlayers.API.v1";

    HidePreferences? GetPreferences(int playerId);
    HidePreferences? GetEffectivePreferences(int playerId);

    /// <summary>Replaces this session's override without persisting it. Returns false for absent players.</summary>
    bool SetOverride(int playerId, HideOverride value);

    /// <summary>Restores the player's latest personal choices. Consumers call this when their activity ends.</summary>
    bool ClearOverride(int playerId);

    /// <summary>Registers the menu opened by bare chat hide commands. Only one provider can be registered.</summary>
    bool RegisterMenuProvider(IHidePlayersMenuProvider provider);

    /// <summary>Unregisters this provider. The owning plugin must call this when unloading.</summary>
    bool UnregisterMenuProvider(IHidePlayersMenuProvider provider);
}

/// <summary>Allows a plugin to supply its own menu, including a SwiftlyS2 Panorama custom HUD.</summary>
public interface IHidePlayersMenuProvider
{
    void OpenMenu(int playerId);
}
