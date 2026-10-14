using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;

namespace HidePlayers;

public sealed partial class HidePlayers
{
    [GameEventHandler(HookMode.Post)]
    private HookResult OnPlayerSpawn(EventPlayerSpawn @event)
    {
        MarkVisibilityDirty();
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    private HookResult OnPlayerDeath(EventPlayerDeath @event)
    {
        MarkVisibilityDirty();
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    private HookResult OnPlayerTeam(EventPlayerTeam @event)
    {
        MarkVisibilityDirty();
        return HookResult.Continue;
    }

    // A spectator's observed pawn must stay visible, so a new observer target needs a new decision.
    [GameEventHandler(HookMode.Post)]
    private HookResult OnSpecTargetUpdated(EventSpecTargetUpdated @event)
    {
        MarkVisibilityDirty();
        return HookResult.Continue;
    }

    [EventListener<EventDelegates.OnMapLoad>]
    private void OnMapLoad(IOnMapLoadEvent @event)
    {
        MarkVisibilityDirty();
    }

    // The engine clears the blocked bit of every entity it deletes, so the plugin only drops its own records.
    [EventListener<EventDelegates.OnMapUnload>]
    private void OnMapUnload(IOnMapUnloadEvent @event)
    {
        _blocked.Clear();
        foreach (var session in _sessions.Values)
        {
            session.Override = null;
        }

        _visibilityDirty = false;
        ForgetPanoramaEntity();
    }
}
