using HidePlayers.Configuration;
using HidePlayers.Contract;
using HidePlayers.Runtime;
using HidePlayers.Storage;
using Microsoft.Extensions.DependencyInjection;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;

namespace HidePlayers;

[PluginMetadata(Id = "HidePlayers", Version = "2.0.0", Name = "HidePlayers", Author = "gyro",
    Description = "Personal player and corpse visibility preferences.")]
public sealed class HidePlayers(ISwiftlyCore core) : BasePlugin(core)
{
    private ServiceProvider? _services;

    public override void Load(bool hotReload)
    {
        Core.Configuration.InitializeJsonWithModel<PluginSettings>("settings.jsonc", "Main");
        var services = new ServiceCollection().AddSwiftly(Core, addConfiguration: false);
        services.AddSingleton(new SettingsService(Core.Configuration.GetConfigPath("settings.jsonc")));
        services.AddSingleton(provider =>
        {
            var connectionName = provider.GetRequiredService<SettingsService>().Current.DatabaseConnection;
            return new PreferenceStore(() => Core.Database.GetConnection(connectionName));
        });
        services.AddSingleton<HidePlayersRuntime>();
        _services = services.BuildServiceProvider();
        _services.GetRequiredService<HidePlayersRuntime>().Start();
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
    {
        interfaceManager.AddSharedInterface<IHidePlayersApi, HidePlayersRuntime>(
            IHidePlayersApi.InterfaceKey, _services!.GetRequiredService<HidePlayersRuntime>());
    }

    public override void Unload()
    {
        _services?.Dispose();
        _services = null;
    }
}
