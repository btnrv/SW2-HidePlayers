using HidePlayers.Contract;

namespace HidePlayers.State;

public sealed class PlayerSession(ulong sessionId, HidePreferences defaults)
{
    public ulong SessionId { get; } = sessionId;
    public HidePreferences Preferences { get; internal set; } = defaults;
    public HideMode LastActiveMode { get; internal set; } = defaults.Mode == HideMode.Off ? HideMode.All : defaults.Mode;
    public HidePreferences ToggledPreferences => Preferences with
    {
        Mode = Preferences.Mode == HideMode.Off ? LastActiveMode : HideMode.Off
    };
    public long Revision { get; internal set; }
    internal HideOverride? Override { get; set; }
    public HidePreferences EffectivePreferences => Override?.Apply(Preferences) ?? Preferences;
}

public sealed class PlayerSessions(int capacity)
{
    private readonly PlayerSession?[] sessions = new PlayerSession?[capacity];

    public PlayerSession Connect(int slot, ulong sessionId, HidePreferences defaults)
    {
        var session = new PlayerSession(sessionId, defaults);
        sessions[slot] = session;
        return session;
    }

    public PlayerSession? Get(int slot) => sessions[slot];

    public void Disconnect(int slot) => sessions[slot] = null;

    public bool SetPreferences(int slot, HidePreferences preferences)
    {
        if (sessions[slot] is not { } session)
            return false;
        session.Preferences = preferences;
        if (preferences.Mode != HideMode.Off)
            session.LastActiveMode = preferences.Mode;
        session.Revision++;
        return true;
    }

    public bool TryLoad(int slot, ulong sessionId, long expectedRevision, HidePreferences preferences,
        HideMode lastActiveMode = HideMode.All)
    {
        if (sessions[slot] is not { } session || session.SessionId != sessionId || session.Revision != expectedRevision)
            return false;
        session.Preferences = preferences;
        session.LastActiveMode = preferences.Mode == HideMode.Off ? lastActiveMode : preferences.Mode;
        return true;
    }

    public bool SetOverride(int slot, HideOverride value)
    {
        if (sessions[slot] is not { } session)
            return false;
        session.Override = value;
        return true;
    }

    public bool ClearOverride(int slot)
    {
        if (sessions[slot] is not { } session)
            return false;
        session.Override = null;
        return true;
    }

    public void ClearOverrides()
    {
        foreach (var session in sessions)
            if (session is not null)
                session.Override = null;
    }

    public void Clear() => Array.Clear(sessions);
}
