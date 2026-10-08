using HidePlayers.Contract;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Players;

namespace HidePlayers.Runtime;

public sealed partial class HidePlayersRuntime
{
    private void ChangePreferences(IPlayer player, HidePreferences preferences)
    {
        if (Session(player.PlayerID) is not { } session)
            return;
        _sessions.SetPreferences(player.PlayerID, preferences);
        RefreshViewer(player);
        if (player.IsAuthorized)
            _ = SavePreferencesAsync(player.SteamID, preferences, session.LastActiveMode);
    }

    private void BeginPreferenceLoad(IPlayer player)
    {
        if (!player.IsAuthorized || !_startedLoads.Add(player.SessionId))
            return;
        var session = _sessions.Get(player.PlayerID)!;
        if (session.Revision != 0)
            _ = SavePreferencesAsync(player.SteamID, session.Preferences, session.LastActiveMode);
        else
            _ = LoadPreferencesAsync(player.PlayerID, player.SessionId, session.Revision, player.SteamID);
    }

    private async Task InitializeStorageAsync()
    {
        try { await _store.InitializeAsync(); }
        catch (Exception exception) { _logger.LogError(exception, "Could not initialize HidePlayers preferences"); }
    }

    private async Task LoadPreferencesAsync(int slot, ulong sessionId, long revision, ulong steamId)
    {
        try
        {
            var preferences = await _store.LoadAsync(steamId);
            if (preferences is not { } saved || !_running)
                return;
            await _core.Scheduler.NextTickAsync(() =>
            {
                if (_running && _sessions.TryLoad(slot, sessionId, revision, saved.Preferences, saved.LastActiveMode)
                    && _players[slot] is { } player)
                    RefreshViewer(player);
            });
        }
        catch (Exception exception) { _logger.LogError(exception, "Could not load preferences for {SteamId}", steamId); }
    }

    private async Task SavePreferencesAsync(ulong steamId, HidePreferences preferences, HideMode lastActiveMode)
    {
        try { await _store.SaveAsync(steamId, preferences, lastActiveMode); }
        catch (Exception exception) { _logger.LogError(exception, "Could not save preferences for {SteamId}", steamId); }
    }
}
