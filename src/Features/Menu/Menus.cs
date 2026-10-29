using HidePlayers.Contract;
using SwiftlyS2.Shared.Players;

namespace HidePlayers;

public sealed partial class HidePlayers
{
    private IHidePlayersMenuProvider? _menuProvider;

    private void RefreshMenus(IPlayer player)
    {
        _frameworkMenu?.GetSession(player)?.Invalidate();
    }

    // Core.Menu closes only the menus of this plugin.
    private void CloseMenus()
    {
        Core.Menu.CloseAll();
    }
}
