using HidePlayers.Contract;

namespace HidePlayers;

/// <summary>The object that other plugins receive. It checks the thread and forwards to the plugin.</summary>
internal sealed class HidePlayersApi(HidePlayers plugin) : IHidePlayersApi
{
    public HidePreferences? GetPreferences(int playerId)
    {
        EnsureGameThread();
        return plugin.PreferencesOf(playerId);
    }

    public HidePreferences? GetEffectivePreferences(int playerId)
    {
        EnsureGameThread();
        return plugin.EffectivePreferencesOf(playerId);
    }

    public bool SetOverride(int playerId, HideOverride value)
    {
        EnsureGameThread();
        if (value.Mode is { } mode && !Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "HideOverride.Mode is not a defined HideMode.");
        }

        return plugin.ApplyOverride(playerId, value);
    }

    public bool ClearOverride(int playerId)
    {
        EnsureGameThread();
        return plugin.ApplyOverride(playerId, null);
    }

    public bool RegisterMenuProvider(IHidePlayersMenuProvider provider)
    {
        EnsureGameThread();
        ArgumentNullException.ThrowIfNull(provider);
        return plugin.AddMenuProvider(provider);
    }

    public bool UnregisterMenuProvider(IHidePlayersMenuProvider provider)
    {
        EnsureGameThread();
        return plugin.RemoveMenuProvider(provider);
    }

    private void EnsureGameThread()
    {
        if (!plugin.OnGameThread)
        {
            throw new InvalidOperationException("The HidePlayers API must be called on the game thread.");
        }
    }
}
