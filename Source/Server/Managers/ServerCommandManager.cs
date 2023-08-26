using RimworldTogether.GameServer.Commands;
using RimworldTogether.GameServer.Core;
using RimworldTogether.GameServer.Files;
using RimworldTogether.GameServer.Misc;
using RimworldTogether.GameServer.Network;
using RimworldTogether.Shared.Misc;

namespace RimworldTogether.GameServer.Managers
{
    public class ServerCommandManager
    {
        public static string[] eventTypes = new string[]
        {
            "Raid",
            "Infestation",
            "MechCluster",
            "ToxicFallout",
            "Manhunter",
            "Wanderer",
            "FarmAnimals",
            "ShipChunks",
            "TraderCaravan"
        };

        public static string[] commandParameters;
        private readonly Network.Network network;
        private readonly CommandManager commandManager;
        private readonly SaveManager saveManager;
        private readonly ModManager modManager;

        public ServerCommandManager(Network.Network network, CommandManager commandManager,
            SaveManager saveManager,
            ModManager modManager)
        {
            this.network = network;
            this.commandManager = commandManager;
            this.saveManager = saveManager;
            this.modManager = modManager;
        }


        public void ParseServerCommands(string parsedString)
        {
            string parsedPrefix = parsedString.Split(' ')[0].ToLower();
            int parsedParameters = parsedString.Split(' ').Count() - 1;
            commandParameters = parsedString.Replace(parsedPrefix + " ", "").Split(" ");

            try
            {
                ServerCommand commandToFetch = ServerCommandStorage.serverCommands.ToList().Find(x => x.prefix == parsedPrefix);
                if (commandToFetch == null) Logger.WriteToConsole($"[ERROR] > Command '{parsedPrefix}' was not found", Logger.LogMode.Warning);
                else
                {
                    if (commandToFetch.parameters != parsedParameters && commandToFetch.parameters != -1)
                    {
                        Logger.WriteToConsole($"[ERROR] > Command '{commandToFetch.prefix}' wanted [{commandToFetch.parameters}] parameters "
                            + $"but was passed [{parsedParameters}]", Logger.LogMode.Warning);
                    }

                    else
                    {
                        if (commandToFetch.commandAction != null) commandToFetch.commandAction.Invoke(this);

                        else Logger.WriteToConsole($"[ERROR] > Command '{commandToFetch.prefix}' didn't have any action built in",
                            Logger.LogMode.Warning);
                    }
                }
            }
            catch (Exception e) { Logger.WriteToConsole($"[Error] > Couldn't parse command '{parsedPrefix}'. Reason: {e}", Logger.LogMode.Error); }
        }

        public void ListenForServerCommands()
        {
            bool interactiveConsole;

            try
            {
                if (Console.In.Peek() != -1) interactiveConsole = true;
                else interactiveConsole = false;
            }

            catch
            {
                interactiveConsole = false;
                Logger.WriteToConsole($"[Warning] > Couldn't found interactive console, disabling commands", Logger.LogMode.Warning);
            }

            if (interactiveConsole)
            {
                while (true)
                {
                    ParseServerCommands(Console.ReadLine());
                }
            }
            else Logger.WriteToConsole($"[Warning] > Couldn't found interactive console, disabling commands", Logger.LogMode.Warning);
        }

        public void HelpCommandAction()
        {
            Logger.WriteToConsole($"List of available commands: [{ServerCommandStorage.serverCommands.Count()}]", Logger.LogMode.Title, false);
            Logger.WriteToConsole("----------------------------------------", Logger.LogMode.Title, false);
            foreach (ServerCommand command in ServerCommandStorage.serverCommands)
            {
                Logger.WriteToConsole($"{command.prefix} - {command.description}", Logger.LogMode.Warning, writeToLogs: false);
            }
            Logger.WriteToConsole("----------------------------------------", Logger.LogMode.Title, false);
        }

