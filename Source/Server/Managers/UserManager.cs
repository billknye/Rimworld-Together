using RimworldTogether.GameServer.Core;
using RimworldTogether.GameServer.Files;
using RimworldTogether.GameServer.Managers.Actions;
using RimworldTogether.GameServer.Misc;
using RimworldTogether.GameServer.Network;
using RimworldTogether.Shared.JSON;
using RimworldTogether.Shared.Misc;
using RimworldTogether.Shared.Network;

namespace RimworldTogether.GameServer.Managers
{
    public class UserManager
    {
        private readonly Network.Network network;
        private readonly UserManager_Joinings userManager_Joinings;

        public UserManager(Network.Network network, UserManager_Joinings userManager_Joinings)
        {
            this.network = network;
            this.userManager_Joinings = userManager_Joinings;
        }

        public static void LoadDataFromFile(Client client)
        {
            UserFile file = GetUserFile(client);
            client.uid = file.uid;
            client.username = file.username;
            client.password = file.password;
            client.factionName = file.factionName;
            client.hasFaction = file.hasFaction;
            client.isAdmin = file.isAdmin;
            client.isBanned = file.isBanned;
            client.enemyPlayers = file.enemyPlayers;
            client.allyPlayers = file.allyPlayers;

            Logger.WriteToConsole($"[Handshake] > {client.username} | {client.SavedIP}");
        }

        public static UserFile GetUserFile(Client client)
        {
            string[] userFiles = Directory.GetFiles(Program.usersPath);

            foreach (string userFile in userFiles)
            {
                UserFile file = Serializer.SerializeFromFile<UserFile>(userFile);
                if (file.username == client.username) return file;
            }

            return null;
        }

        public static UserFile GetUserFileFromName(string username)
        {
            string[] userFiles = Directory.GetFiles(Program.usersPath);

            foreach (string userFile in userFiles)
            {
                UserFile file = Serializer.SerializeFromFile<UserFile>(userFile);
                if (file.username == username) return file;
            }

            return null;
        }

        public static UserFile[] GetAllUserFiles()
        {
            List<UserFile> userFiles = new List<UserFile>();

            string[] paths = Directory.GetFiles(Program.usersPath);
            foreach (string path in paths) userFiles.Add(Serializer.SerializeFromFile<UserFile>(path));
            return userFiles.ToArray();
        }

        public static void SaveUserFile(Client client, UserFile userFile)
        {
            string savePath = Path.Combine(Program.usersPath, client.username + ".json");
            Serializer.SerializeToFile(savePath, userFile);
        }

        public static void SaveUserFileFromName(string username, UserFile userFile)
        {
            string savePath = Path.Combine(Program.usersPath, username + ".json");
            Serializer.SerializeToFile(savePath, userFile);
        }

        public void SendPlayerRecount()
        {
            PlayerRecountJSON playerRecountJSON = new PlayerRecountJSON();
            playerRecountJSON.currentPlayers = network.connectedClients.ToArray().Count().ToString();
            foreach (Client client in network.connectedClients.ToArray()) playerRecountJSON.currentPlayerNames.Add(client.username);

            string[] contents = new string[] { Serializer.SerializeToString(playerRecountJSON) };
            Packet packet = new Packet("PlayerRecountPacket", contents);
            foreach (Client client in network.connectedClients.ToArray()) network.SendData(client, packet);
        }

        public bool CheckIfUserIsConnected(string username)
        {
            List<Client> connectedClients = network.connectedClients.ToList();

            Client toGet = connectedClients.Find(x => x.username == username);
            if (toGet != null) return true;
            else return false;
        }

        public Client GetConnectedClientFromUsername(string username)
        {
            List<Client> connectedClients = network.connectedClients.ToList();
            return connectedClients.Find(x => x.username == username);
        }

        public bool CheckIfUserExists(Client client)
        {
            string[] existingUsers = Directory.GetFiles(Program.usersPath);

            foreach (string user in existingUsers)
            {
                UserFile existingUser = Serializer.SerializeFromFile<UserFile>(user);
                if (existingUser.username != client.username) continue;
                else
                {
                    if (existingUser.password == client.password) return true;
                    else
                    {
                        UserManager_Joinings.SendLoginResponse(network, client, UserManager_Joinings.LoginResponse.InvalidLogin);

                        return false;
                    }
                }
            }

            UserManager_Joinings.SendLoginResponse(network, client, UserManager_Joinings.LoginResponse.InvalidLogin);

            return false;
        }

        public bool CheckIfUserBanned(Client client)
        {
            if (!client.isBanned) return false;
            else
            {
                UserManager_Joinings.SendLoginResponse(network, client, UserManager_Joinings.LoginResponse.BannedLogin);
                return true;
            }
        }

        public static void SaveUserIP(Client client)
        {
            UserFile userFile = GetUserFile(client);
            userFile.SavedIP = client.SavedIP;
            SaveUserFile(client, userFile);
        }

        public static string[] GetUserStructuresTilesFromUsername(string username)
        {
            SettlementFile[] settlements = SettlementManager.GetAllSettlements().ToList().FindAll(x => x.owner == username).ToArray();
            SiteFile[] sites = SiteManager.GetAllSites().ToList().FindAll(x => x.owner == username).ToArray();

            List<string> tilesToExclude = new List<string>();
            foreach (SettlementFile settlement in settlements) tilesToExclude.Add(settlement.tile);
            foreach (SiteFile site in sites) tilesToExclude.Add(site.tile);

            return tilesToExclude.ToArray();
        }
    }

    public class UserManager_Joinings
    {
        private readonly Network.Network network;

        public enum CheckMode { Login, Register }

        public enum LoginResponse
        {
            InvalidLogin,
            BannedLogin,
            RegisterSuccess,
            RegisterInUse,
            RegisterError,
            ExtraLogin,
            WrongMods,
            ServerFull,
            Whitelist
        }

        public UserManager_Joinings(Network.Network network)
        {
            this.network = network;
        }

        public bool CheckLoginDetails(Client client, CheckMode mode)
        {
            bool isInvalid = false;
            if (string.IsNullOrWhiteSpace(client.username)) isInvalid = true;
            if (client.username.Any(Char.IsWhiteSpace)) isInvalid = true;
            if (string.IsNullOrWhiteSpace(client.password)) isInvalid = true;
            if (client.username.Length > 32) isInvalid = true;

            if (!isInvalid) return true;
            else
            {
                if (mode == CheckMode.Login) SendLoginResponse(network, client, LoginResponse.InvalidLogin);
                else if (mode == CheckMode.Register) SendLoginResponse(network, client, LoginResponse.RegisterError);
                return false;
            }
        }

        public static void SendLoginResponse(Network.Network network, Client client, LoginResponse response, object extraDetails = null)
        {
            LoginDetailsJSON loginDetailsJSON = new LoginDetailsJSON();
            loginDetailsJSON.tryResponse = ((int)response).ToString();

            if (response == LoginResponse.WrongMods) loginDetailsJSON.conflictingMods = (List<string>)extraDetails;

            string[] contents = new string[] { Serializer.SerializeToString(loginDetailsJSON) };
            Packet packet = new Packet("LoginResponsePacket", contents);
            network.SendData(client, packet);

            client.disconnectFlag = true;
        }

        public bool CheckWhitelist(Client client)
        {
            if (!Program.whitelist.UseWhitelist) return true;
            else
            {
                foreach (string str in Program.whitelist.WhitelistedUsers)
                {
                    if (str == client.username) return true;
                }
            }

            SendLoginResponse(network, client, LoginResponse.Whitelist);

            return false;
        }
    }
}
