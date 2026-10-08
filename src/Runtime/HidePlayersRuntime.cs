using HidePlayers.Configuration;
using HidePlayers.Contract;
using HidePlayers.State;
using HidePlayers.Storage;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace HidePlayers.Runtime;

public sealed partial class HidePlayersRuntime : IHidePlayersApi, IDisposable
{
    private readonly ISwiftlyCore _core;
    private readonly SettingsService _settings;
    private readonly PreferenceStore _store;
    private readonly ILogger<HidePlayersRuntime> _logger;
    private readonly PlayerSessions _sessions;
    private readonly TransmitBlocks _blocks;
    private readonly IPlayer?[] _players;
    private readonly uint?[] _livePawns;
    private readonly Dictionary<uint, CCSPlayerPawn> _pawns = [];
    private readonly HashSet<ulong> _startedLoads = [];
    private IHidePlayersMenuProvider? _menuProvider;
    private bool _running;

    public HidePlayersRuntime(ISwiftlyCore core, SettingsService settings, PreferenceStore store,
        ILogger<HidePlayersRuntime> logger)
    {
        _core = core;
        _settings = settings;
        _store = store;
        _logger = logger;
        var capacity = core.PlayerManager.PlayerCap;
        _players = new IPlayer?[capacity];
        _livePawns = new uint?[capacity];
        _sessions = new PlayerSessions(capacity);
        _blocks = new TransmitBlocks(capacity, (slot, entity, blocked) =>
            _players[slot]?.ShouldBlockTransmitEntity(entity, blocked));
    }

    public void Start()
    {
        _running = true;
        RegisterCommands();
        SubscribeEvents();
        _ = InitializeStorageAsync();
        SeedPlayersAndPawns();
    }

    private PlayerSession? Session(int slot) =>
        _running && (uint)slot < _players.Length ? _sessions.Get(slot) : null;

    private void SyncPlayer(IPlayer player)
    {
        var slot = player.PlayerID;
        if (_players[slot] is { } previous && previous.SessionId != player.SessionId)
            RemovePlayer(slot);
        _players[slot] = player;
        if (!player.IsFakeClient && _sessions.Get(slot) is null)
            _sessions.Connect(slot, player.SessionId, _settings.Current.DefaultPreferences);
        if (!player.IsFakeClient)
        {
            BeginPreferenceLoad(player);
            RefreshViewer(player);
        }
        if (player.Controller.IsValid && player.PlayerPawn is { IsValid: true } pawn)
            TrackPawn(pawn, slot);
    }

    private void RefreshViewer(IPlayer player)
    {
        if (Session(player.PlayerID) is not { } session)
            return;
        _core.Profiler.StartRecording("HidePlayers.Viewer");
        try
        {
            foreach (var (handle, pawn) in _pawns)
                ApplyPair(player, session.EffectivePreferences, handle, pawn);
        }
        finally { _core.Profiler.StopRecording("HidePlayers.Viewer"); }
        RefreshOpenMenu(player);
    }

    private void TrackPawn(CCSPlayerPawn pawn, int ownerSlot)
    {
        var handle = _core.EntitySystem.GetRefEHandle(pawn).Raw;
        if ((uint)ownerSlot < _livePawns.Length)
        {
            if (pawn.LifeState == 0)
            {
                if (_livePawns[ownerSlot] is { } previous && previous != handle)
                    RemovePawn(previous);
                _livePawns[ownerSlot] = handle;
            }
            else if (_livePawns[ownerSlot] == handle)
                _livePawns[ownerSlot] = null;
        }
        _pawns[handle] = pawn;
        _core.Profiler.StartRecording("HidePlayers.Pawn");
        try
        {
            for (var slot = 0; slot < _players.Length; slot++)
                if (_players[slot] is { } viewer && _sessions.Get(slot) is { } session)
                    ApplyPair(viewer, session.EffectivePreferences, handle, pawn);
        }
        finally { _core.Profiler.StopRecording("HidePlayers.Pawn"); }
    }

    private void ApplyPair(IPlayer viewer, HidePreferences preferences, uint handle, CCSPlayerPawn pawn)
    {
        if (!pawn.IsValid)
            return;
        var blocked = viewer.IsValid && VisibilityPolicy.ShouldHide(
            viewer.IsAlive, (int)viewer.Controller.Team, preferences,
            viewer.PlayerPawn?.Index == pawn.Index, pawn.TeamNum, pawn.LifeState != 0);
        _blocks.Set(handle, (int)pawn.Index, viewer.PlayerID, blocked);
    }

    private void RemovePawn(uint handle)
    {
        _blocks.Remove(handle);
        _pawns.Remove(handle);
    }

    private void RemovePlayer(int slot)
    {
        DisposeMenu(slot);
        _blocks.RemoveViewer(slot);
        if (_livePawns[slot] is { } handle)
            RemovePawn(handle);
        if (_sessions.Get(slot) is { } session)
            _startedLoads.Remove(session.SessionId);
        _sessions.Disconnect(slot);
        _players[slot] = null;
        _livePawns[slot] = null;
    }

    private void SeedPlayersAndPawns()
    {
        foreach (var player in _core.PlayerManager.GetAllPlayers())
            SyncPlayer(player);
        foreach (var pawn in _core.EntitySystem.GetAllEntitiesByClass<CCSPlayerPawn>())
            if (pawn.IsValid && pawn.LifeState != 0)
                TrackPawn(pawn, -1);
    }

    public void Dispose()
    {
        if (!_running)
            return;
        _running = false;
        UnsubscribeEvents();
        UnregisterCommands();
        DisposeMenus();
        _panoramaHud?.Despawn();
        _panoramaHud = null;
        _menuProvider = null;
        _blocks.Clear();
        _pawns.Clear();
        _sessions.Clear();
        Array.Clear(_players);
        try { _store.FlushAsync().GetAwaiter().GetResult(); }
        catch (Exception exception) { _logger.LogError(exception, "Could not finish saving HidePlayers preferences"); }
    }
}