        public void ListCommandAction()
        {
            Logger.WriteToConsole($"Connected players: [{network.connectedClients.ToArray().Count()}]", Logger.LogMode.Title, false);
            Logger.WriteToConsole("----------------------------------------", Logger.LogMode.Title, false);
            foreach (Client client in network.connectedClients.ToArray())
            {
                Logger.WriteToConsole($"{client.username} - {client.SavedIP}", Logger.LogMode.Warning, writeToLogs: false);
            }
            Logger.WriteToConsole("----------------------------------------", Logger.LogMode.Title, false);
        }

        public void DeepListCommandAction()
        {
            UserFile[] userFiles = UserManager.GetAllUserFiles();

            Logger.WriteToConsole($"Server players: [{userFiles.Count()}]", Logger.LogMode.Title, false);
            Logger.WriteToConsole("----------------------------------------", Logger.LogMode.Title, false);
            foreach (UserFile user in userFiles)
            {
                Logger.WriteToConsole($"{user.username} - {user.SavedIP}", Logger.LogMode.Warning, writeToLogs: false);
            }
            Logger.WriteToConsole("----------------------------------------", Logger.LogMode.Title, false);
        }

        public void OpCommandAction()
        {
            Client toFind = network.connectedClients.ToList().Find(x => x.username == ServerCommandManager.commandParameters[0]);
            if (toFind == null) Logger.WriteToConsole($"[ERROR] > User '{ServerCommandManager.commandParameters[0]}' was not found",
                Logger.LogMode.Warning);

            else
            {
                if (CheckIfIsAlready(toFind)) return;
                else
                {
                    toFind.isAdmin = true;

                    UserFile userFile = UserManager.GetUserFile(toFind);
                    userFile.isAdmin = true;
                    UserManager.SaveUserFile(toFind, userFile);

                    commandManager.SendOpCommand(toFind);

                    Logger.WriteToConsole($"User '{ServerCommandManager.commandParameters[0]}' has now admin privileges",
                        Logger.LogMode.Warning);
                }
            }

            bool CheckIfIsAlready(Client client)
            {
                if (client.isAdmin)
                {
                    Logger.WriteToConsole($"[ERROR] > User '{client.username}' " +
                    $"was already an admin", Logger.LogMode.Warning);
                    return true;
                }

                else return false;
            }
        }

        public void DeopCommandAction()
        {
            Client toFind = network.connectedClients.ToList().Find(x => x.username == ServerCommandManager.commandParameters[0]);
            if (toFind == null) Logger.WriteToConsole($"[ERROR] > User '{ServerCommandManager.commandParameters[0]}' was not found",
                Logger.LogMode.Warning);

            else
            {
                if (CheckIfIsAlready(toFind)) return;
                else
                {
                    toFind.isAdmin = false;

                    UserFile userFile = UserManager.GetUserFile(toFind);
                    userFile.isAdmin = false;
                    UserManager.SaveUserFile(toFind, userFile);

                    commandManager.SendDeOpCommand(toFind);

                    Logger.WriteToConsole($"User '{toFind.username}' is no longer an admin",
                        Logger.LogMode.Warning);
                }
            }

            bool CheckIfIsAlready(Client client)
            {
                if (!client.isAdmin)
                {
                    Logger.WriteToConsole($"[ERROR] > User '{client.username}' " +
                    $"was not an admin", Logger.LogMode.Warning);
                    return true;
                }

                else return false;
            }
        }

        public void KickCommandAction()
        {
            Client toFind = network.connectedClients.ToList().Find(x => x.username == ServerCommandManager.commandParameters[0]);
            if (toFind == null) Logger.WriteToConsole($"[ERROR] > User '{ServerCommandManager.commandParameters[0]}' was not found",
                Logger.LogMode.Warning);

            else
            {
                toFind.disconnectFlag = true;

                Logger.WriteToConsole($"User '{ServerCommandManager.commandParameters[0]}' has been kicked from the server",
                    Logger.LogMode.Warning);
            }
        }

