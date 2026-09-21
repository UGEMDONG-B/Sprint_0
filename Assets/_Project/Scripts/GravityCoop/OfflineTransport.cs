using System;
using Unity.Netcode;

namespace Sprint0.GravityCoop
{
    // Keep the authoritative Netcode simulation in one process without opening any sockets.
    public sealed class OfflineTransport : NetworkTransport
    {
        public override ulong ServerClientId => 0;
        public override void Initialize(NetworkManager networkManager = null) { }
        public override bool StartServer() => true;
        public override bool StartClient() => false;
        public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery) { }
        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
        {
            clientId = 0;
            payload = default;
            receiveTime = 0;
            return NetworkEvent.Nothing;
        }
        public override void DisconnectRemoteClient(ulong clientId) { }
        public override void DisconnectLocalClient() { }
        public override ulong GetCurrentRtt(ulong clientId) => 0;
        public override void Shutdown() { }
    }
}
