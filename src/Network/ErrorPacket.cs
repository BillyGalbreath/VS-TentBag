using ProtoBuf;

namespace TentBag.Network;

[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public sealed class ErrorPacket : Packet {
    public string? Error = null;
}
