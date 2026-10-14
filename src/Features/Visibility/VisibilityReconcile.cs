using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace HidePlayers;

public sealed partial class HidePlayers
{
    private readonly record struct PawnSnapshot(TargetView View, uint Handle);

    // Per viewer slot: the entity indexes this plugin blocked, with the handle of the pawn that held the index.
    // The engine's blocked set is shared with other plugins, so only these entries are ever unblocked.
    private readonly Dictionary<int, Dictionary<int, uint>> _blocked = [];
    private readonly List<PawnSnapshot> _pawns = [];
    private readonly HashSet<int> _pawnIndexes = [];
    private readonly List<int> _staleIndexes = [];
    private bool _visibilityDirty;
    private bool _reconcileQueued;

    private void MarkVisibilityDirty()
    {
        _visibilityDirty = true;
        if (_reconcileQueued)
        {
            return;
        }

        _reconcileQueued = true;
        Core.Scheduler.NextTick(ReconcileVisibility);
    }

    private void ReconcileVisibility()
    {
        _reconcileQueued = false;
        if (!_active || !_visibilityDirty)
        {
            return;
        }

        Core.Profiler.StartRecording("HidePlayers.Reconcile");
        try
        {
            SnapshotPawns();
            _visibilityDirty = false;
            foreach (var (slot, session) in _sessions)
            {
                if (Core.PlayerManager.GetPlayer(slot) is { } viewer && viewer.SessionId == session.SessionId)
                {
                    ReconcileViewer(viewer, session);
                }
            }
        }
        finally
        {
            Core.Profiler.StopRecording("HidePlayers.Reconcile");
        }
    }

    private void SnapshotPawns()
    {
        _pawns.Clear();
        _pawnIndexes.Clear();
        foreach (var pawn in Core.EntitySystem.GetAllEntitiesByClass<CCSPlayerPawn>())
        {
            if (!pawn.IsValid)
            {
                continue;
            }

            var view = new TargetView((int)pawn.Index, pawn.Team, pawn.LifeState == (byte)LifeState_t.LIFE_ALIVE);
            _pawns.Add(new PawnSnapshot(view, Core.EntitySystem.GetRefEHandle(pawn).Raw));
            _pawnIndexes.Add(view.Index);
        }
    }

    private void ReconcileViewer(IPlayer viewer, PlayerSession session)
    {
        var slot = viewer.PlayerID;
        if (!_blocked.TryGetValue(slot, out var blocked))
        {
            blocked = [];
            _blocked[slot] = blocked;
        }

        var viewerView = DescribeViewer(viewer);
        var preferences = session.EffectivePreferences;
        foreach (var pawn in _pawns)
        {
            var index = pawn.View.Index;
            var known = blocked.TryGetValue(index, out var knownHandle);
            if (VisibilityPolicy.ShouldHide(preferences, viewerView, pawn.View))
            {
                // A different handle means that the index was reused. The engine cleared the old bit when the
                // old pawn was deleted, so the new pawn needs its own block.
                if (!known || knownHandle != pawn.Handle)
                {
                    viewer.ShouldBlockTransmitEntity(index, true);
                    blocked[index] = pawn.Handle;
                }
            }
            else if (known)
            {
                if (knownHandle == pawn.Handle)
                {
                    viewer.ShouldBlockTransmitEntity(index, false);
                }

                blocked.Remove(index);
            }
        }

        // An index without a pawn belongs to a deleted entity, and the engine already cleared its bit.
        _staleIndexes.Clear();
        foreach (var index in blocked.Keys)
        {
            if (!_pawnIndexes.Contains(index))
            {
                _staleIndexes.Add(index);
            }
        }

        foreach (var index in _staleIndexes)
        {
            blocked.Remove(index);
        }
    }

    private static ViewerView DescribeViewer(IPlayer viewer)
    {
        if (viewer.Controller is not { IsValid: true } controller)
        {
            return new ViewerView(false, Team.None, -1, -1);
        }

        var ownPawnIndex = viewer.PlayerPawn is { IsValid: true } ownPawn ? (int)ownPawn.Index : -1;
        return new ViewerView(viewer.IsAlive, controller.Team, ownPawnIndex, ObservedIndex(controller));
    }

    // A dead player controls an observer pawn that holds the observer target. Both pawns are checked because the
    // services live on the pawn that the player currently controls.
    private static int ObservedIndex(CCSPlayerController controller)
    {
        var observed = ObservedIndex(controller.ObserverPawn.Value);
        return observed >= 0 ? observed : ObservedIndex(controller.Pawn.Value);
    }

    private static int ObservedIndex(CBasePlayerPawn? pawn)
    {
        if (pawn is not { IsValid: true, ObserverServices: { } services })
        {
            return -1;
        }

        var target = services.ObserverTarget;
        return target.IsValid ? (int)target.EntityIndex : -1;
    }

    private void ReleaseTransmitBlocks()
    {
        foreach (var (slot, blocked) in _blocked)
        {
            if (Core.PlayerManager.GetPlayer(slot) is not { } viewer
                || !_sessions.TryGetValue(slot, out var session)
                || session.SessionId != viewer.SessionId)
            {
                continue;
            }

            foreach (var (index, handle) in blocked)
            {
                // A pawn that is gone had its bit cleared by the engine.
                if (new CHandle<CCSPlayerPawn>(handle).IsValid)
                {
                    viewer.ShouldBlockTransmitEntity(index, false);
                }
            }
        }

        _blocked.Clear();
    }
}
