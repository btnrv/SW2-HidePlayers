using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace HidePlayers;

// The SwiftlyS2 side of VisibilityCore: the events that drive it and the engine state it reads and writes. Hiding changes
// only on these events and on corpse timers; there is no per-tick work. CS2 fires no spec_target_updated or
// spec_mode_updated (server.dll has no such event names), so spectator cameras are covered by the policy instead.
public sealed partial class HidePlayers : IVisibilityEngine
{
    private CancellationTokenSource? _wakeTimer;

    // SwiftlyS2 stops calling a game event's remaining handlers, in every plugin, once one returns Handled or Stop.
    // GameManager returns Handled for player_team, which still fires the event, so each trigger listens in both phases.
    // Handling an event twice changes nothing.
    private void HookVisibilityEvents()
    {
        HookBothPhases<EventPlayerDeath>(@event => _visibility.ReleaseViewer(@event.UserId));
        HookBothPhases<EventPlayerTeam>(@event => _visibility.ReleaseViewer(@event.UserId));
        HookBothPhases<EventBotTakeover>(@event => _visibility.ReleaseViewer(@event.UserId));
        HookBothPhases<EventPlayerSpawn>(@event =>
            _visibility.ShowSpawnedPawn(@event.UserIdPawn is { IsValid: true } pawn ? (int)pawn.Index : -1));
        HookBothPhases<EventRoundStart>(_ => _visibility.RequestRecalculation());
    }

    private void HookBothPhases<T>(Action<T> handle) where T : IGameEvent<T>
    {
        HookResult Handle(T @event)
        {
            handle(@event);
            return HookResult.Continue;
        }

        Core.GameEvent.HookPre<T>(Handle);
        Core.GameEvent.HookPost<T>(Handle);
    }

    [EventListener<EventDelegates.OnEntityParentChanged>]
    private void OnEntityParentChanged(IOnEntityParentChangedEvent @event)
    {
        var parent = @event.NewParent is { } newParent ? (int)newParent.Index : -1;
        _visibility.OnParentChanged((int)@event.Entity.Index, parent, @event.Entity is CBasePlayerPawn);
    }

    [EventListener<EventDelegates.OnEntityDeleted>]
    private void OnEntityDeleted(IOnEntityDeletedEvent @event)
    {
        if (@event.Entity is { } entity)
        {
            _visibility.OnEntityDeleted((int)entity.Index);
        }
    }

    [EventListener<EventDelegates.OnMapLoad>]
    private void OnMapLoad(IOnMapLoadEvent @event)
    {
        _visibility.Start();
    }

    [EventListener<EventDelegates.OnMapUnload>]
    private void OnMapUnload(IOnMapUnloadEvent @event)
    {
        _visibility.Stop();
        foreach (var session in _sessions.Values)
        {
            session.Override = null;
        }

        ForgetPanoramaEntity();
    }

    private void RecalculateVisibility()
    {
        Core.Profiler.StartRecording("HidePlayers.Visibility");
        try
        {
            _visibility.Recalculate();
        }
        finally
        {
            Core.Profiler.StopRecording("HidePlayers.Visibility");
        }
    }

    float IVisibilityEngine.Now => Core.Engine.GlobalVars.CurrentTime;

    float IVisibilityEngine.DeathCameraSeconds =>
        ConVarSeconds("spec_freeze_deathanim_time") + ConVarSeconds("spec_freeze_traveltime") + ConVarSeconds("spec_freeze_time");

    private float ConVarSeconds(string name) => Core.ConVar.Find<float>(name)?.Value ?? 0f;

    void IVisibilityEngine.ReadPawns(List<PawnInfo> pawns)
    {
        pawns.Clear();
        foreach (var pawn in Core.EntitySystem.GetAllEntitiesByClass<CCSPlayerPawn>())
        {
            pawns.Add(new PawnInfo((int)pawn.Index, pawn.Team, pawn.LifeState == (byte)LifeState_t.LIFE_ALIVE,
                pawn.DeathTime.Value, pawn.Controller.Raw, pawn.OriginalController.Raw, pawn.DefaultController.Raw));
        }
    }

