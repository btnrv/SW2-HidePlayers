using HidePlayers.Contract;

namespace HidePlayers.Runtime;

public sealed partial class HidePlayersRuntime
{
    public HidePreferences? GetPreferences(int playerId) => Session(playerId)?.Preferences;
    public HidePreferences? GetEffectivePreferences(int playerId) => Session(playerId)?.EffectivePreferences;

    public bool SetOverride(int playerId, HideOverride value)
    {
        if (value.Mode is { } mode && !Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(value));
        if (Session(playerId) is null || !_sessions.SetOverride(playerId, value))
            return false;
        RefreshViewer(_players[playerId]!);
        return true;
    }

    public bool ClearOverride(int playerId)
    {
        if (Session(playerId) is null || !_sessions.ClearOverride(playerId))
            return false;
        RefreshViewer(_players[playerId]!);
        return true;
    }

    public bool RegisterMenuProvider(IHidePlayersMenuProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (!_running || (_menuProvider is not null && !ReferenceEquals(_menuProvider, provider)))
            return false;
        _menuProvider = provider;
        CloseMenus();
        return true;
    }

    public bool UnregisterMenuProvider(IHidePlayersMenuProvider provider)
    {
        if (!ReferenceEquals(_menuProvider, provider))
            return false;
        _menuProvider = null;
        return true;
    }
}
