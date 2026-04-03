using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;

namespace HidePlayers;

public sealed partial class Plugin
{
    [EventListener<EventDelegates.OnClientPutInServer>]
    public void OnClientPutInServer(IOnClientPutInServerEvent @event)
    {
        _visibility.InitPlayer(@event.PlayerId);
        _visibility.ScheduleRecalculation(_mode);
    }

    [EventListener<EventDelegates.OnClientDisconnected>]
    public void OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        _visibility.RemovePlayer(@event.PlayerId);
        _visibility.ScheduleRecalculation(_mode);
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event)
    {
        _visibility.ScheduleRecalculation(_mode);
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerDeath(EventPlayerDeath @event)
    {
        _visibility.ScheduleRecalculation(_mode);
        return HookResult.Continue;
    }
}
