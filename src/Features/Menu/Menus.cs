using HidePlayers.Contract;
using SwiftlyS2.Shared.Players;

namespace HidePlayers;

public sealed partial class HidePlayers
{
    private IHidePlayersMenuProvider? _menuProvider;

    // A registered provider wins, then the configured renderer.
    private void OpenMenu(IPlayer player)
    {
        if (_menuProvider is { } provider)
        {
            provider.OpenMenu(player.PlayerID);
        }
        else if (Settings.MenuRenderer == MenuRenderer.Panorama)
        {
            OpenPanoramaMenu(player);
        }
        else
        {
            OpenFrameworkMenu(player);
        }
    }

    private void RefreshMenus(IPlayer player)
    {
        _frameworkMenu?.GetSession(player)?.Invalidate();
        RefreshPanoramaMenu(player);
    }

    // Core.Menu closes only the menus of this plugin.
    private void CloseMenus()
    {
        Core.Menu.CloseAll();
        ClosePanoramaMenus();
    }
}
