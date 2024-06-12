using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ProtoBuf;
using TentBag.Configuration;
using TentBag.Items;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace TentBag;

public class TentBag : ModSystem {
    public static TentBag Instance { get; private set; } = null!;

    public ICoreAPI Api { get; private set; } = null!;

    public ILogger Logger => Mod.Logger;
    public string ModId => Mod.Info.ModID;
    public Config Config => _config ?? new Config();

    private Config? _config;
    private FileWatcher? _fileWatcher;
    private IServerNetworkChannel? _channel;

    public TentBag() {
        Instance = this;
    }

    public override void StartPre(ICoreAPI api) {
        Api = api;
        ReloadConfig();
    }

    public override void Start(ICoreAPI api) {
        api.RegisterItemClass("TentBag", typeof(ItemTentBag));
    }

    public override void StartClientSide(ICoreClientAPI capi) {
        capi.Network.RegisterChannel(Mod.Info.ModID)
            .RegisterMessageType<ErrorPacket>()
            .SetMessageHandler<ErrorPacket>(packet => {
                if (!string.IsNullOrEmpty(packet.Error)) {
                    (Api as ICoreClientAPI)?.TriggerIngameError(this, "error", packet.Error);
                }
            });
    }

    public override void StartServerSide(ICoreServerAPI sapi) {
        _channel = sapi.Network.RegisterChannel(Mod.Info.ModID)
            .RegisterMessageType<ErrorPacket>();
    }

    public void SendClientError(IPlayer? player, string error) {
        if (player is IServerPlayer serverPlayer) {
            _channel?.SendPacket(new ErrorPacket { Error = error }, serverPlayer);
        }
    }

    public void ReloadConfig() {
        _config = Api.LoadModConfig<Config>($"{ModId}.json") ?? new Config();

        (_fileWatcher ??= new FileWatcher(this)).Queued = true;

        string json = JsonConvert.SerializeObject(_config, new JsonSerializerSettings {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Include,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        });

        FileInfo fileInfo = new(Path.Combine(GamePaths.ModConfig, $"{ModId}.json"));
        GamePaths.EnsurePathExists(fileInfo.Directory!.FullName);
        File.WriteAllText(fileInfo.FullName, json);

        Api.Event.RegisterCallback(_ => _fileWatcher.Queued = false, 100);
    }

    public override void Dispose() {
        _fileWatcher?.Dispose();
        _fileWatcher = null;

        _channel = null;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    private class ErrorPacket {
        public string? Error;
    }
}
