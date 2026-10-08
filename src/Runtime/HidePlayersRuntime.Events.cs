using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;

namespace HidePlayers.Runtime;

public sealed partial class HidePlayersRuntime
{
    private readonly List<Guid> _eventHooks = [];
    private readonly HashSet<int> _pendingPlayers = [];
    private bool _refreshScheduled;

    private void SubscribeEvents()
    {
        _core.Event.OnClientPutInServer += OnJoin;
        _core.Event.OnClientDisconnected += OnDisconnect;
        _core.Event.OnClientSteamAuthorize += OnAuthorize;
        _core.Event.OnEntityDeleted += OnEntityDeleted;
        _core.Event.OnMapLoad += OnMapLoad;
        _core.Event.OnMapUnload += OnMapUnload;
        _core.Event.OnCustomHudClicked += OnPanoramaClicked;
        _eventHooks.Add(_core.GameEvent.HookPost<EventPlayerSpawn>(OnSpawn));
        _eventHooks.Add(_core.GameEvent.HookPost<EventPlayerDeath>(OnDeath));
        _eventHooks.Add(_core.GameEvent.HookPost<EventPlayerTeam>(OnTeam));
    }

    private void UnsubscribeEvents()
    {
        _core.Event.OnClientPutInServer -= OnJoin;
        _core.Event.OnClientDisconnected -= OnDisconnect;
        _core.Event.OnClientSteamAuthorize -= OnAuthorize;
        _core.Event.OnEntityDeleted -= OnEntityDeleted;
        _core.Event.OnMapLoad -= OnMapLoad;
        _core.Event.OnMapUnload -= OnMapUnload;
        _core.Event.OnCustomHudClicked -= OnPanoramaClicked;
        foreach (var hook in _eventHooks)
            _core.GameEvent.Unhook(hook);
        _eventHooks.Clear();
    }

    private void OnJoin(IOnClientPutInServerEvent @event) => SchedulePlayer(@event.PlayerId);
    private void OnAuthorize(IOnClientSteamAuthorizeEvent @event) => SchedulePlayer(@event.PlayerId);
    private void OnDisconnect(IOnClientDisconnectedEvent @event) => RemovePlayer(@event.PlayerId);

    private HookResult OnSpawn(EventPlayerSpawn @event)
    {
        if (@event.UserIdPlayer is { } player)
            SchedulePlayer(player.PlayerID);
        return HookResult.Continue;
    }

    private HookResult OnTeam(EventPlayerTeam @event)
    {
        if (@event.UserIdPlayer is { } player)
            SchedulePlayer(player.PlayerID);
        return HookResult.Continue;
    }

    private HookResult OnDeath(EventPlayerDeath @event)
    {
        var player = @event.UserIdPlayer;
        if (@event.UserIdPawn is { IsValid: true } pawn)
            TrackPawn(pawn, player?.PlayerID ?? -1);
        if (player is not null)
            RefreshViewer(player);
        // Manual test: observe a living player who dies with Hide dead enabled.
        // Check observer transitions and client stability before adding any special handling.
        return HookResult.Continue;
    }

    private void OnEntityDeleted(IOnEntityDeletedEvent @event)
    {
        if (@event.Entity.Entity is { } identity)
            RemovePawn(identity.EntityHandle.Raw);
    }

    private void OnMapUnload(IOnMapUnloadEvent @event)
    {
        DisposeMenus();
        _panoramaHud = null;
        _blocks.Clear();
        _pawns.Clear();
        Array.Clear(_livePawns);
        _sessions.ClearOverrides();
        _pendingPlayers.Clear();
    }

    private void OnMapLoad(IOnMapLoadEvent @event) => _core.Scheduler.NextTick(() =>
    {
        if (_running)
            SeedPlayersAndPawns();
    });

    private void SchedulePlayer(int slot)
    {
        _pendingPlayers.Add(slot);
        if (_refreshScheduled)
            return;
        _refreshScheduled = true;
        _core.Scheduler.NextTick(() =>
        {
            _refreshScheduled = false;
            if (!_running)
                return;
            foreach (var playerId in _pendingPlayers)
                if (_core.PlayerManager.GetPlayer(playerId) is { } player)
                    SyncPlayer(player);
            _pendingPlayers.Clear();
        });
    }
}
