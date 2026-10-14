using HidePlayers.Contract;

namespace HidePlayers;

// The operations behind IHidePlayersApi. HidePlayersApi checks the thread, then calls these on the game thread.
public sealed partial class HidePlayers
{
    internal bool OnGameThread => Core.IsGameThread;

    internal HidePreferences? PreferencesOf(int playerId) =>
        _sessions.TryGetValue(playerId, out var session) ? session.Preferences : null;

    internal HidePreferences? EffectivePreferencesOf(int playerId) =>
        _sessions.TryGetValue(playerId, out var session) ? session.EffectivePreferences : null;

    internal bool ApplyOverride(int playerId, HideOverride? value)
    {
        if (!_sessions.TryGetValue(playerId, out var session))
        {
            return false;
        }

        session.Override = value;
        MarkVisibilityDirty();
        if (Core.PlayerManager.GetPlayer(playerId) is { } player)
        {
            RefreshMenus(player);
        }

        return true;
    }

    internal bool AddMenuProvider(IHidePlayersMenuProvider provider)
    {
        if (!_active || (_menuProvider is not null && !ReferenceEquals(_menuProvider, provider)))
        {
            return false;
        }

        _menuProvider = provider;
        CloseMenus();
        return true;
    }

    internal bool RemoveMenuProvider(IHidePlayersMenuProvider provider)
    {
        if (!ReferenceEquals(_menuProvider, provider))
        {
            return false;
        }

        _menuProvider = null;
        return true;
    }
}
