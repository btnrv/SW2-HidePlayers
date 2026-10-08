using HidePlayers.Configuration;
using HidePlayers.Contract;
using SwiftlyS2.Shared.Commands;

namespace HidePlayers.Runtime;

public sealed partial class HidePlayersRuntime
{
    private Guid? _menuCommandRegistration;

    private void RegisterCommands()
    {
        var settings = _settings.Current.MenuCommand;
        _menuCommandRegistration = _core.Command.RegisterCommand(settings.Name, OpenMenuCommand, registerRaw: false, permission: settings.Permission);
        foreach (var alias in settings.Aliases)
            _core.Command.RegisterCommandAlias(settings.Name, alias, registerRaw: false);
    }

    private void UnregisterCommands()
    {
        if (_menuCommandRegistration is { } registration)
            _core.Command.UnregisterCommand(registration);
        _menuCommandRegistration = null;
    }

    private void OpenMenuCommand(ICommandContext context)
    {
        if (!_running)
            return;
        if (context.Sender is not { IsValid: true, IsFakeClient: false } player)
        {
            context.Reply(_core.Localizer["command.player_only", _settings.Current.Prefix]);
            return;
        }
        SyncPlayer(player);
        // Console dispatch uses "sw_"; chat dispatch keeps the server's configured chat prefix.
        if (context.Args.Length == 0 && context.Prefix != "sw_")
        {
            if (_menuProvider is { } provider)
                provider.OpenMenu(player.PlayerID);
            else if (_settings.Current.MenuRenderer == MenuRenderer.Panorama)
                OpenPanoramaMenu(player);
            else
                OpenPersonalMenu(player);
            return;
        }

        if (Session(player.PlayerID) is not { } session)
            return;
        var localizer = _core.Translation.GetPlayerLocalizer(player);
        HidePreferences selected;
        if (context.Args.Length == 0)
            selected = session.ToggledPreferences;
        else if (!HideCommandParser.TryParse(context.Args, session.Preferences, out selected))
        {
            context.Reply(localizer["command.usage", _settings.Current.Prefix, _settings.Current.MenuCommand.Name]);
            return;
        }
        ChangePreferences(player, selected);
        var status = localizer[selected.HideDead ? "status.on" : "status.off"];
        context.Reply(localizer["command.applied", _settings.Current.Prefix, localizer[$"menu.mode.{selected.Mode}"], status]);
    }
}