        public void BanCommandAction()
        {
            Client toFind = network.connectedClients.ToList().Find(x => x.username == ServerCommandManager.commandParameters[0]);
            if (toFind == null)
            {
                UserFile userFile = UserManager.GetUserFileFromName(ServerCommandManager.commandParameters[0]);
                if (userFile == null) Logger.WriteToConsole($"[ERROR] > User '{ServerCommandManager.commandParameters[0]}' was not found",
                    Logger.LogMode.Warning);

                else
                {
                    if (CheckIfIsAlready(userFile)) return;
                    else
                    {
                        userFile.isBanned = true;
                        UserManager.SaveUserFileFromName(userFile.username, userFile);

                        Logger.WriteToConsole($"User '{ServerCommandManager.commandParameters[0]}' has been banned from the server",
                            Logger.LogMode.Warning);
                    }
                }
            }

            else
            {
                commandManager.SendBanCommand(toFind);

                toFind.disconnectFlag = true;

                UserFile userFile = UserManager.GetUserFile(toFind);
                userFile.isBanned = true;
                UserManager.SaveUserFile(toFind, userFile);

                Logger.WriteToConsole($"User '{ServerCommandManager.commandParameters[0]}' has been banned from the server",
                    Logger.LogMode.Warning);
            }

            bool CheckIfIsAlready(UserFile userFile)
            {
                if (userFile.isBanned)
                {
                    Logger.WriteToConsole($"[ERROR] > User '{ServerCommandManager.commandParameters[0]}' " +
                    $"was already banned from the server", Logger.LogMode.Warning);
                    return true;
                }

                else return false;
            }
        }

        public void BanListCommandAction()
        {
            List<UserFile> userFiles = UserManager.GetAllUserFiles().ToList().FindAll(x => x.isBanned);

            Logger.WriteToConsole($"Banned players: [{userFiles.Count()}]", Logger.LogMode.Title, false);
            Logger.WriteToConsole("----------------------------------------", Logger.LogMode.Title, false);
            foreach (UserFile user in userFiles)
            {
                Logger.WriteToConsole($"{user.username} - {user.SavedIP}", Logger.LogMode.Warning, writeToLogs: false);
            }
            Logger.WriteToConsole("----------------------------------------", Logger.LogMode.Title, false);
        }

        public void PardonCommandAction()
        {
            UserFile userFile = UserManager.GetUserFileFromName(ServerCommandManager.commandParameters[0]);
            if (userFile == null) Logger.WriteToConsole($"[ERROR] > User '{ServerCommandManager.commandParameters[0]}' was not found",
                Logger.LogMode.Warning);

            else
            {
                if (CheckIfIsAlready(userFile)) return;
                else
                {
                    userFile.isBanned = false;
                    UserManager.SaveUserFileFromName(userFile.username, userFile);

                    Logger.WriteToConsole($"User '{ServerCommandManager.commandParameters[0]}' is no longer banned from the server",
                        Logger.LogMode.Warning);
                }
            }

            bool CheckIfIsAlready(UserFile userFile)
            {
                if (!userFile.isBanned)
                {
                    Logger.WriteToConsole($"[ERROR] > User '{ServerCommandManager.commandParameters[0]}' " +
                    $"was not banned from the server", Logger.LogMode.Warning);
                    return true;
                }

                else return false;
            }
        }

        public void ReloadCommandAction()
        {
            Program.LoadResources(modManager);
        }

