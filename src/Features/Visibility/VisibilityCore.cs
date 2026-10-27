using HidePlayers.Contract;
using SwiftlyS2.Shared.Players;

namespace HidePlayers;

/// <summary>
/// A player pawn. Controller identities are raw entity handles, <see cref="VisibilityCore.NoController"/> when unset.
/// <c>DeathTime</c> is the game time of its last death.
/// </summary>
internal readonly record struct PawnInfo(
    int Index, Team Team, bool Alive, float DeathTime, uint Controller, uint OriginalController, uint DefaultController);

/// <summary>A human viewer. <c>Controller</c> is the raw handle of their controller; <c>DeathTime</c> their last death.</summary>
internal readonly record struct ViewerInfo(HidePreferences Preferences, bool Alive, Team Team, uint Controller, float DeathTime);

/// <summary>Every engine read and write <see cref="VisibilityCore"/> makes. Called on the game thread only.</summary>
internal interface IVisibilityEngine
{
    /// <summary>The current game time, in seconds.</summary>
    float Now { get; }

    /// <summary>How long the death camera lasts: death animation, travel and freeze time.</summary>
    float DeathCameraSeconds { get; }

    /// <summary>Replaces the contents of <paramref name="pawns"/> with every player pawn.</summary>
    void ReadPawns(List<PawnInfo> pawns);

    /// <summary>
    /// Describes the human in this slot, or returns false without one. Fills <paramref name="camera"/> with what their
    /// client needs: their pawns and view entity, their observer targets while not alive, and every scene-graph
    /// ancestor of those.
    /// </summary>
    bool TryReadViewer(int slot, HashSet<int> camera, out ViewerInfo viewer);

    /// <summary>Adds the entities attached below this one, skipping player pawns and everything attached to them.</summary>
    void ReadAttachments(int entity, List<int> attachments);

    bool IsBlocked(int slot, int entity);

    void SetBlocked(int slot, int entity, bool blocked);

    /// <summary>Runs <see cref="VisibilityCore.Recalculate"/> at the end of this game frame, before its snapshot.</summary>
    void RecalculateAtEndOfFrame();

    /// <summary>Runs <see cref="VisibilityCore.Recalculate"/> once at this game time, replacing an earlier request.</summary>
    void RecalculateAt(float time);
}

/// <summary>
/// Decides what each viewer does not receive and owns the transmit bits HidePlayers sets. Pawn decisions are recalculated
/// at the end of the frame after state changes; attachments, spawns and deaths are also applied at once, because a
/// change made after this frame's recalculation (from a timer or a later NextTick) still reaches this frame's snapshot.
/// </summary>
internal sealed class VisibilityCore(IVisibilityEngine engine)
{
    public const uint NoController = uint.MaxValue;

    // CS2 accepts a dying or dead pawn as a spectator target until 6 seconds after its death
    // (CPlayer_ObserverServices::IsValidObserverTarget, server.dll 1.41.9.2).
    private const float ObserverTargetSeconds = 6f;
    private const float SettleMarginSeconds = 0.5f;
    private const float Always = float.MinValue;
    private const float Never = float.MaxValue;

    private sealed class Viewer
    {
        // The bits HidePlayers set. SwiftlyS2 keeps one set per viewer for all plugins, so only these are cleared.
        public readonly HashSet<int> Owned = [];

        // The pawns the policy hides, including one whose bit another plugin set first.
        public readonly HashSet<int> Hidden = [];

        // What the client needs, as of the last recalculation. No event reports camera changes.
        public readonly HashSet<int> Camera = [];

        public bool Living;
        public bool ReleasePending;
        public float ReleasedAt;
    }

    private readonly Dictionary<int, Viewer> _viewers = [];
    private readonly List<PawnInfo> _pawns = [];
    private readonly Dictionary<int, List<int>> _trees = [];
    private bool _running = true;
    private bool _queued;

    public void AddViewer(int slot)
    {
        _viewers.TryAdd(slot, new Viewer());
        RequestRecalculation();
    }

