using HidePlayers.Contract;
using SwiftlyS2.Shared.Commands;

namespace HidePlayers;

public sealed partial class HidePlayers
{
    // The command name and aliases come from the settings, so they are registered here and not with [Command].
    private void RegisterCommands()
    {
        var command = Settings.MenuCommand;
        Core.Command.RegisterCommand(
            command.Name, OnHideCommand, registerRaw: false, permission: command.Permission,
            helpText: "Hide other players and corpses.");
        foreach (var alias in command.Aliases)
        {
            Core.Command.RegisterCommandAlias(command.Name, alias, registerRaw: false);
        }

        Core.Command.RegisterCommand(
            "hidemenu", OnHideMenuCommand, registerRaw: false, permission: command.Permission,
            helpText: "Open the player and corpse visibility menu.");
    }

    private void OnHideMenuCommand(ICommandContext context)
    {
        if (!context.IsSentByPlayer || context.Sender is not { IsFakeClient: false } player || EnsureSession(player) is null)
        {
            context.Reply(Core.Localizer.Prefixed("command.player_only", Settings.Prefix));
            return;
        }

        OpenFrameworkMenu(player);
    }

    private void OnHideCommand(ICommandContext context)
    {
        if (!context.IsSentByPlayer || context.Sender is not { IsFakeClient: false } player || EnsureSession(player) is not { } session)
        {
            context.Reply(Core.Localizer.Prefixed("command.player_only", Settings.Prefix));
            return;
        }

        var localizer = Core.Translation.GetPlayerLocalizer(player);
        HidePreferences selected;
        if (context.Args.Length == 0)
        {
            selected = session.ToggledPreferences;
        }
        else if (!HideCommandParser.TryParse(context.Args, session.Preferences, out selected))
        {
            context.Reply(localizer.Prefixed("command.usage", Settings.Prefix, context.Prefix, context.CommandName));
            return;
        }

        ChangePreferences(player, selected);
        var status = localizer[selected.HideDead ? "status.on" : "status.off"];
        context.Reply(localizer.Prefixed("command.applied", Settings.Prefix, localizer[$"menu.mode.{selected.Mode}"], status));
    }
}
