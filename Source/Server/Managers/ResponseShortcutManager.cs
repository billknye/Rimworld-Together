using RimworldTogether.GameServer.Managers.Actions;
using RimworldTogether.GameServer.Misc;
using RimworldTogether.GameServer.Network;
using RimworldTogether.Shared.JSON;
using RimworldTogether.Shared.Misc;
using RimworldTogether.Shared.Network;

namespace RimworldTogether.GameServer.Managers
{
    public class ResponseShortcutManager
    {
        private readonly Network.Network network;

        public ResponseShortcutManager(Network.Network network)
        {
            this.network = network;
        }

        public void SendIllegalPacket(Client client, bool broadcast = true)
        {
            Packet Packet = new Packet("IllegalActionPacket");
            network.SendData(client, Packet);
            client.disconnectFlag = true;

            if (broadcast) Logger.WriteToConsole($"[Illegal action] > {client.username} > {client.SavedIP}", Logger.LogMode.Error);
        }

        public void SendUnavailablePacket(Client client)
        {
            Packet packet = new Packet("UserUnavailablePacket");
            network.SendData(client, packet);
        }

        public void SendBreakPacket(Client client)
        {
            Packet packet = new Packet("BreakPacket");
            network.SendData(client, packet);
        }

        public void SendNoPowerPacket(Client client, FactionManifestJSON factionManifest)
        {
            factionManifest.manifestMode = ((int)FactionManager.FactionManifestMode.NoPower).ToString();

            string[] contents = new string[] { Serializer.SerializeToString(factionManifest) };
            Packet packet = new Packet("FactionPacket", contents);
            network.SendData(client, packet);
        }
    }
}
