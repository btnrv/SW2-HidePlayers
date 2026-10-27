using SwiftlyS2.Shared.Events;

namespace HidePlayers;

public sealed partial class HidePlayers
{
    [EventListener<EventDelegates.OnClientPutInServer>]
    private void OnClientPutInServer(IOnClientPutInServerEvent @event)
    {
        if (Core.PlayerManager.GetPlayer(@event.PlayerId) is not { } player)
        {
            return;
        }

        if (EnsureSession(player) is { } session)
        {
            BeginPreferenceLoad(player, session);
        }
    }

    [EventListener<EventDelegates.OnClientSteamAuthorize>]
    private void OnClientSteamAuthorize(IOnClientSteamAuthorizeEvent @event)
    {
        // Authorization can finish before the player is put in the server. The session does not exist yet then,
        // and OnClientPutInServer starts the load because the player is already authorized.
        if (Core.PlayerManager.GetPlayer(@event.PlayerId) is { } player && GetSession(player) is { } session)
        {
            BeginPreferenceLoad(player, session);
        }
    }

    [EventListener<EventDelegates.OnClientDisconnected>]
    private void OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        ForgetViewer(@event.PlayerId);
    }
}
