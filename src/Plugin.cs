using HidePlayers.Models;
using HidePlayers.Services;
using Microsoft.Extensions.Configuration;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;

namespace HidePlayers;

[PluginMetadata(
    Id = "HidePlayers",
    Version = "1.0.0",
    Name = "HidePlayers",
    Author = "gyro",
    Description = "Allows players to toggle visibility of other player models."
)]
public sealed partial class Plugin(ISwiftlyCore core) : BasePlugin(core)
{
    private HidePlayersConfig _config = new();
    private HideMode _mode = HideMode.All;
    private VisibilityService _visibility = null!;
    private readonly List<Guid> _commandGuids = [];

    public override void Load(bool hotReload)
    {
        LoadConfiguration();
        _visibility = new VisibilityService(Core);
        RegisterCommands();

        if (hotReload)
        {
            foreach (var player in Core.PlayerManager.GetAllPlayers())
                _visibility.InitPlayer(player.PlayerID);

            _visibility.RecalculateAll(_mode);
        }
    }

    public override void Unload()
    {
        UnregisterCommands();
        _visibility.ClearAll();
    }

    private void LoadConfiguration()
    {
        Core.Configuration
            .InitializeJsonWithModel<HidePlayersConfig>("config.jsonc", "Main")
            .Configure(builder =>
            {
                builder.AddJsonFile(
                    Core.Configuration.GetConfigPath("config.jsonc"),
                    optional: false,
                    reloadOnChange: true
                );
            });

        ReloadConfigValues();
    }

    private void ReloadConfigValues()
    {
        if (Core.Configuration.Manager is IConfigurationRoot root)
            root.Reload();

        _config = Core.Configuration.Manager
            .GetSection("Main")
            .Get<HidePlayersConfig>() ?? new HidePlayersConfig();

        _mode = HideModeParser.Parse(_config.HideMode);
    }
}