    /// <summary>Drops a viewer whose connection ended. SwiftlyS2 has already cleared their bits.</summary>
    public void ForgetViewer(int slot) => _viewers.Remove(slot);

    public void Start()
    {
        _running = true;
        RequestRecalculation();
    }

    /// <summary>Releases every block and recalculates nothing until <see cref="Start"/>.</summary>
    public void Stop()
    {
        _running = false;
        foreach (var (slot, viewer) in _viewers)
        {
            Release(slot, viewer);
        }
    }

    public void RequestRecalculation()
    {
        if (!_queued)
        {
            _queued = true;
            engine.RecalculateAtEndOfFrame();
        }
    }

    /// <summary>
    /// Releases a viewer who dies, changes team or takes over a bot. Their camera can move to another player, such as
    /// their killer, in this frame.
    /// </summary>
    public void ReleaseViewer(int slot)
    {
        if (_running && _viewers.TryGetValue(slot, out var viewer))
        {
            Release(slot, viewer);
            // Entity deletion can arrive during shutdown after GlobalVars is gone. Start the camera's settling
            // window on the next recalculation, when gameplay resumes, without reading the clock in this callback.
            viewer.ReleasePending = true;
        }

        RequestRecalculation();
    }

    /// <summary>Shows a pawn that came alive to every viewer who is not alive, since they can now spectate it.</summary>
    public void ShowSpawnedPawn(int pawn)
    {
        _trees.Clear();
        foreach (var (slot, viewer) in _viewers)
        {
            if (!viewer.Living && viewer.Hidden.Remove(pawn))
            {
                SetTree(slot, viewer, pawn, false);
            }
        }

        RequestRecalculation();
    }

    /// <summary>
    /// Hides an entity attached below a hidden one, and shows an entity taken from one, with everything attached to it.
    /// Weapons are equipped with FollowEntity and dropped by clearing the parent, so a hidden player's weapon is never
    /// sent without its holder, and a dropped one is sent.
    /// </summary>
    public void OnParentChanged(int entity, int parent, bool entityIsPawn)
    {
        if (entityIsPawn)
        {
            // A pawn keeps its own decision, but what it hangs from can now be something a camera needs.
            RequestRecalculation();
            return;
        }

        _trees.Clear();
        foreach (var (slot, viewer) in _viewers)
        {
            var hide = parent >= 0 && (viewer.Owned.Contains(parent) || viewer.Hidden.Contains(parent));
            if (hide && viewer.Camera.Contains(entity))
            {
                // The recalculation shows what the viewer's camera now hangs from.
                RequestRecalculation();
            }
            else if (hide || viewer.Owned.Contains(entity))
            {
                SetTree(slot, viewer, entity, hide);
            }
        }
    }

    /// <summary>
    /// Forgets a deleted index. SwiftlyS2 cleared its bit for every viewer before this, so a reused index starts visible.
    /// </summary>
    public void OnEntityDeleted(int entity)
    {
        foreach (var (slot, viewer) in _viewers)
        {
            viewer.Owned.Remove(entity);
            viewer.Hidden.Remove(entity);

            // A living viewer who lost their pawn or view entity sees through another camera from now on.
            if (viewer.Living && viewer.Camera.Contains(entity))
            {
                ReleaseViewer(slot);
            }
        }
    }