        public void ModListCommandAction()
        {
            Logger.WriteToConsole($"Required Mods: [{modManager.LoadedRequiredMods.Count()}]", Logger.LogMode.Title, false);
            Logger.WriteToConsole("----------------------------------------", Logger.LogMode.Title, false);
            foreach (string str in modManager.LoadedRequiredMods)
            {
                Logger.WriteToConsole($"{str}", Logger.LogMode.Warning, writeToLogs: false);
            }
            Logger.WriteToConsole("----------------------------------------", Logger.LogMode.Title, false);

            Logger.WriteToConsole($"Optional Mods: [{modManager.LoadedOptionalMods.Count()}]", Logger.LogMode.Title, false);
            Logger.WriteToConsole("----------------------------------------", Logger.LogMode.Title, false);
            foreach (string str in modManager.LoadedOptionalMods)
            {
                Logger.WriteToConsole($"{str}", Logger.LogMode.Warning, writeToLogs: false);
            }
            Logger.WriteToConsole("----------------------------------------", Logger.LogMode.Title, false);

            Logger.WriteToConsole($"Forbidden Mods: [{modManager.LoadedForbiddenMods.Count()}]", Logger.LogMode.Title, false);
            Logger.WriteToConsole("----------------------------------------", Logger.LogMode.Title, false);
            foreach (string str in modManager.LoadedForbiddenMods)
            {
                Logger.WriteToConsole($"{str}", Logger.LogMode.Warning, writeToLogs: false);
            }
            Logger.WriteToConsole("----------------------------------------", Logger.LogMode.Title, false);
        }

        public void EventCommandAction()
        {
            Client toFind = network.connectedClients.ToList().Find(x => x.username == ServerCommandManager.commandParameters[0]);
            if (toFind == null) Logger.WriteToConsole($"[ERROR] > User '{ServerCommandManager.commandParameters[0]}' was not found",
                Logger.LogMode.Warning);

            else
            {
                for (int i = 0; i < ServerCommandManager.eventTypes.Count(); i++)
                {
                    if (ServerCommandManager.eventTypes[i] == ServerCommandManager.commandParameters[1])
                    {
                        commandManager.SendEventCommand(toFind, i);

                        Logger.WriteToConsole($"Sent event '{ServerCommandManager.commandParameters[1]}' to {toFind.username}",
                            Logger.LogMode.Warning);

                        return;
                    }
                }

                Logger.WriteToConsole($"[ERROR] > Event '{ServerCommandManager.commandParameters[1]}' was not found",
                    Logger.LogMode.Warning);
            }
        }

        public void EventAllCommandAction()
        {
            for (int i = 0; i < ServerCommandManager.eventTypes.Count(); i++)
            {
                if (ServerCommandManager.eventTypes[i] == ServerCommandManager.commandParameters[0])
                {
                    foreach (Client client in network.connectedClients.ToArray())
                    {
                        commandManager.SendEventCommand(client, i);
                    }

                    Logger.WriteToConsole($"Sent event '{ServerCommandManager.commandParameters[0]}' to every connected player",
                        Logger.LogMode.Title);

                    return;
                }
            }

            Logger.WriteToConsole($"[ERROR] > Event '{ServerCommandManager.commandParameters[0]}' was not found",
                    Logger.LogMode.Warning);
        }

        public void EventListCommandAction()
        {
            Logger.WriteToConsole($"Available events: [{ServerCommandManager.eventTypes.Count()}]", Logger.LogMode.Title, false);
            Logger.WriteToConsole("----------------------------------------", Logger.LogMode.Title, false);
            foreach (string str in ServerCommandManager.eventTypes)
            {
                Logger.WriteToConsole($"{str}", Logger.LogMode.Warning, writeToLogs: false);
            }
            Logger.WriteToConsole("----------------------------------------", Logger.LogMode.Title, false);
        }

        public void BroadcastCommandAction()
        {
            string fullText = "";
            foreach (string str in ServerCommandManager.commandParameters)
            {
                fullText += $"{str} ";
            }
            fullText = fullText.Remove(fullText.Length - 1, 1);

            commandManager.SendBroadcastCommand(fullText);

            Logger.WriteToConsole($"Sent broadcast '{fullText}'", Logger.LogMode.Title);
        }

        public void WhitelistCommandAction()
        {
            Logger.WriteToConsole($"Whitelisted usernames: [{Program.whitelist.WhitelistedUsers.Count()}]", Logger.LogMode.Title, false);
            Logger.WriteToConsole("----------------------------------------", Logger.LogMode.Title, false);
            foreach (string str in Program.whitelist.WhitelistedUsers)
            {
                Logger.WriteToConsole($"{str}", Logger.LogMode.Warning, writeToLogs: false);
            }
            Logger.WriteToConsole("----------------------------------------", Logger.LogMode.Title, false);
        }

