﻿using System.Diagnostics.CodeAnalysis;
using TentBag.Compatibility;
using TentBag.Configuration;
using TentBag.Items;
using TentBag.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace TentBag;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public class TentBag : ModSystem {
    public static TentBag Instance { get; private set; } = null!;

    public ICoreAPI? Api { get; private set; }
    public Compat? Compat { get; private set; }

    private IServerNetworkChannel? _serverChannel;

    public TentBag() {
        Instance = this;
    }

    public override void StartPre(ICoreAPI api) {
        Api = api;

        Compat = Compat.GetVersion();
    }

    public override void Start(ICoreAPI api) {
        api.RegisterItemClass("TentBag", typeof(ItemTentBag));
    }

    public override void StartClientSide(ICoreClientAPI api) {
        api.Network.RegisterChannel(Mod.Info.ModID)
            .RegisterMessageType<ErrorPacket>()
            .SetMessageHandler<ErrorPacket>(HandleErrorPacket);
    }

    public override void StartServerSide(ICoreServerAPI api) {
        Config.Reload();

        _serverChannel = api.Network.RegisterChannel(Mod.Info.ModID)
            .RegisterMessageType<ErrorPacket>();
    }

    public void SendClientError(IPlayer? player, string error) {
        if (player is IServerPlayer serverPlayer) {
            _serverChannel?.SendPacket(new ErrorPacket { Error = error }, serverPlayer);
        }
    }

    private void HandleErrorPacket(ErrorPacket packet) {
        if (!string.IsNullOrEmpty(packet.Error)) {
            (Api as ICoreClientAPI)?.TriggerIngameError(this, "error", packet.Error);
        }
    }

    public override void Dispose() {
        Config.Dispose();

        Api = null;
        _serverChannel = null;
    }
}
