using HidePlayers.Contract;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace HidePlayers;

public sealed partial class HidePlayers
{
    private readonly Dictionary<int, PlayerSession> _sessions = [];
    private CookiePreferenceStore? _store;
    private bool _cookiesWarningLogged;

    private HidePreferences DefaultPreferences()
    {
        var defaults = Settings.DefaultPreferences;
        return new HidePreferences(defaults.Mode, defaults.HideDead);
    }

    /// <summary>Returns the session of a connected human, creating it on first use. Bots have none.</summary>
    private PlayerSession? EnsureSession(IPlayer player)
    {
        if (player.IsFakeClient)
        {
            return null;
        }

        var slot = player.PlayerID;
        if (_sessions.TryGetValue(slot, out var existing))
        {
            if (existing.SessionId == player.SessionId)
            {
                return existing;
            }

            ForgetViewer(slot);
        }

        var session = new PlayerSession(player.SessionId, DefaultPreferences());
        _sessions[slot] = session;
        return session;
    }

    private PlayerSession? GetSession(IPlayer player) =>
        _sessions.TryGetValue(player.PlayerID, out var session) && session.SessionId == player.SessionId ? session : null;

    private void SeedSessions()
    {
        foreach (var player in Core.PlayerManager.GetAllPlayers())
        {
            EnsureSession(player);
        }

        MarkVisibilityDirty();
    }

    private void ForgetViewer(int slot)
    {
        ClosePanoramaMenu(slot);
        _sessions.Remove(slot);
        // The engine clears a viewer's blocked entities on disconnect, so there is nothing to unblock.
        _blocked.Remove(slot);
    }

    private void ClearPlayerState()
    {
        _sessions.Clear();
        _blocked.Clear();
        _panoramaOpen.Clear();
    }

    private void ChangePreferences(IPlayer player, HidePreferences preferences)
    {
        if (GetSession(player) is not { } session)
        {
            return;
        }

        session.SetPreferences(preferences);
        MarkVisibilityDirty();
        RefreshMenus(player);
        if (_store is { } store && player.IsAuthorized)
        {
            WritePreferences(store, (long)player.SteamID, session);
        }
    }

    // Cookies is resolved here and not in Load, because the registry is rebuilt every time the plugin set changes.
    private void UseCookies(IInterfaceManager interfaceManager)
    {
        if (!interfaceManager.HasSharedInterface(CookiePreferenceStore.InterfaceKey))
        {
            _store = null;
            if (!_cookiesWarningLogged)
            {
                _cookiesWarningLogged = true;
                Core.Logger.LogWarning("The Cookies plugin is not loaded. HidePlayers keeps preferences for this connection only.");
            }

            return;
        }

        // An exception that escapes this callback stops shared-interface injection for every plugin.
        try
        {
            _store = CookiePreferenceStore.Resolve(interfaceManager, _store);
        }
        catch (Exception exception) when (exception is InvalidCastException or FileNotFoundException or TypeLoadException)
        {
            _store = null;
            Core.Logger.LogError(exception, "Cookies contract mismatch; restart the server.");
        }
    }

    private void BeginPreferenceLoads()
    {
        foreach (var (slot, session) in _sessions)
        {
            if (Core.PlayerManager.GetPlayer(slot) is { } player && player.SessionId == session.SessionId)
            {
                BeginPreferenceLoad(player, session);
            }
        }
    }

    /// <summary>Starts the first read or write against the current store. Repeated calls change nothing.</summary>
    private void BeginPreferenceLoad(IPlayer player, PlayerSession session)
    {
        if (_store is not { } store || !player.IsAuthorized || ReferenceEquals(session.StartedWithStore, store))
        {
            return;
        }

        session.StartedWithStore = store;
        var steamId = (long)player.SteamID;
        if (session.Revision != 0)
        {
            // The player chose before authorization finished, so their choice replaces the stored one.
            WritePreferences(store, steamId, session);
            return;
        }

        var slot = player.PlayerID;
        var sessionId = session.SessionId;
        var revision = session.Revision;
        _ = Task.Run(() => ReadPreferences(store, slot, sessionId, revision, steamId));
    }

    private void ReadPreferences(CookiePreferenceStore store, int slot, ulong sessionId, long revision, long steamId)
    {
        string? stored;
        try
        {
            stored = store.TryRead(steamId);
        }
        catch (Exception exception)
        {
            Core.Logger.LogError(exception, "Could not read the HidePlayers preferences of {SteamId}", steamId);
            return;
        }

        Core.Scheduler.NextTick(() => ApplyStoredPreferences(slot, sessionId, revision, stored));
    }

    private void ApplyStoredPreferences(int slot, ulong sessionId, long revision, string? stored)
    {
        if (!PreferenceCodec.TryParse(stored, out var preferences, out var lastActiveMode)
            || !_sessions.TryGetValue(slot, out var session)
            || session.SessionId != sessionId
            || !session.TryApplyStored(revision, preferences, lastActiveMode)
            || Core.PlayerManager.GetPlayerFromSessionId(sessionId) is not { } player)
        {
            return;
        }

        MarkVisibilityDirty();
        RefreshMenus(player);
    }

    private void WritePreferences(CookiePreferenceStore store, long steamId, PlayerSession session)
    {
        try
        {
            store.Write(steamId, PreferenceCodec.Encode(session.Preferences, session.LastActiveMode));
        }
        catch (Exception exception)
        {
            Core.Logger.LogError(exception, "Could not save the HidePlayers preferences of {SteamId}", steamId);
        }
    }
}