    /// <summary>
    /// Decides every pawn for every viewer and applies the decisions that changed, with each pawn's attachments. A dead
    /// viewer's corpse decisions also depend on time, so the earliest one still due wakes the next recalculation.
    /// </summary>
    public void Recalculate()
    {
        _queued = false;
        if (!_running || _viewers.Count == 0)
        {
            return;
        }

        try
        {
            var now = engine.Now;
            var settle = MathF.Max(ObserverTargetSeconds, engine.DeathCameraSeconds) + SettleMarginSeconds;
            var wake = Never;
            _trees.Clear();
            engine.ReadPawns(_pawns);
            foreach (var (slot, viewer) in _viewers)
            {
                if (viewer.ReleasePending)
                {
                    viewer.ReleasedAt = now;
                    viewer.ReleasePending = false;
                }

                var known = engine.TryReadViewer(slot, viewer.Camera, out var info);
                viewer.Living = known && info.Alive && IsPlaying(info.Team);
                var settled = MathF.Max(info.DeathTime, viewer.ReleasedAt) + settle;
                foreach (var pawn in _pawns)
                {
                    var from = known ? HiddenFrom(info, viewer.Living, settled, pawn, settle) : Never;
                    if (from != Never && viewer.Camera.Contains(pawn.Index))
                    {
                        // A spectator camera leaves a corpse without an event, so look again in a second.
                        wake = MathF.Min(wake, MathF.Max(from, now + 1f));
                        from = Never;
                    }

                    var hide = from <= now;
                    if (!hide)
                    {
                        wake = MathF.Min(wake, from);
                    }

                    if (hide ? viewer.Hidden.Add(pawn.Index) : viewer.Hidden.Remove(pawn.Index))
                    {
                        SetTree(slot, viewer, pawn.Index, hide);
                    }
                }
            }

            if (wake != Never)
            {
                engine.RecalculateAt(wake);
            }
        }
        catch
        {
            // Blocks decided from a half-read state could hide what a camera needs; show everything instead.
            foreach (var (slot, viewer) in _viewers)
            {
                Release(slot, viewer);
            }

            throw;
        }
    }

    /// <returns>The game time from which the viewer does not receive the pawn, <see cref="Never"/> if not now.</returns>
    private static float HiddenFrom(ViewerInfo viewer, bool living, float viewerSettled, PawnInfo pawn, float settle)
    {
        // The viewer's own pawns, including a bot they took over and their own corpse.
        if (pawn.Controller == viewer.Controller || pawn.OriginalController == viewer.Controller
            || pawn.DefaultController == viewer.Controller)
        {
            return Never;
        }

        if (!pawn.Alive)
        {
            // A living viewer cannot spectate, so corpses go at once. A dead viewer waits until its own death camera is
            // over and the corpse can no longer become a spectator target; no event marks either moment.
            return !viewer.Preferences.HideDead ? Never : living ? Always : MathF.Max(viewerSettled, pawn.DeathTime + settle);
        }

        // Dead players and spectators always see the living, so they can spectate them.
        var hidden = living && pawn.Controller != NoController && IsPlaying(pawn.Team) && viewer.Preferences.Mode switch
        {
            HideMode.All => true,
            HideMode.Teammates => viewer.Team == pawn.Team,
            HideMode.Enemies => viewer.Team != pawn.Team,
            _ => false
        };
        return hidden ? Always : Never;
    }

    private static bool IsPlaying(Team team) => team is Team.T or Team.CT;

    /// <summary>Blocks or shows an entity with everything attached to it.</summary>
    private void SetTree(int slot, Viewer viewer, int root, bool blocked)
    {
        if (!_trees.TryGetValue(root, out var tree))
        {
            _trees[root] = tree = [root];
            engine.ReadAttachments(root, tree);
        }

        foreach (var entity in tree)
        {
            if (!blocked)
            {
                if (viewer.Owned.Remove(entity))
                {
                    engine.SetBlocked(slot, entity, false);
                }
            }
            else if (!viewer.Owned.Contains(entity) && !engine.IsBlocked(slot, entity))
            {
                // A bit another plugin set first stays that plugin's.
                engine.SetBlocked(slot, entity, true);
                viewer.Owned.Add(entity);
            }
        }
    }

    private void Release(int slot, Viewer viewer)
    {
        foreach (var entity in viewer.Owned)
        {
            engine.SetBlocked(slot, entity, false);
        }

        viewer.Owned.Clear();
        viewer.Hidden.Clear();
        viewer.Living = false;
        viewer.ReleasePending = false;
    }
}