        public void WhitelistAddCommandAction()
        {
            UserFile userFile = UserManager.GetUserFileFromName(ServerCommandManager.commandParameters[0]);
            if (userFile == null) Logger.WriteToConsole($"[ERROR] > User '{ServerCommandManager.commandParameters[0]}' was not found",
                Logger.LogMode.Warning);

            else
            {
                if (CheckIfIsAlready(userFile)) return;
                else WhitelistManager.AddUserToWhitelist(ServerCommandManager.commandParameters[0]);
            }

            bool CheckIfIsAlready(UserFile userFile)
            {
                if (Program.whitelist.WhitelistedUsers.Contains(userFile.username))
                {
                    Logger.WriteToConsole($"[ERROR] > User '{ServerCommandManager.commandParameters[0]}' " +
                        $"was already whitelisted", Logger.LogMode.Warning);

                    return true;
                }

                else return false;
            }
        }

        public void WhitelistRemoveCommandAction()
        {
            UserFile userFile = UserManager.GetUserFileFromName(ServerCommandManager.commandParameters[0]);
            if (userFile == null) Logger.WriteToConsole($"[ERROR] > User '{ServerCommandManager.commandParameters[0]}' was not found",
                Logger.LogMode.Warning);

            else
            {
                if (CheckIfIsAlready(userFile)) return;
                else WhitelistManager.RemoveUserFromWhitelist(ServerCommandManager.commandParameters[0]);
            }

            bool CheckIfIsAlready(UserFile userFile)
            {
                if (!Program.whitelist.WhitelistedUsers.Contains(userFile.username))
                {
                    Logger.WriteToConsole($"[ERROR] > User '{ServerCommandManager.commandParameters[0]}' " +
                        $"was not whitelisted", Logger.LogMode.Warning);

                    return true;
                }

                else return false;
            }
        }

        public void WhitelistToggleCommandAction()
        {
            WhitelistManager.ToggleWhitelist();
        }

        public void ForceSaveCommandAction()
        {
            Client toFind = network.connectedClients.ToList().Find(x => x.username == ServerCommandManager.commandParameters[0]);
            if (toFind == null) Logger.WriteToConsole($"[ERROR] > User '{ServerCommandManager.commandParameters[0]}' was not found",
                Logger.LogMode.Warning);

            else
            {
                commandManager.SendForceSaveCommand(toFind);

                Logger.WriteToConsole($"User '{ServerCommandManager.commandParameters[0]}' has been forced to save",
                    Logger.LogMode.Warning);
            }
        }

        public void DeletePlayerCommandAction()
        {
            UserFile userFile = UserManager.GetUserFileFromName(ServerCommandManager.commandParameters[0]);
            if (userFile == null) Logger.WriteToConsole($"[ERROR] > User '{ServerCommandManager.commandParameters[0]}' was not found",
                Logger.LogMode.Warning);

            else saveManager.DeletePlayerDetails(userFile.username);
        }

        public void EnableDifficultyCommandAction()
        {
            if (Program.difficultyValues.UseCustomDifficulty == true)
            {
                Logger.WriteToConsole($"[ERROR] > Custom difficulty was already enabled", Logger.LogMode.Warning);
            }

            else
            {
                Program.difficultyValues.UseCustomDifficulty = true;
                CustomDifficultyManager.SaveCustomDifficulty(Program.difficultyValues);

                Logger.WriteToConsole($"Custom difficulty is now enabled", Logger.LogMode.Warning);
            }
        }

        public void DisableDifficultyCommandAction()
        {
            if (Program.difficultyValues.UseCustomDifficulty == false)
            {
                Logger.WriteToConsole($"[ERROR] > Custom difficulty was already disabled", Logger.LogMode.Warning);
            }

            else
            {
                Program.difficultyValues.UseCustomDifficulty = false;
                CustomDifficultyManager.SaveCustomDifficulty(Program.difficultyValues);

                Logger.WriteToConsole($"Custom difficulty is now disabled", Logger.LogMode.Warning);
            }
        }

