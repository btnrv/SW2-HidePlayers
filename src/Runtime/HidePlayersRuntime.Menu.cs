using SwiftlyS2.Shared.Players;

namespace HidePlayers.Runtime;

public sealed partial class HidePlayersRuntime
{
    private readonly Dictionary<int, PersonalMenu> _personalMenus = [];

    private void OpenPersonalMenu(IPlayer player)
    {
        if (_sessions.Get(player.PlayerID) is not { } session)
            return;
        CloseMenu(player.PlayerID);
        if (!_personalMenus.TryGetValue(player.PlayerID, out var owned))
        {
            owned = CreatePersonalMenu(player, session);
            _personalMenus.Add(player.PlayerID, owned);
        }
        RefreshMenuContents(player, owned);
        _core.MenusAPI.OpenMenuForPlayer(player, owned.Menu);
    }

    private IPlayer? ResolveMenuSession(int slot, ulong sessionId)
    {
        if (!_running || _sessions.Get(slot)?.SessionId != sessionId)
            return null;
        var player = _core.PlayerManager.GetPlayer(slot);
        return player is { IsValid: true } && player.SessionId == sessionId ? player : null;
    }

    private void RefreshOpenMenu(IPlayer player)
    {
        RefreshPanoramaMenu(player);
        if (_personalMenus.TryGetValue(player.PlayerID, out var owned)
            && ReferenceEquals(_core.MenusAPI.GetCurrentMenu(player), owned.Menu))
            RefreshMenuContents(player, owned);
    }

    private void CloseMenu(int slot)
    {
        ClosePanoramaMenu(slot);
        if (!_personalMenus.TryGetValue(slot, out var owned))
            return;
        var player = _core.PlayerManager.GetPlayer(slot);
        if (player is not null && ReferenceEquals(_core.MenusAPI.GetCurrentMenu(player), owned.Menu))
            _core.MenusAPI.CloseMenuForPlayer(player, owned.Menu);
    }

    private void CloseMenus()
    {
        ClosePanoramaMenus();
        foreach (var slot in _personalMenus.Keys)
            CloseMenu(slot);
    }

    private void DisposeMenu(int slot)
    {
        CloseMenu(slot);
        if (_personalMenus.Remove(slot, out var owned))
            owned.Dispose();
    }

    private void DisposeMenus()
    {
        CloseMenus();
        foreach (var owned in _personalMenus.Values)
            owned.Dispose();
        _personalMenus.Clear();
    }
}
