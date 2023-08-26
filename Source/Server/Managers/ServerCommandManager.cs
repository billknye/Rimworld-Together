using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RimworldTogether.GameServer.Commands;
using RimworldTogether.GameServer.Core;
using RimworldTogether.GameServer.Files;
using RimworldTogether.GameServer.Network;
using RimworldTogether.Shared.Misc;

namespace RimworldTogether.GameServer.Managers;

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
    private readonly ILogger<ServerCommandManager> logger;
    private readonly Network.Network network;
    private readonly CommandManager commandManager;
    private readonly SaveManager saveManager;
    private readonly ModManager modManager;
    private readonly WorldManager worldManager;
    private readonly CustomDifficultyManager customDifficultyManager;
    private readonly WhitelistManager whitelistManager;
    private readonly IHostApplicationLifetime hostApplicationLifetime;

    private Task? quitTask;

    public ServerCommandManager(
        ILogger<ServerCommandManager> logger,
        Network.Network network,
        CommandManager commandManager,
        SaveManager saveManager,
        ModManager modManager,
        WorldManager worldManager,
        CustomDifficultyManager customDifficultyManager,
        WhitelistManager whitelistManager,
        IHostApplicationLifetime hostApplicationLifetime)
    {
        this.logger = logger;
        this.network = network;
        this.commandManager = commandManager;
        this.saveManager = saveManager;
        this.modManager = modManager;
        this.worldManager = worldManager;
        this.customDifficultyManager = customDifficultyManager;
        this.whitelistManager = whitelistManager;
        this.hostApplicationLifetime = hostApplicationLifetime;
    }


    public void ParseServerCommands(string parsedString)
    {
        string parsedPrefix = parsedString.Split(' ')[0].ToLower();
        int parsedParameters = parsedString.Split(' ').Count() - 1;
        commandParameters = parsedString.Replace(parsedPrefix + " ", "").Split(" ");

        try
        {
            ServerCommand commandToFetch = ServerCommandStorage.serverCommands.ToList().Find(x => x.prefix == parsedPrefix);
            if (commandToFetch == null) logger.LogWarning($"[ERROR] > Command '{parsedPrefix}' was not found");
            else
            {
                if (commandToFetch.parameters != parsedParameters && commandToFetch.parameters != -1)
                {
                    logger.LogWarning($"[ERROR] > Command '{commandToFetch.prefix}' wanted [{commandToFetch.parameters}] parameters "
                        + $"but was passed [{parsedParameters}]");
                }

                else
                {
                    if (commandToFetch.commandAction != null) commandToFetch.commandAction.Invoke(this);

                    else logger.LogInformation($"[ERROR] > Command '{commandToFetch.prefix}' didn't have any action built in");
                }
            }
        }
        catch (Exception e) { logger.LogError(e, $"[Error] > Couldn't parse command '{parsedPrefix}'."); }
    }

    public async Task ListenForServerCommands(CancellationToken cancellationToken = default)
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
            logger.LogWarning($"[Warning] > Couldn't found interactive console, disabling commands");
        }

        if (!interactiveConsole)
        {
            logger.LogWarning($"[Warning] > Couldn't found interactive console, disabling commands");
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await Console.In.ReadLineAsync(cancellationToken);
                ParseServerCommands(result);
            }
            catch (TaskCanceledException)
            {
                logger.LogInformation("Server Commands Stopped");
                break;
            }
        }
    }

    public void HelpCommandAction()
    {
        logger.LogInformation($"List of available commands: [{ServerCommandStorage.serverCommands.Count()}]");
        logger.LogInformation("----------------------------------------");
        foreach (ServerCommand command in ServerCommandStorage.serverCommands)
        {
            logger.LogInformation($"{command.prefix} - {command.description}");
        }

        logger.LogInformation("----------------------------------------");
    }

    public void ListCommandAction()
    {
        logger.LogInformation($"Connected players: [{network.connectedClients.ToArray().Count()}]");
        logger.LogInformation("----------------------------------------");
        foreach (Client client in network.connectedClients.ToArray())
        {
            logger.LogInformation($"{client.username} - {client.SavedIP}");
        }

        logger.LogInformation("----------------------------------------");
    }

    public void DeepListCommandAction()
    {
        UserFile[] userFiles = UserManager.GetAllUserFiles();

        logger.LogInformation($"Server players: [{userFiles.Count()}]");
        logger.LogInformation("----------------------------------------");
        foreach (UserFile user in userFiles)
        {
            logger.LogInformation($"{user.username} - {user.SavedIP}");
        }
        logger.LogInformation("----------------------------------------");
    }

    public void OpCommandAction()
    {
        Client toFind = network.connectedClients.ToList().Find(x => x.username == ServerCommandManager.commandParameters[0]);
        if (toFind == null) logger.LogWarning($"[ERROR] > User '{ServerCommandManager.commandParameters[0]}' was not found");

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

                logger.LogWarning($"User '{ServerCommandManager.commandParameters[0]}' has now admin privileges");
            }
        }

        bool CheckIfIsAlready(Client client)
        {
            if (client.isAdmin)
            {
                logger.LogWarning($"[ERROR] > User '{client.username}' was already an admin");
                return true;
            }

            else return false;
        }
    }

    public void DeopCommandAction()
    {
        Client toFind = network.connectedClients.ToList().Find(x => x.username == ServerCommandManager.commandParameters[0]);
        if (toFind == null) logger.LogWarning($"[ERROR] > User '{ServerCommandManager.commandParameters[0]}' was not found");

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

                logger.LogWarning($"User '{toFind.username}' is no longer an admin");
            }
        }

        bool CheckIfIsAlready(Client client)
        {
            if (!client.isAdmin)
            {
                logger.LogWarning($"[ERROR] > User '{client.username}' was not an admin");
                return true;
            }

            else return false;
        }
    }

    public void KickCommandAction()
    {
        Client toFind = network.connectedClients.ToList().Find(x => x.username == ServerCommandManager.commandParameters[0]);
        if (toFind == null) logger.LogWarning($"[ERROR] > User '{ServerCommandManager.commandParameters[0]}' was not found");

        else
        {
            toFind.disconnectFlag = true;

            logger.LogWarning($"User '{ServerCommandManager.commandParameters[0]}' has been kicked from the server");
        }
    }

    public void BanCommandAction()
    {
        Client toFind = network.connectedClients.ToList().Find(x => x.username == ServerCommandManager.commandParameters[0]);
        if (toFind == null)
        {
            UserFile userFile = UserManager.GetUserFileFromName(ServerCommandManager.commandParameters[0]);
            if (userFile == null) logger.LogWarning($"[ERROR] > User '{ServerCommandManager.commandParameters[0]}' was not found");

            else
            {
                if (CheckIfIsAlready(userFile)) return;
                else
                {
                    userFile.isBanned = true;
                    UserManager.SaveUserFileFromName(userFile.username, userFile);

                    logger.LogWarning($"User '{ServerCommandManager.commandParameters[0]}' has been banned from the server");
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

            logger.LogWarning($"User '{ServerCommandManager.commandParameters[0]}' has been banned from the server");
        }

        bool CheckIfIsAlready(UserFile userFile)
        {
            if (userFile.isBanned)
            {
                logger.LogWarning($"[ERROR] > User '{ServerCommandManager.commandParameters[0]}' " +
                    $"was already banned from the server");

                return true;
            }

            else return false;
        }
    }

    public void BanListCommandAction()
    {
        List<UserFile> userFiles = UserManager.GetAllUserFiles().ToList().FindAll(x => x.isBanned);

        logger.LogInformation($"Banned players: [{userFiles.Count()}]");
        logger.LogInformation("----------------------------------------");
        foreach (UserFile user in userFiles)
        {
            logger.LogWarning($"{user.username} - {user.SavedIP}");
        }

        logger.LogInformation("----------------------------------------");
    }

    public void PardonCommandAction()
    {
        UserFile userFile = UserManager.GetUserFileFromName(ServerCommandManager.commandParameters[0]);
        if (userFile == null) logger.LogWarning($"[ERROR] > User '{ServerCommandManager.commandParameters[0]}' was not found");

        else
        {
            if (CheckIfIsAlready(userFile)) return;
            else
            {
                userFile.isBanned = false;
                UserManager.SaveUserFileFromName(userFile.username, userFile);

                logger.LogWarning($"User '{ServerCommandManager.commandParameters[0]}' is no longer banned from the server");
            }
        }

        bool CheckIfIsAlready(UserFile userFile)
        {
            if (!userFile.isBanned)
            {
                logger.LogWarning($"[ERROR] > User '{ServerCommandManager.commandParameters[0]}' " +
                    $"was not banned from the server");

                return true;
            }

            else return false;
        }
    }

    public void ReloadCommandAction()
    {
        Program.LoadResources(logger, modManager, network, worldManager, customDifficultyManager, whitelistManager);
    }

    public void ModListCommandAction()
    {
        logger.LogInformation($"Required Mods: [{modManager.LoadedRequiredMods.Count()}]");
        logger.LogInformation("----------------------------------------");

        foreach (string str in modManager.LoadedRequiredMods)
        {
            logger.LogWarning($"{str}");
        }
        logger.LogInformation("----------------------------------------");

        logger.LogInformation($"Optional Mods: [{modManager.LoadedOptionalMods.Count()}]");
        logger.LogInformation("----------------------------------------");
        foreach (string str in modManager.LoadedOptionalMods)
        {
            logger.LogWarning($"{str}");
        }

        logger.LogInformation("----------------------------------------");

        logger.LogInformation($"Forbidden Mods: [{modManager.LoadedForbiddenMods.Count()}]");
        logger.LogInformation("----------------------------------------");

        foreach (string str in modManager.LoadedForbiddenMods)
        {
            logger.LogWarning($"{str}");
        }

        logger.LogInformation("----------------------------------------");
    }

    public void EventCommandAction()
    {
        Client toFind = network.connectedClients.ToList().Find(x => x.username == ServerCommandManager.commandParameters[0]);
        if (toFind == null) logger.LogWarning($"[ERROR] > User '{ServerCommandManager.commandParameters[0]}' was not found");

        else
        {
            for (int i = 0; i < ServerCommandManager.eventTypes.Count(); i++)
            {
                if (ServerCommandManager.eventTypes[i] == ServerCommandManager.commandParameters[1])
                {
                    commandManager.SendEventCommand(toFind, i);

                    logger.LogWarning($"Sent event '{ServerCommandManager.commandParameters[1]}' to {toFind.username}");

                    return;
                }
            }

            logger.LogWarning($"[ERROR] > Event '{ServerCommandManager.commandParameters[1]}' was not found");
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

                logger.LogInformation($"Sent event '{ServerCommandManager.commandParameters[0]}' to every connected player");

                return;
            }
        }

        logger.LogWarning($"[ERROR] > Event '{ServerCommandManager.commandParameters[0]}' was not found");
    }

    public void EventListCommandAction()
    {
        logger.LogInformation($"Available events: [{ServerCommandManager.eventTypes.Count()}]");
        logger.LogInformation("----------------------------------------");
        foreach (string str in ServerCommandManager.eventTypes)
        {
            logger.LogWarning($"{str}");
        }
        logger.LogInformation("----------------------------------------");
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

        logger.LogInformation($"Sent broadcast '{fullText}'");
    }

    public void WhitelistCommandAction()
    {
        logger.LogInformation($"Whitelisted usernames: [{Program.whitelist.WhitelistedUsers.Count()}]");
        logger.LogInformation("----------------------------------------");

        foreach (string str in Program.whitelist.WhitelistedUsers)
        {
            logger.LogWarning($"{str}");
        }

        logger.LogInformation("----------------------------------------");
    }

    public void WhitelistAddCommandAction()
    {
        UserFile userFile = UserManager.GetUserFileFromName(ServerCommandManager.commandParameters[0]);
        if (userFile == null) logger.LogWarning($"[ERROR] > User '{ServerCommandManager.commandParameters[0]}' was not found");

        else
        {
            if (CheckIfIsAlready(userFile)) return;
            else whitelistManager.AddUserToWhitelist(ServerCommandManager.commandParameters[0]);
        }

        bool CheckIfIsAlready(UserFile userFile)
        {
            if (Program.whitelist.WhitelistedUsers.Contains(userFile.username))
            {
                logger.LogWarning($"[ERROR] > User '{ServerCommandManager.commandParameters[0]}' " +
                    $"was already whitelisted");

                return true;
            }

            else return false;
        }
    }

    public void WhitelistRemoveCommandAction()
    {
        UserFile userFile = UserManager.GetUserFileFromName(ServerCommandManager.commandParameters[0]);
        if (userFile == null) logger.LogWarning($"[ERROR] > User '{ServerCommandManager.commandParameters[0]}' was not found");

        else
        {
            if (CheckIfIsAlready(userFile)) return;
            else whitelistManager.RemoveUserFromWhitelist(ServerCommandManager.commandParameters[0]);
        }

        bool CheckIfIsAlready(UserFile userFile)
        {
            if (!Program.whitelist.WhitelistedUsers.Contains(userFile.username))
            {
                logger.LogWarning($"[ERROR] > User '{ServerCommandManager.commandParameters[0]}' " +
                    $"was not whitelisted");

                return true;
            }

            else return false;
        }
    }

    public void WhitelistToggleCommandAction()
    {
        whitelistManager.ToggleWhitelist();
    }

    public void ForceSaveCommandAction()
    {
        Client toFind = network.connectedClients.ToList().Find(x => x.username == ServerCommandManager.commandParameters[0]);
        if (toFind == null) logger.LogWarning($"[ERROR] > User '{ServerCommandManager.commandParameters[0]}' was not found");

        else
        {
            commandManager.SendForceSaveCommand(toFind);

            logger.LogWarning($"User '{ServerCommandManager.commandParameters[0]}' has been forced to save");
        }
    }

    public void DeletePlayerCommandAction()
    {
        UserFile userFile = UserManager.GetUserFileFromName(ServerCommandManager.commandParameters[0]);
        if (userFile == null) logger.LogWarning($"[ERROR] > User '{ServerCommandManager.commandParameters[0]}' was not found");

        else saveManager.DeletePlayerDetails(userFile.username);
    }

    public void EnableDifficultyCommandAction()
    {
        if (Program.difficultyValues.UseCustomDifficulty == true)
        {
            logger.LogWarning($"[ERROR] > Custom difficulty was already enabled");
        }

        else
        {
            Program.difficultyValues.UseCustomDifficulty = true;
            customDifficultyManager.SaveCustomDifficulty(Program.difficultyValues);

            logger.LogWarning($"Custom difficulty is now enabled");
        }
    }

    public void DisableDifficultyCommandAction()
    {
        if (Program.difficultyValues.UseCustomDifficulty == false)
        {
            logger.LogWarning($"[ERROR] > Custom difficulty was already disabled");
        }

        else
        {
            Program.difficultyValues.UseCustomDifficulty = false;
            customDifficultyManager.SaveCustomDifficulty(Program.difficultyValues);

            logger.LogWarning($"Custom difficulty is now disabled");
        }
    }

    public void LockSaveCommandAction()
    {
        // TODO - Compression is different for client and server, causing saves to become useless after executing this
        return;

        byte[] saveFile = SaveManager.GetUserSaveFromUsername(ServerCommandManager.commandParameters[0]);

        if (saveFile == null)
        {
            logger.LogWarning($"[ERROR] > Save {ServerCommandManager.commandParameters[0]} was not found");
        }

        else
        {
            byte[] lockedBytes = GZip.CompressDefault(saveFile);

            File.WriteAllBytes(Path.Combine(Program.savesPath, ServerCommandManager.commandParameters[0] + ".mpsave"), lockedBytes);

            logger.LogInformation($"Save {ServerCommandManager.commandParameters[0]} has been locked");
        }
    }

    public void UnlockSaveCommandAction()
    {
        // TODO - Compression is different for client and server, causing saves to become useless after executing this
        return;

        byte[] saveFile = SaveManager.GetUserSaveFromUsername(ServerCommandManager.commandParameters[0]);

        if (saveFile == null)
        {
            logger.LogWarning($"[ERROR] > Save {ServerCommandManager.commandParameters[0]} was not found");
        }

        else
        {
            byte[] unlockedBytes = GZip.DecompressDefault(saveFile);

            File.WriteAllBytes(Path.Combine(Program.savesPath, ServerCommandManager.commandParameters[0] + ".mpsave"), unlockedBytes);

            logger.LogInformation($"Save {ServerCommandManager.commandParameters[0]} has been unlocked");
        }
    }

    public void QuitCommandAction()
    {
        Program.isClosing = true;

        logger.LogWarning($"Waiting for all saves to quit");

        foreach (Client client in network.connectedClients.ToArray())
        {
            commandManager.SendForceSaveCommand(client);
        }

        while (network.connectedClients.ToArray().Length > 0)
        {
            Thread.Sleep(1);
        }

        hostApplicationLifetime.StopApplication();
    }

    public void ForceQuitCommandAction() { Environment.Exit(0); }

    public void ClearCommandAction()
    {
        Console.Clear();

        logger.LogInformation("[Cleared console]");
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