        public void LockSaveCommandAction()
        {
            //TODO
            //Compression is different for client and server, causing saves to become useless after executing this
            return;

            byte[] saveFile = SaveManager.GetUserSaveFromUsername(ServerCommandManager.commandParameters[0]);

            if (saveFile == null)
            {
                Logger.WriteToConsole($"[ERROR] > Save {ServerCommandManager.commandParameters[0]} was not found", Logger.LogMode.Warning);
            }

            else
            {
                byte[] lockedBytes = GZip.CompressDefault(saveFile);

                File.WriteAllBytes(Path.Combine(Program.savesPath, ServerCommandManager.commandParameters[0] + ".mpsave"), lockedBytes);

                Logger.WriteToConsole($"Save {ServerCommandManager.commandParameters[0]} has been locked");
            }
        }

        public void UnlockSaveCommandAction()
        {
            //TODO
            //Compression is different for client and server, causing saves to become useless after executing this
            return;

            byte[] saveFile = SaveManager.GetUserSaveFromUsername(ServerCommandManager.commandParameters[0]);

            if (saveFile == null)
            {
                Logger.WriteToConsole($"[ERROR] > Save {ServerCommandManager.commandParameters[0]} was not found", Logger.LogMode.Warning);
            }

            else
            {
                byte[] unlockedBytes = GZip.DecompressDefault(saveFile);

                File.WriteAllBytes(Path.Combine(Program.savesPath, ServerCommandManager.commandParameters[0] + ".mpsave"), unlockedBytes);

                Logger.WriteToConsole($"Save {ServerCommandManager.commandParameters[0]} has been unlocked");
            }
        }

        public void QuitCommandAction()
        {
            Program.isClosing = true;

            Logger.WriteToConsole($"Waiting for all saves to quit", Logger.LogMode.Warning);

            foreach (Client client in network.connectedClients.ToArray())
            {
                commandManager.SendForceSaveCommand(client);
            }

            while (network.connectedClients.ToArray().Length > 0)
            {
                Thread.Sleep(1);
            }

            Environment.Exit(0);
        }

        public void ForceQuitCommandAction() { Environment.Exit(0); }

        public void ClearCommandAction()
        {
            Console.Clear();

            Logger.WriteToConsole("[Cleared console]", Logger.LogMode.Title);
        }
    }


    public static class ServerCommandStorage
    {
        private static ServerCommand helpCommand = new ServerCommand("help", 0,
            "Shows a list of all available commands to use",
            n => n.HelpCommandAction());

        private static ServerCommand listCommand = new ServerCommand("list", 0,
            "Shows all connected players",
            n => n.ListCommandAction());

        private static ServerCommand opCommand = new ServerCommand("op", 1,
            "Gives admin privileges to the selected player",
            n => n.OpCommandAction());

        private static ServerCommand deopCommand = new ServerCommand("deop", 1,
            "Removes admin privileges from the selected player",
            n => n.DeopCommandAction());

        private static ServerCommand kickCommand = new ServerCommand("kick", 1,
            "Kicks the selected player from the server",
            n => n.KickCommandAction());

        private static ServerCommand banCommand = new ServerCommand("ban", 1,
            "Bans the selected player from the server",
            n => n.BanCommandAction());

        private static ServerCommand pardonCommand = new ServerCommand("pardon", 1,
            "Pardons the selected player from the server",
            n => n.PardonCommandAction());

        private static ServerCommand deepListCommand = new ServerCommand("deeplist", 0,
            "Shows a list of all server players",
            n => n.DeepListCommandAction());

        private static ServerCommand banListCommand = new ServerCommand("banlist", 0,
            "Shows a list of all banned server players",
            n => n.BanListCommandAction());

