using RimworldTogether.GameServer.Files;
using RimworldTogether.GameServer.Managers;
using RimworldTogether.GameServer.Managers.Actions;
using RimworldTogether.GameServer.Misc;
using RimworldTogether.GameServer.Network;
using RimworldTogether.Shared.Misc;
using System.Globalization;

namespace RimworldTogether.GameServer.Core;

public static class Program
{
    public static string mainPath;
    public static string corePath;
    public static string usersPath;
    public static string settlementsPath;
    public static string savesPath;
    public static string mapsPath;
    public static string logsPath;
    public static string sitesPath;
    public static string factionsPath;

    public static string modsPath;
    public static string requiredModsPath;
    public static string optionalModsPath;
    public static string forbiddenModsPath;

    public static ServerConfigFile serverConfig;
    public static ServerValuesFile serverValues;
    public static WorldValuesFile worldValues;
    public static DifficultyValuesFile difficultyValues;
    public static EventValuesFile eventValues;
    public static SiteValuesFile siteValues;
    public static ActionValuesFile actionValues;
    public static WhitelistFile whitelist;

    public static string serverVersion = "1.0.9";

    public static bool isClosing;
    public static CancellationToken serverCancelationToken = new();

    public static async Task Main()
    {
        try
        {
            QuickEdit quickEdit = new QuickEdit();
            quickEdit.DisableQuickEdit();
        }
        catch { };

        Console.ForegroundColor = ConsoleColor.White;

        SetPaths();
        LoadServerConfig();

        var network = new Network.Network();
        var commandManager = new CommandManager(network);
        var responseShortcutManager = new ResponseShortcutManager(network);
        var settlementManager = new SettlementManager(network, responseShortcutManager);
        var siteManager = new SiteManager(network, responseShortcutManager);
        var userManagerJoinings = new UserManager_Joinings(network);
        var userManager = new UserManager(network, userManagerJoinings);
        var saveManager = new SaveManager(network, settlementManager, commandManager, responseShortcutManager, siteManager, userManager);
        var modManager = new ModManager(userManagerJoinings, network);
        var eventManager = new EventManager(network, responseShortcutManager, userManager);
        var likelihoodManager = new LikelihoodManager(network, responseShortcutManager);
        var factionManager = new FactionManager(network, likelihoodManager, responseShortcutManager, siteManager, userManager);
        var visitManager = new VisitManager(network, userManager, responseShortcutManager);
        var chatManager = new ChatManager(network, visitManager);
        var spyManager = new SpyManager(network, userManager);
        var serverOverallManager = new ServerOverallManager(network);
        var worldManager = new WorldManager(network);
        var userLogin = new Users.UserLogin(chatManager, network, userManager, saveManager, serverOverallManager, worldManager,
            userManagerJoinings, modManager);
        var transferManager = new TransferManager(network, userManager, responseShortcutManager);
        var offlineVisitManager = new OfflineVisitManager(network, userManager);
        var raidManager = new RaidManager(network, userManager);
        var customDifficultyManager = new CustomDifficultyManager(responseShortcutManager);
        var userRegister = new Users.UserRegister(userManager, userManagerJoinings, network);
        var packethandler = new PacketHandler(settlementManager, eventManager, factionManager, likelihoodManager, chatManager,
            spyManager, userManagerJoinings, userLogin, saveManager, transferManager, siteManager, visitManager, offlineVisitManager,
            raidManager, worldManager, customDifficultyManager, userRegister, network);
        var serverCommandManager = new ServerCommandManager(network, commandManager, saveManager, modManager);

        LoadResources(modManager, network);

        var networkTask = network.ReadyServer();
        serverCommandManager.ListenForServerCommands();

        while (true) Thread.Sleep(1);
    }

    public static void LoadResources(ModManager modManager, Network.Network network)
    {
        Logger.WriteToConsole($"Loading all necessary resources", Logger.LogMode.Title);
        Logger.WriteToConsole($"----------------------------------------", Logger.LogMode.Title);

        SetCulture();
        CustomDifficultyManager.LoadCustomDifficulty();
        // LoadServerConfig();
        LoadServerValues();
        LoadEventValues();
        LoadSiteValues();
        WorldManager.LoadWorldFile();
        LoadActionValues();
        WhitelistManager.LoadServerWhitelist();
        modManager.LoadMods();

        Titler.ChangeTitle(network.ConnectedClients, int.Parse(serverConfig.MaxPlayers));

        Logger.WriteToConsole($"----------------------------------------", Logger.LogMode.Title);
    }