    bool IVisibilityEngine.TryReadViewer(int slot, HashSet<int> camera, out ViewerInfo viewer)
    {
        camera.Clear();
        viewer = default;
        if (!_sessions.TryGetValue(slot, out var session) || Core.PlayerManager.GetPlayer(slot) is not { } player
            || player.SessionId != session.SessionId || player.Controller is not { IsValid: true } controller)
        {
            return false;
        }

        // Pawn is the player's own pawn while alive, a bot they took over, or their observer pawn once dead.
        var pawn = controller.Pawn.Value;
        var playerPawn = controller.PlayerPawn.Value;
        var alive = pawn is CCSPlayerPawn living && living.LifeState == (byte)LifeState_t.LIFE_ALIVE;

        // A living player observes nobody. Observer targets count only while not alive, so a handle left over from the
        // last death camera cannot keep a player visible to a living viewer.
        AddCamera(pawn, !alive, camera);
        AddCamera(playerPawn, !alive, camera);
        if (!alive)
        {
            AddCamera(controller.ObserverPawn.Value, true, camera);
            AddCameraEntity(controller.DesiredObserverTarget.Value, camera);
        }

        // After a bot takeover the latest death may be the bot's pawn, not the player's own.
        var deathTime = MathF.Max(pawn?.DeathTime.Value ?? 0f, playerPawn?.DeathTime.Value ?? 0f);
        viewer = new ViewerInfo(session.EffectivePreferences, alive, controller.Team,
            Core.EntitySystem.GetRefEHandle(controller).Raw, deathTime);
        return true;
    }

    private static void AddCamera(CBasePlayerPawn? pawn, bool observing, HashSet<int> camera)
    {
        if (pawn is null)
        {
            return;
        }

        AddCameraEntity(pawn, camera);
        if (observing && pawn.ObserverServices is { } observer)
        {
            AddCameraEntity(observer.ObserverTarget.Value, camera);
        }

        if (pawn.CameraServices is { } services)
        {
            AddCameraEntity(services.ViewEntity.Value, camera);
        }
    }

    // Adds the entity and its scene-graph ancestors: a child whose parent is not sent has nothing to follow on the client.
    private static void AddCameraEntity(CEntityInstance? entity, HashSet<int> camera)
    {
        if (entity is null)
        {
            return;
        }

        camera.Add((int)entity.Index);
        for (var node = (entity as CBaseEntity)?.CBodyComponent?.SceneNode?.Parent; node is not null; node = node.Parent)
        {
            if (node.Owner is { } owner)
            {
                camera.Add((int)owner.Index);
            }
        }
    }

    // Held weapons (CS2 equips them with FollowEntity), cosmetics, particles and anything else parented in the scene graph.
    void IVisibilityEngine.ReadAttachments(int entity, List<int> attachments)
    {
        if (Core.EntitySystem.GetEntityByIndex((uint)entity) is CBaseEntity { CBodyComponent.SceneNode: { } node })
        {
            AddChildren(node, attachments);
        }
    }

    // Child, NextSibling and Owner return a wrapper cached on the node they are read from, so each node reads each once.
    private void AddChildren(CGameSceneNode node, List<int> attachments)
    {
        for (var child = node.Child; child is not null; child = child.NextSibling)
        {
            var attached = child.Owner is { } owner ? Core.EntitySystem.GetEntityByIndex(owner.Index) : null;

            // A pawn parented to another pawn, with everything on it, gets its own decision.
            if (attached is CBasePlayerPawn)
            {
                continue;
            }

            if (attached is not null)
            {
                attachments.Add((int)attached.Index);
            }

            AddChildren(child, attachments);
        }
    }

    bool IVisibilityEngine.IsBlocked(int slot, int entity) =>
        Core.PlayerManager.GetPlayer(slot) is not { } player || player.IsTransmitEntityBlocked(entity);

    void IVisibilityEngine.SetBlocked(int slot, int entity, bool blocked) =>
        Core.PlayerManager.GetPlayer(slot)?.ShouldBlockTransmitEntity(entity, blocked);

    // NextTick work runs in SwiftlyS2's GameFrame hook, before the engine builds this frame's snapshots.
    void IVisibilityEngine.RecalculateAtEndOfFrame() => Core.Scheduler.NextTick(RecalculateVisibility);

    // Timers run after the NextTick queue, still before the snapshot. The timer counts real time and the core game time,
    // so a wake-up that comes early just schedules the rest.
    void IVisibilityEngine.RecalculateAt(float time)
    {
        _wakeTimer?.Cancel();
        _wakeTimer = Core.Scheduler.DelayBySeconds(
            MathF.Max(time - Core.Engine.GlobalVars.CurrentTime, 0f), RecalculateVisibility);
    }
}
