using System.Net;
using System.Net.Sockets;
using RimworldTogether.GameServer.Core;
using RimworldTogether.GameServer.Managers;
using RimworldTogether.GameServer.Misc;
using RimworldTogether.Shared.Misc;
using RimworldTogether.Shared.Network;

namespace RimworldTogether.GameServer.Network
{
    public class Network
    {
        public List<Client> connectedClients = new List<Client>();
        private TcpListener server;
        private IPAddress localAddress = IPAddress.Parse(Program.serverConfig.IP);
        private int port = int.Parse(Program.serverConfig.Port);

        private Task? heartbeatTask;
        private Task? siteTickTask;

        public bool isServerOpen;
        public bool usingNewNetworking;

        // TODO fix this hack
        public PacketHandler PacketHandler { get; set; }

        public Network()
        {

        }

        public async Task ReadyServer(CancellationToken cancellationToken = default)
        {
            server = new TcpListener(localAddress, port);
            server.Start();
            isServerOpen = true;

            heartbeatTask = HeartbeatClients(cancellationToken);
            // TODO resolve circular dependency
            //siteTickTask = SiteManager.StartSiteTicker(cancellationToken);

            Logger.WriteToConsole("Type 'help' to get a list of available commands");
            Logger.WriteToConsole($"Listening for users at {localAddress}:{port}");
            Logger.WriteToConsole("Server launched");
            Titler.ChangeTitle(connectedClients.Count, int.Parse(Program.serverConfig.MaxPlayers));

            while (!cancellationToken.IsCancellationRequested)
            {
                await ListenForIncomingUsers(cancellationToken);
            }
        }

        private async Task ListenForIncomingUsers(CancellationToken cancellationToken = default)
        {
            var tcpClient = await server.AcceptTcpClientAsync(cancellationToken);

            Client newServerClient = new Client(tcpClient);

            if (Program.isClosing) newServerClient.disconnectFlag = true;
            else
            {
                if (connectedClients.ToArray().Count() >= int.Parse(Program.serverConfig.MaxPlayers))
                {
                    // TODO resolve circular dependency, network <-> usermanager_joinings.
                    UserManager_Joinings.SendLoginResponse(this, newServerClient, UserManager_Joinings.LoginResponse.ServerFull);
                    Logger.WriteToConsole($"[Warning] > Server Full", Logger.LogMode.Warning);
                }

                else
                {
                    connectedClients.Add(newServerClient);

                    Titler.ChangeTitle(connectedClients.Count, int.Parse(Program.serverConfig.MaxPlayers));

                    newServerClient.DataTask = ListenToClient(newServerClient, cancellationToken);

                    Logger.WriteToConsole($"[Connect] > {newServerClient.username} | {newServerClient.SavedIP}");
                }
            }
        }

        private async Task ListenToClient(Client client, CancellationToken cancellationToken = default)
        {
            try
            {
                while (!client.disconnectFlag)
                {
                    string? data = await client.streamReader.ReadLineAsync(cancellationToken);
                    if (data == null) break;

                    Packet receivedPacket = Serializer.SerializeToPacket(data);
                    if (receivedPacket == null) break;

                    try
                    {
                        // TODO resolve circular dependency
                        PacketHandler.HandlePacket(client, receivedPacket);
                    }
                    catch
                    {
                        // TODO resolve circular dependency
                        //responseShortcutManager.SendIllegalPacket(client, true);
                    }
                }
            }

            catch
            {
                client.disconnectFlag = true;

                return;
            }
        }

        public void SendData(Client client, Packet packet)
        {
            while (client.isBusy) Thread.Sleep(100);

            try
            {
                client.isBusy = true;

                client.streamWriter.WriteLine(Serializer.SerializeToString(packet));
                client.streamWriter.Flush();

                client.isBusy = false;
            }
            catch
            {
            }
        }

        public void KickClient(Client client)
        {
            try
            {
                connectedClients.Remove(client);
                client.tcp.Dispose();

                // TODO resolve circular dependency
                // UserManager.SendPlayerRecount();

                Titler.ChangeTitle(connectedClients.Count, int.Parse(Program.serverConfig.MaxPlayers));

                Logger.WriteToConsole($"[Disconnect] > {client.username} | {client.SavedIP}");
            }
            catch
            {
                Logger.WriteToConsole($"Error disconnecting user {client.username}, this will cause memory overhead", Logger.LogMode.Warning);
            }
        }

        private async Task HeartbeatClients(CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100, cancellationToken);

                Client[] actualClients = connectedClients.ToArray();
                foreach (Client client in actualClients)
                {
                    try
                    {
                        if (client.disconnectFlag || !CheckIfConnected(client))
                        {
                            KickClient(client);
                        }
                    }

                    catch
                    {
                        KickClient(client);
                    }
                }
            }
        }

        private bool CheckIfConnected(Client client)
        {
            if (!client.tcp.Connected) return false;
            else
            {
                if (client.tcp.Client.Poll(0, SelectMode.SelectRead))
                {
                    byte[] buff = new byte[1];
                    if (client.tcp.Client.Receive(buff, SocketFlags.Peek) == 0) return false;
                    else return true;
                }

                else return true;
            }
        }
    }
}