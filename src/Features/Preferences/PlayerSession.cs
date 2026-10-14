using HidePlayers.Contract;

namespace HidePlayers;

/// <summary>One human connection's choices, keyed by slot while the connection lasts.</summary>
internal sealed class PlayerSession(ulong sessionId, HidePreferences defaults)
{
    public ulong SessionId { get; } = sessionId;

    public HidePreferences Preferences { get; private set; } = defaults;

    /// <summary>The last mode other than Off, so that toggling Off restores it.</summary>
    public HideMode LastActiveMode { get; private set; } = defaults.Mode == HideMode.Off ? HideMode.All : defaults.Mode;

    /// <summary>Counts the player's own changes. A stored value that was read before a change is dropped.</summary>
    public long Revision { get; private set; }

    public HideOverride? Override { get; set; }

    /// <summary>The Cookies store this session already started a read or a write for.</summary>
    public object? StartedWithStore { get; set; }

    public HidePreferences EffectivePreferences => Override?.Apply(Preferences) ?? Preferences;

    public HidePreferences ToggledPreferences => Preferences with
    {
        Mode = Preferences.Mode == HideMode.Off ? LastActiveMode : HideMode.Off
    };

    public void SetPreferences(HidePreferences preferences)
    {
        Preferences = preferences;
        if (preferences.Mode != HideMode.Off)
        {
            LastActiveMode = preferences.Mode;
        }

        Revision++;
    }

    public bool TryApplyStored(long expectedRevision, HidePreferences preferences, HideMode lastActiveMode)
    {
        if (Revision != expectedRevision)
        {
            return false;
        }

        Preferences = preferences;
        LastActiveMode = preferences.Mode == HideMode.Off ? lastActiveMode : preferences.Mode;
        return true;
    }
}