        private static ServerCommand reloadCommand = new ServerCommand("reload", 0,
            "Reloads all server resources",
            n => n.ReloadCommandAction());

        private static ServerCommand modListCommand = new ServerCommand("modlist", 0,
            "Shows all currently loaded mods",
            n => n.ModListCommandAction());

        private static ServerCommand eventCommand = new ServerCommand("event", 2,
            "Sends a command to the selecter players",
            n => n.EventCommandAction());

        private static ServerCommand eventAllCommand = new ServerCommand("eventall", 1,
            "Sends a command to all connected players",
            n => n.EventAllCommandAction());

        private static ServerCommand eventListCommand = new ServerCommand("eventlist", 0,
            "Shows a list of all available events to use",
            n => n.EventListCommandAction());

        private static ServerCommand broadcastCommand = new ServerCommand("broadcast", -1,
            "Broadcast a message to all connected players",
            n => n.BroadcastCommandAction());

        private static ServerCommand clearCommand = new ServerCommand("clear", 0,
            "Clears the console output",
            n => n.ClearCommandAction());

        private static ServerCommand whitelistCommand = new ServerCommand("whitelist", 0,
            "Shows all whitelisted players",
            n => n.WhitelistCommandAction());

        private static ServerCommand whitelistAddCommand = new ServerCommand("whitelistadd", 1,
            "Adds a player to the whitelist",
            n => n.WhitelistAddCommandAction());

        private static ServerCommand whitelistRemoveCommand = new ServerCommand("whitelistremove", 1,
            "Removes a player from the whitelist",
            n => n.WhitelistRemoveCommandAction());

        private static ServerCommand whitelistToggleCommand = new ServerCommand("togglewhitelist", 0,
            "Toggles the whitelist ON or OFF",
            n => n.WhitelistToggleCommandAction());

        private static ServerCommand forceSaveCommand = new ServerCommand("forcesave", 1,
            "Forces a player to sync their save",
            n => n.ForceSaveCommandAction());

        private static ServerCommand deletePlayerCommand = new ServerCommand("deleteplayer", 1,
            "Deletes all data of a player",
            n => n.DeletePlayerCommandAction());

        private static ServerCommand enableDifficultyCommand = new ServerCommand("enabledifficulty", 0,
            "Locks an editable save for use [WIP]",
            n => n.EnableDifficultyCommandAction());

        private static ServerCommand disableDifficultyCommand = new ServerCommand("disabledifficulty", 0,
            "Locks an editable save for use [WIP]",
            n => n.DisableDifficultyCommandAction());

        private static ServerCommand lockSaveCommand = new ServerCommand("locksave", 1,
            "Locks an editable save for use [WIP]",
            n => n.LockSaveCommandAction());

        private static ServerCommand unlockSaveCommand = new ServerCommand("unlocksave", 1,
            "Unlocks a save file for editing [WIP]",
            n => n.UnlockSaveCommandAction());

        private static ServerCommand quitCommand = new ServerCommand("quit", 0,
            "Saves all player details and then closes the server",
            n => n.QuitCommandAction());

        private static ServerCommand forceQuitCommand = new ServerCommand("forcequit", 0,
            "Closes the server without saving player details",
            n => n.ForceQuitCommandAction());

        public static ServerCommand[] serverCommands = new ServerCommand[]
        {
            helpCommand,
            listCommand,
            deepListCommand,
            opCommand,
            deopCommand,
            kickCommand,
            banCommand,
            banListCommand,
            pardonCommand,
            reloadCommand,
            modListCommand,
            eventCommand,
            eventAllCommand,
            eventListCommand,
            broadcastCommand,
            whitelistCommand,
            whitelistAddCommand,
            whitelistRemoveCommand,
            whitelistToggleCommand,
            clearCommand,
            forceSaveCommand,
            deletePlayerCommand,
            enableDifficultyCommand,
            disableDifficultyCommand,
            //lockSaveCommand,
            //unlockSaveCommand,
            quitCommand,
            forceQuitCommand
        };
    }
}