    private static void SetCulture()
    {
        CultureInfo.CurrentCulture = new CultureInfo("en-US", false);
        CultureInfo.CurrentUICulture = new CultureInfo("en-US", false);
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US", false);
        CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US", false);

        Logger.WriteToConsole($"Loaded server culture > [{CultureInfo.CurrentCulture}]");
    }

    private static void SetPaths()
    {
        mainPath = Directory.GetCurrentDirectory();
        corePath = Path.Combine(mainPath, "Core");
        usersPath = Path.Combine(mainPath, "Users");
        settlementsPath = Path.Combine(mainPath, "Settlements");
        savesPath = Path.Combine(mainPath, "Saves");
        mapsPath = Path.Combine(mainPath, "Maps");
        logsPath = Path.Combine(mainPath, "Logs");
        sitesPath = Path.Combine(mainPath, "Sites");
        factionsPath = Path.Combine(mainPath, "Factions");

        modsPath = Path.Combine(mainPath, "Mods");
        requiredModsPath = Path.Combine(modsPath, "Required");
        optionalModsPath = Path.Combine(modsPath, "Optional");
        forbiddenModsPath = Path.Combine(modsPath, "Forbidden");

        if (!Directory.Exists(corePath)) Directory.CreateDirectory(corePath);
        if (!Directory.Exists(usersPath)) Directory.CreateDirectory(usersPath);
        if (!Directory.Exists(settlementsPath)) Directory.CreateDirectory(settlementsPath);
        if (!Directory.Exists(savesPath)) Directory.CreateDirectory(savesPath);
        if (!Directory.Exists(mapsPath)) Directory.CreateDirectory(mapsPath);
        if (!Directory.Exists(logsPath)) Directory.CreateDirectory(logsPath);
        if (!Directory.Exists(sitesPath)) Directory.CreateDirectory(sitesPath);
        if (!Directory.Exists(factionsPath)) Directory.CreateDirectory(factionsPath);

        if (!Directory.Exists(modsPath)) Directory.CreateDirectory(modsPath);
        if (!Directory.Exists(requiredModsPath)) Directory.CreateDirectory(requiredModsPath);
        if (!Directory.Exists(optionalModsPath)) Directory.CreateDirectory(optionalModsPath);
        if (!Directory.Exists(forbiddenModsPath)) Directory.CreateDirectory(forbiddenModsPath);
    }

    private static void LoadServerConfig()
    {
        string path = Path.Combine(corePath, "ServerConfig.json");

        if (File.Exists(path)) serverConfig = Serializer.SerializeFromFile<ServerConfigFile>(path);
        else
        {
            serverConfig = new ServerConfigFile();
            Serializer.SerializeToFile(path, serverConfig);
        }

        Logger.WriteToConsole("Loaded server configs");
    }

    private static void LoadServerValues()
    {
        string path = Path.Combine(corePath, "ServerValues.json");

        if (File.Exists(path)) serverValues = Serializer.SerializeFromFile<ServerValuesFile>(path);
        else
        {
            serverValues = new ServerValuesFile();
            Serializer.SerializeToFile(path, serverValues);
        }

        Logger.WriteToConsole("Loaded server values");
    }

    private static void LoadEventValues()
    {
        string path = Path.Combine(corePath, "EventValues.json");

        if (File.Exists(path)) eventValues = Serializer.SerializeFromFile<EventValuesFile>(path);
        else
        {
            eventValues = new EventValuesFile();
            Serializer.SerializeToFile(path, eventValues);
        }

        Logger.WriteToConsole("Loaded event values");
    }

    private static void LoadSiteValues()
    {
        string path = Path.Combine(corePath, "SiteValues.json");

        if (File.Exists(path)) siteValues = Serializer.SerializeFromFile<SiteValuesFile>(path);
        else
        {
            siteValues = new SiteValuesFile();
            Serializer.SerializeToFile(path, siteValues);
        }

        Logger.WriteToConsole("Loaded site values");
    }

    private static void LoadActionValues()
    {
        string path = Path.Combine(corePath, "ActionValues.json");

        if (File.Exists(path)) actionValues = Serializer.SerializeFromFile<ActionValuesFile>(path);
        else
        {
            actionValues = new ActionValuesFile();
            Serializer.SerializeToFile(path, actionValues);
        }

        Logger.WriteToConsole("Loaded action values");
    }
}